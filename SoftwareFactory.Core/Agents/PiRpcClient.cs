using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace SoftwareFactory.Core.Agents;

/// <summary>JSONL transport. Exactly one reader owns stdout; responses are correlated by id.</summary>
internal sealed class PiRpcClient : IAsyncDisposable
{
    private readonly Process _process;
    private readonly PiHarnessOptions _options;
    private readonly Action<JsonObject> _onEvent;
    private readonly Action<Exception> _onDisconnected;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonObject>> _pending = new();
    private readonly ConcurrentQueue<string> _stderr = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly CancellationTokenSource _readerCancellation = new();
    private readonly Task _stdoutTask;
    private readonly Task _stderrTask;
    private Exception? _connectionError;
    private int _disposed;

    internal PiRpcClient(ProcessStartInfo startInfo, PiHarnessOptions options,
        Action<JsonObject> onEvent, Action<Exception> onDisconnected)
    {
        _options = options;
        _onEvent = onEvent;
        _onDisconnected = onDisconnected;
        _process = new Process { StartInfo = startInfo };
        try
        {
            if (!_process.Start())
                throw new InvalidOperationException("Pi process did not start.");
        }
        catch (Exception exception)
        {
            _process.Dispose();
            throw new InvalidOperationException(
                "Cannot start Pi harness. Install Pi and Node.js, check PATH or configure PiHarnessOptions.CliPath.", exception);
        }
        _stderrTask = ReadStderrAsync();
        _stdoutTask = ReadStdoutAsync();
    }

    internal async Task<JsonObject> RequestAsync(string type, JsonObject? parameters, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var command = parameters is null ? new JsonObject() : (JsonObject)parameters.DeepClone();
        string id = Guid.NewGuid().ToString("N");
        command["id"] = id;
        command["type"] = type;
        var completion = new TaskCompletionSource<JsonObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.RequestTimeout);
        try
        {
            if (Volatile.Read(ref _connectionError) is { } error)
                throw new IOException("Pi RPC connection is unavailable.", error);
            await SendAsync(command, deadline.Token);
            var response = await completion.Task.WaitAsync(deadline.Token);
            if (response["success"]?.GetValue<bool>() != true)
                throw new InvalidOperationException($"Pi {type} failed: {response["error"]}");
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Pi {type} did not respond within {_options.RequestTimeout}.");
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task SendAsync(JsonObject record, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            // No BOM. WriteLine uses LF, as required by the Pi JSONL protocol.
            _process.StandardInput.NewLine = "\n";
            await _process.StandardInput.WriteLineAsync(record.ToJsonString().AsMemory(), cancellationToken);
            await _process.StandardInput.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadStdoutAsync()
    {
        Exception error;
        try
        {
            // .NET's reader recognizes CR/LF only, not U+2028/U+2029 inside JSON strings.
            while (await _process.StandardOutput.ReadLineAsync(_readerCancellation.Token) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var record = JsonNode.Parse(line)?.AsObject()
                    ?? throw new InvalidDataException("Pi sent an empty JSON record.");
                if (record["type"]?.GetValue<string>() == "response")
                {
                    string? id = record["id"]?.GetValue<string>();
                    if (id is not null && _pending.TryRemove(id, out var completion))
                        completion.TrySetResult(record);
                    else if (record["success"]?.GetValue<bool>() == false)
                        throw new InvalidDataException($"Uncorrelated Pi error: {record["error"]}");
                }
                else
                {
                    if (record["type"]?.GetValue<string>() == "extension_ui_request")
                        await HandleExtensionUiAsync(record);
                    _onEvent(record);
                }
            }
            error = new IOException($"Pi RPC connection closed. {string.Join(Environment.NewLine, _stderr)}");
        }
        catch (Exception exception)
        {
            error = exception;
        }
        Volatile.Write(ref _connectionError, error);
        foreach (var completion in _pending.Values)
            completion.TrySetException(error);
        _onDisconnected(error);
    }

    private async Task HandleExtensionUiAsync(JsonObject record)
    {
        string? method = record["method"]?.GetValue<string>();
        if (method is "select" or "confirm" or "input" or "editor")
        {
            // A headless factory must never hang on a dialog or approve it implicitly.
            await SendAsync(new JsonObject
            {
                ["type"] = "extension_ui_response",
                ["id"] = record["id"]?.GetValue<string>(),
                ["cancelled"] = true
            }, _readerCancellation.Token);
            Console.Error.WriteLine($"[pi] Cancelled unattended UI request: {record["title"]}");
        }
        else if (method == "notify")
        {
            Console.Error.WriteLine($"[pi] {record["message"]}");
        }
    }

    private async Task ReadStderrAsync()
    {
        try
        {
            while (await _process.StandardError.ReadLineAsync(_readerCancellation.Token) is { } line)
            {
                _stderr.Enqueue(line);
                while (_stderr.Count > 20)
                    _stderr.TryDequeue(out _);
                Console.Error.WriteLine($"[pi stderr] {line}");
            }
        }
        catch (OperationCanceledException) when (_readerCancellation.IsCancellationRequested) { }
        catch (IOException) when (Volatile.Read(ref _disposed) != 0) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        try
        {
            // Abort active model/tool work before closing stdin. EOF alone allows Pi
            // to finish pending work; it is not cancellation.
            try
            {
                using var deadline = new CancellationTokenSource(_options.ShutdownTimeout);
                await SendAsync(new JsonObject { ["type"] = "abort" }, deadline.Token);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or OperationCanceledException) { }
            await _writeLock.WaitAsync();
            try
            {
                try { _process.StandardInput.Close(); }
                catch (IOException) { }
            }
            finally { _writeLock.Release(); }
            try
            {
                await _process.WaitForExitAsync().WaitAsync(_options.ShutdownTimeout);
            }
            catch (TimeoutException)
            {
                try
                {
                    if (!_process.HasExited)
                        _process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (_process.HasExited) { }
                await _process.WaitForExitAsync();
            }
        }
        finally
        {
            _readerCancellation.Cancel();
            try { await Task.WhenAll(_stdoutTask, _stderrTask); }
            finally
            {
                _process.Dispose();
                _readerCancellation.Dispose();
            }
        }
    }
}
