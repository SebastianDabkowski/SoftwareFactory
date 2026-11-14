using System.Text.Json.Nodes;

namespace SoftwareFactory.Core.Agents;

/// <summary>One long-lived pi.dev RPC harness and conversation per agent.</summary>
public class AgentBase : IAsyncDisposable
{
    private readonly string _workspace;
    private readonly PiHarnessOptions _options;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private PiRpcClient? _client;
    private CancellationTokenSource? _lifetime;
    private TaskCompletionSource<bool>? _activeRun;
    private string? _runError;

    public string AgentName { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public int Id { get; internal set; }
    public PiModelConfig Config { get; internal set; } = new();
    public string? SessionId { get; private set; }
    public string? SessionFile { get; private set; }
    public string LastResponse { get; private set; } = string.Empty;

    public AgentBase(string projectPath, PiHarnessOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        _workspace = Path.GetFullPath(projectPath);
        _options = options ?? new PiHarnessOptions();
        _options.Validate();
    }

    public async Task StartAgent(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        await _operations.WaitAsync(cancellationToken);
        try
        {
            await StartHarnessCoreAsync(cancellationToken);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime!.Token);
            try
            {
                await RunPromptAsync(prompt, linked.Token);
            }
            catch
            {
                await StopProcessAsync();
                throw;
            }
        }
        finally
        {
            _operations.Release();
        }
    }

    /// <summary>Start Pi and verify RPC readiness without making a model call.</summary>
    public async Task StartHarnessAsync(CancellationToken cancellationToken = default)
    {
        await _operations.WaitAsync(cancellationToken);
        try { await StartHarnessCoreAsync(cancellationToken); }
        finally { _operations.Release(); }
    }

    private async Task StartHarnessCoreAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
            throw new InvalidOperationException("Agent is already started. Use Message() to continue its conversation.");
        cancellationToken.ThrowIfCancellationRequested();
        Config.Validate();
        var startInfo = PiProcessLauncher.Create(_workspace, AgentName, Instruction, Config, _options);
        Directory.CreateDirectory(_workspace);
        _lifetime = new CancellationTokenSource();
        try
        {
            _client = new PiRpcClient(startInfo, _options, HandleEvent,
                error => Volatile.Read(ref _activeRun)?.TrySetException(error));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            var state = await _client.RequestAsync("get_state", null, linked.Token);
            SessionId = state["data"]?["sessionId"]?.GetValue<string>();
            SessionFile = state["data"]?["sessionFile"]?.GetValue<string>();
            Console.WriteLine($"[{AgentName}] Pi session: {SessionId}");
        }
        catch
        {
            await StopProcessAsync();
            throw;
        }
    }

    /// <summary>Send another prompt in the same session; waits for all automatic work to settle.</summary>
    public async Task Message(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        await _operations.WaitAsync(cancellationToken);
        try
        {
            if (_client is null || _lifetime is null)
                throw new InvalidOperationException("Agent is not running. Call StartAgent() first.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            try
            {
                await RunPromptAsync(prompt, linked.Token);
            }
            catch
            {
                await StopProcessAsync();
                throw;
            }
        }
        finally
        {
            _operations.Release();
        }
    }

    public async Task SetModel(PiModelConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        await _operations.WaitAsync(cancellationToken);
        try
        {
            if (_client is not null && _lifetime is not null)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
                try
                {
                    await _client.RequestAsync("set_model", new JsonObject
                    {
                        ["provider"] = config.Provider,
                        ["modelId"] = config.Model
                    }, linked.Token);
                    await _client.RequestAsync("set_thinking_level", new JsonObject
                    {
                        ["level"] = config.ThinkingLevel
                    }, linked.Token);
                }
                catch
                {
                    await StopProcessAsync();
                    throw;
                }
            }
            Config = config;
        }
        finally
        {
            _operations.Release();
        }
    }

    private async Task RunPromptAsync(string prompt, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _runError = null;
        LastResponse = string.Empty;
        // Register before sending: events (including agent_settled) can precede the response.
        Volatile.Write(ref _activeRun, completion);
        try
        {
            var response = await _client!.RequestAsync("prompt", new JsonObject { ["message"] = prompt }, cancellationToken);
            if (response["data"]?["disposition"]?.GetValue<string>() == "handled")
                return;
            bool aborted = await completion.Task.WaitAsync(_options.RunTimeout, cancellationToken);
            if (aborted)
                throw new OperationCanceledException("Pi agent run was aborted.", cancellationToken);
            if (_runError is not null)
                throw new InvalidOperationException($"Pi agent failed: {_runError}");
        }
        finally
        {
            Volatile.Write(ref _activeRun, null);
        }
    }

    private void HandleEvent(JsonObject record)
    {
        switch (record["type"]?.GetValue<string>())
        {
            case "message_update":
                if (record["assistantMessageEvent"]?["type"]?.GetValue<string>() == "text_delta")
                    Console.Write(record["assistantMessageEvent"]?["delta"]?.GetValue<string>());
                break;
            case "message_end":
                if (record["message"]?["role"]?.GetValue<string>() == "assistant")
                {
                    var message = record["message"]!;
                    string? stopReason = message["stopReason"]?.GetValue<string>();
                    _runError = stopReason is "error" or "aborted"
                        ? message["errorMessage"]?.GetValue<string>() ?? stopReason
                        : null;
                    LastResponse = string.Concat(message["content"]?.AsArray()
                        .Where(block => block?["type"]?.GetValue<string>() == "text")
                        .Select(block => block?["text"]?.GetValue<string>() ?? string.Empty) ?? []);
                }
                break;
            case "tool_execution_start":
                Console.WriteLine($"\n[{AgentName}] Tool: {record["toolName"]}");
                break;
            case "tool_execution_end":
                if (record["isError"]?.GetValue<bool>() == true)
                    Console.Error.WriteLine($"[{AgentName}] Tool failed: {record["toolName"]}");
                break;
            case "auto_retry_end":
                if (record["success"]?.GetValue<bool>() == false)
                    _runError = record["finalError"]?.GetValue<string>() ?? "Pi retries exhausted.";
                break;
            case "compaction_end":
                if (record["errorMessage"] is not null && record["willRetry"]?.GetValue<bool>() != true)
                    _runError = record["errorMessage"]!.GetValue<string>();
                break;
            case "agent_settled":
                Volatile.Read(ref _activeRun)?.TrySetResult(record["aborted"]?.GetValue<bool>() == true);
                Console.WriteLine();
                break;
        }
    }

    public void StopAgent() => StopAgentAsync().GetAwaiter().GetResult();

    public async Task StopAgentAsync()
    {
        // Wake a pending prompt/request before waiting for its operation lock.
        try { Volatile.Read(ref _lifetime)?.Cancel(); }
        catch (ObjectDisposedException) { /* An operation already finished cleanup. */ }
        await _operations.WaitAsync();
        try { await StopProcessAsync(); }
        finally { _operations.Release(); }
    }

    private async Task StopProcessAsync()
    {
        var client = _client;
        _client = null;
        try
        {
            if (client is not null)
                await client.DisposeAsync();
        }
        finally
        {
            _lifetime?.Dispose();
            _lifetime = null;
        }
    }

    public async ValueTask DisposeAsync() => await StopAgentAsync();
}
