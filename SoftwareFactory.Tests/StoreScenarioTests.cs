using System.Text.Json.Nodes;
using SoftwareFactory.Core;
using SoftwareFactory.Core.Agents;
using SoftwareFactory.Core.Scenarios;

namespace SoftwareFactory.Tests;

public class StoreScenarioTests
{
    [Fact]
    public void CommandLine_SelectsStoreAndCustomWorkspace()
    {
        var command = FactoryCommandLine.Parse(["--scenario", "ai-agent-store", "--workspace", "folder with spaces", "--dry-run"]);
        Assert.Equal(AiAgentStoreScenario.Name, command.ScenarioName);
        Assert.Equal("folder with spaces", command.WorkspacePath);
        Assert.True(command.DryRun);
        Assert.False(command.CheckHarness);
    }

    [Fact]
    public void CommandLine_PreservesDefaultAndHarnessModes()
    {
        Assert.Null(FactoryCommandLine.Parse([]).ScenarioName);
        Assert.True(FactoryCommandLine.Parse(["--check-harness"]).CheckHarness);
        Assert.True(FactoryCommandLine.Parse(["--help"]).ShowHelp);
    }

    [Theory]
    [InlineData("--scenario")]
    [InlineData("--workspace")]
    [InlineData("--unknown")]
    public void CommandLine_RejectsMissingOrUnknownArguments(string argument)
    {
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse([argument]));
    }

    [Fact]
    public void CommandLine_RejectsUnknownScenarioAndIncompatibleModes()
    {
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse(["--scenario", "unknown"]));
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse(["--dry-run", "--check-harness"]));
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse(["--scenario", "--dry-run"]));
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse(["--dry-run", "--dry-run"]));
    }

    [Theory]
    [InlineData("Support Bot")]
    [InlineData("Email Writer")]
    [InlineData("Meeting Analyst")]
    [InlineData("SQLite")]
    [InlineData("dotnet test AiAgentStore.slnx --no-build")]
    [InlineData("QA_REPORT.md")]
    public void EmbeddedBrief_ContainsAcceptanceCriteria(string requirement)
    {
        Assert.Contains(requirement, AiAgentStoreScenario.Goal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Factory_ExecutesRequestedStagesInOrderWithoutModelCalls(bool includeQa)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "Factory scenario " + Guid.NewGuid().ToString("N"));
        try
        {
            var factory = new SoftwareSystem(new PiHarnessOptions
            {
                CliPath = Path.Combine(AppContext.BaseDirectory, "FakePi.mjs"),
                PersistSessions = false,
                RequestTimeout = TimeSpan.FromSeconds(5),
                RunTimeout = TimeSpan.FromSeconds(5),
                ShutdownTimeout = TimeSpan.FromMilliseconds(300)
            });
            await factory.Start(workspace, AiAgentStoreScenario.Goal, runQualityAssurance: includeQa);

            string[] roles = File.ReadAllLines(Path.Combine(workspace, "harness-starts.jsonl"))
                .Select(line => JsonNode.Parse(line)!["args"]!.AsArray()
                    .Select(value => value!.GetValue<string>()).ToArray())
                .Select(arguments => arguments[Array.IndexOf(arguments, "--name") + 1]).ToArray();
            Assert.Equal(includeQa ? ["Architect", "Developer", "QA"] : new[] { "Architect", "Developer" }, roles);

            var prompts = File.ReadAllLines(Path.Combine(workspace, "harness-commands.jsonl"))
                .Select(line => JsonNode.Parse(line)!)
                .Where(command => command["type"]!.GetValue<string>() == "prompt")
                .Select(command => command["message"]!.GetValue<string>()).ToArray();
            Assert.Equal(roles.Length, prompts.Length);
            Assert.All(prompts, prompt => Assert.Contains(AiAgentStoreScenario.Goal, prompt));
            if (includeQa)
                Assert.Contains("Actually execute restore, build and tests", prompts[^1]);
        }
        finally
        {
            if (Directory.Exists(workspace))
                Directory.Delete(workspace, recursive: true);
        }
    }
}
