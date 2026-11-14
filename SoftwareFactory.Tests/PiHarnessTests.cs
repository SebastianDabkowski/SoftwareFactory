using System.Diagnostics;
using System.Text.Json.Nodes;
using SoftwareFactory.Core.Agents;

namespace SoftwareFactory.Tests;

public sealed class PiHarnessTests : IAsyncLifetime
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "Software Factory pi test " + Guid.NewGuid().ToString("N"));
    private AgentBase? _agent;

    private AgentBase CreateAgent(PiHarnessOptions? options = null)
    {
        options ??= new PiHarnessOptions
        {
            CliPath = Path.Combine(AppContext.BaseDirectory, "FakePi.mjs"),
            RequestTimeout = TimeSpan.FromSeconds(5),
            RunTimeout = TimeSpan.FromSeconds(5),
            ShutdownTimeout = TimeSpan.FromMilliseconds(300)
        };
        return _agent = AgentFactory.CreateAgent(AgentRole.Developer, _workspace, options);
    }

    private JsonObject StartRecord() => JsonNode.Parse(File.ReadAllText(Path.Combine(_workspace, "harness-start.json")))!.AsObject();
    private JsonObject[] Commands()
    {
        using var stream = new FileStream(Path.Combine(_workspace, "harness-commands.jsonl"),
            FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string text = reader.ReadToEnd();
        return text.Split('\n').SkipLast(1).Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonNode.Parse(line)!.AsObject()).ToArray();
    }

    [Fact]
    public async Task StartAndMessage_UseOnePiSessionAndCorrectArguments()
    {
        var agent = CreateAgent();
        agent.Instruction = "Developer instructions: \"quoted\" & echo NOT_A_SHELL_COMMAND\nDruga linia";

        await agent.StartAgent("first");
        Assert.Equal("Reply 1: first", agent.LastResponse);
        Assert.Equal("fake-session", agent.SessionId);
        Assert.NotNull(agent.SessionFile);
        await agent.Message("second");
        Assert.Equal("Reply 2: second", agent.LastResponse);

        var start = StartRecord();
        Assert.Equal(Path.GetFullPath(_workspace).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(start["cwd"]!.GetValue<string>()).TrimEnd(Path.DirectorySeparatorChar));
        string[] arguments = start["args"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
        Assert.Equal("rpc", arguments[Array.IndexOf(arguments, "--mode") + 1]);
        Assert.Contains("--no-approve", arguments);
        Assert.DoesNotContain("--no-session", arguments);
        Assert.Equal(agent.Instruction, arguments[Array.IndexOf(arguments, "--append-system-prompt") + 1]);
        Assert.Equal(2, Commands().Count(command => command["type"]!.GetValue<string>() == "prompt"));

        await agent.StopAgentAsync();
        int pid = start["pid"]!.GetValue<int>();
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid));
        await agent.StopAgentAsync();
    }

    [Fact]
    public async Task StartHarness_ChecksReadinessWithoutPromptingModel()
    {
        var agent = CreateAgent();
        await agent.StartHarnessAsync();
        Assert.Equal("fake-session", agent.SessionId);
        Assert.DoesNotContain(Commands(), command => command["type"]!.GetValue<string>() == "prompt");
        await agent.Message("first");
        Assert.Equal("Reply 1: first", agent.LastResponse);
    }

    [Fact]
    public async Task Completion_WaitsForSettledNotAgentEnd()
    {
        var agent = CreateAgent();
        await agent.StartAgent("delayed");
        Assert.Equal("Settled after agent_end", agent.LastResponse);
    }

    [Theory]
    [InlineData("handled", "")]
    [InlineData("retry", "Reply 1: retry")]
    [InlineData("ui", "Dialog cancelled safely")]
    public async Task Prompt_HandlesExtensionsAndRetry(string prompt, string expected)
    {
        var agent = CreateAgent();
        await agent.StartAgent(prompt);
        Assert.Equal(expected, agent.LastResponse);
    }

    [Theory]
    [InlineData("reject", "Prompt rejected")]
    [InlineData("error", "Provider unavailable")]
    public async Task Prompt_PropagatesErrorsAndCleansUp(string prompt, string expected)
    {
        var agent = CreateAgent();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => agent.StartAgent(prompt));
        Assert.Contains(expected, error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.Message("after failure"));
    }

    [Fact]
    public async Task Prompt_ReportsAbortedRun()
    {
        var agent = CreateAgent();
        await Assert.ThrowsAsync<OperationCanceledException>(() => agent.StartAgent("aborted"));
    }

    [Fact]
    public async Task HarnessExit_FailsPendingRequestInsteadOfHanging()
    {
        var agent = CreateAgent();
        await Assert.ThrowsAsync<IOException>(() => agent.StartAgent("crash"));
    }

    [Fact]
    public async Task Stop_CancelsAnActiveRunAndKillsUncooperativeHarness()
    {
        var agent = CreateAgent();
        await agent.StartAgent("first");
        var running = agent.Message("hang-shutdown");
        // Wait until the child has accepted the second command.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (Commands().Count(command => command["type"]!.GetValue<string>() == "prompt") < 2)
            await Task.Delay(10, deadline.Token);

        await agent.StopAgentAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        int pid = StartRecord()["pid"]!.GetValue<int>();
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid));
    }

    [Fact]
    public async Task Cancellation_StopsActivePrompt()
    {
        var agent = CreateAgent();
        await agent.StartAgent("first");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agent.Message("hang", cancellation.Token));
    }

    [Fact]
    public async Task RequestTimeout_CleansUpHarness()
    {
        var agent = CreateAgent(new PiHarnessOptions
        {
            CliPath = Path.Combine(AppContext.BaseDirectory, "FakePi.mjs"),
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            RunTimeout = TimeSpan.FromSeconds(5),
            ShutdownTimeout = TimeSpan.FromMilliseconds(300)
        });
        await agent.StartAgent("first");
        await Assert.ThrowsAsync<TimeoutException>(() => agent.Message("ignore"));
    }

    [Fact]
    public async Task RunTimeout_StopsAnAcceptedButUnfinishedPrompt()
    {
        var agent = CreateAgent(new PiHarnessOptions
        {
            CliPath = Path.Combine(AppContext.BaseDirectory, "FakePi.mjs"),
            RequestTimeout = TimeSpan.FromSeconds(5),
            RunTimeout = TimeSpan.FromMilliseconds(100),
            ShutdownTimeout = TimeSpan.FromMilliseconds(300)
        });
        await agent.StartHarnessAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => agent.Message("hang"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.Message("after timeout"));
    }

    [Fact]
    public async Task SetModel_SupportsCustomProvidersAndLiveRpcChanges()
    {
        var agent = CreateAgent();
        var initial = new PiModelConfig("custom-model", "high", "custom-provider");
        await agent.SetModel(initial);
        Assert.Equal(initial, agent.Config);
        await agent.StartAgent("first");
        var replacement = new PiModelConfig("another-model", "low", "another-provider");
        await agent.SetModel(replacement);

        Assert.Equal(replacement, agent.Config);
        var setModel = Assert.Single(Commands(), command => command["type"]!.GetValue<string>() == "set_model");
        Assert.Equal("another-provider", setModel["provider"]!.GetValue<string>());
        Assert.Equal("another-model", setModel["modelId"]!.GetValue<string>());
        var thinking = Assert.Single(Commands(), command => command["type"]!.GetValue<string>() == "set_thinking_level");
        Assert.Equal("low", thinking["level"]!.GetValue<string>());
    }

    [Fact]
    public async Task Message_RequiresStartedHarness()
    {
        var agent = CreateAgent();
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.Message("first"));
    }

    [Fact]
    public async Task Start_RejectsSecondStartWithoutDestroyingExistingSession()
    {
        var agent = CreateAgent();
        await agent.StartAgent("first");
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.StartAgent("again"));
        await agent.Message("second");
        Assert.Equal("Reply 2: second", agent.LastResponse);
    }

    [Fact]
    public async Task InvalidThinkingLevel_IsRejectedBeforeStartingHarness()
    {
        var agent = CreateAgent();
        await Assert.ThrowsAsync<ArgumentException>(() => agent.SetModel(new PiModelConfig(ThinkingLevel: "invalid")));
        Assert.False(Directory.Exists(_workspace));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_agent is not null)
            await _agent.DisposeAsync();
        if (Directory.Exists(_workspace))
            Directory.Delete(_workspace, recursive: true);
    }
}
