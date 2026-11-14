using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SoftwareFactory.Core.Agents;
using SoftwareFactory.Core.Projects;
using SoftwareFactory.Core.Workflow;

namespace SoftwareFactory.Tests;

public class WorkflowTests
{
    [Fact]
    public void Definition_HasFiveStagesRolesAndDeploymentApprovals()
    {
        var definition = WorkflowDefinitionStore.LoadDefault();
        Assert.Equal("0.1.0", definition.Version);
        Assert.Equal("business.research", definition.EntryStepId);
        Assert.Equal(new[] { "business", "architecture", "implementation", "testing", "deployment" },
            definition.Stages.Select(stage => stage.Id));
        var steps = definition.Stages.SelectMany(stage => stage.Steps).ToArray();
        Assert.Contains(steps, step => step.Role == AgentRole.Monitoring);
        Assert.True(steps.Single(step => step.Id == "testing.release").RequiresApproval);
        Assert.True(steps.Single(step => step.Id == "deployment.stage").RequiresApproval);
        Assert.Contains(steps.Single(step => step.Id == "implementation.playwright").Transitions,
            transition => transition.Outcome == "failed" && transition.Target == "implementation.backend");
        Assert.Contains(steps.Single(step => step.Id == "implementation.review").Transitions,
            transition => transition.Outcome == "changes_requested" && transition.Target == "implementation.backend");
        Assert.Contains(steps.Single(step => step.Id == "testing.triage").Transitions,
            transition => transition.Outcome == "bugs_found" && transition.Target == "implementation.select-ticket");
    }

    [Fact]
    public void Definition_RoundTripsWithReadableEnums()
    {
        string json = WorkflowDefinitionStore.Serialize(WorkflowDefinitionStore.LoadDefault());
        Assert.Contains("\"role\": \"businessAnalyst\"", json);
        Assert.Equal(json, WorkflowDefinitionStore.Serialize(WorkflowDefinitionStore.Parse(json)));
    }

    [Fact]
    public void Definition_RejectsUnknownTargetAndDuplicateStep()
    {
        var definition = WorkflowDefinitionStore.LoadDefault();
        definition.Stages[0].Steps[0].Transitions[0] = new WorkflowTransition { Outcome = "completed", Target = "missing" };
        Assert.Throws<InvalidDataException>(() => WorkflowDefinitionStore.Validate(definition));
        definition = WorkflowDefinitionStore.LoadDefault();
        definition.Stages[1].Steps.Add(definition.Stages[0].Steps[0]);
        Assert.Throws<InvalidDataException>(() => WorkflowDefinitionStore.Validate(definition));
    }

    [Fact]
    public void Definition_RejectsUnreachableSteps()
    {
        var definition = WorkflowDefinitionStore.LoadDefault();
        definition.Stages[0].Steps[0].Transitions.Clear();
        definition.Stages[0].Steps[0].Transitions.Add(new WorkflowTransition { Outcome = "done", Target = WorkflowTerminals.Completed });
        Assert.Throws<InvalidDataException>(() => WorkflowDefinitionStore.Validate(definition));
    }

    [Fact]
    public void Definition_RejectsClosedInfiniteLoop()
    {
        var definition = WorkflowDefinitionStore.LoadDefault() with
        {
            EntryStepId = "start",
            Stages = [new WorkflowStage
            {
                Id = "stage", Name = "Stage", Steps = [new WorkflowStep
                {
                    Id = "start", Name = "Start", Role = AgentRole.Architect, Instructions = "Work",
                    Transitions = [new WorkflowTransition { Outcome = "again", Target = "start" }]
                }]
            }]
        };
        var error = Assert.Throws<InvalidDataException>(() => WorkflowDefinitionStore.Validate(definition));
        Assert.Contains("loop", error.Message);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("docs/../../outside")]
    [InlineData("C:\\secrets")]
    [InlineData("/outside")]
    public void Definition_RejectsArtifactsOutsideWorkspace(string path)
    {
        var definition = WorkflowDefinitionStore.LoadDefault();
        definition.Stages[0].Steps[0].Outputs.Add(path);
        Assert.Throws<InvalidDataException>(() => WorkflowDefinitionStore.Validate(definition));
    }

    [Fact]
    public void Definition_RejectsDisabledEvidenceAndUnboundedRetries()
    {
        var definition = WorkflowDefinitionStore.LoadDefault();
        Assert.Throws<InvalidDataException>(() => WorkflowDefinitionStore.Validate(definition with
        {
            Policy = definition.Policy with { MaxTicketReworkCycles = 0 }
        }));
        Assert.Throws<InvalidDataException>(() => WorkflowDefinitionStore.Validate(definition with
        {
            Policy = definition.Policy with { RequireEvidenceForPass = false }
        }));
    }

    [Fact]
    public async Task Prepare_WritesPinnedSnapshotEmptyBacklogAndHonestInitialState()
    {
        string parent = Path.Combine(Path.GetTempPath(), "Factory workflow " + Guid.NewGuid().ToString("N"));
        string workspace = Path.Combine(parent, "project");
        const string goal = "Sklep z agentami AI — cel właściciela.";
        try
        {
            var definition = WorkflowDefinitionStore.LoadDefault();
            var prepared = await FactoryProjectPreparer.PrepareAsync(workspace, goal, definition);
            Assert.Equal(FactoryRunStatus.Prepared, prepared.State.Status);
            Assert.Equal(definition.EntryStepId, prepared.State.CurrentStepId);
            Assert.Equal(prepared.Project.Id, prepared.State.ProjectId);
            Assert.Equal(goal, File.ReadAllText(Path.Combine(workspace, "BRIEF.md")));
            string snapshot = File.ReadAllText(Path.Combine(workspace, "process/definition.json"));
            Assert.Equal("0.1.0", WorkflowDefinitionStore.Parse(snapshot).Version);
            Assert.Equal(prepared.State.DefinitionSha256,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))));
            Assert.Equal(prepared.State.GoalSha256,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(goal))));
            var state = JsonSerializer.Deserialize<FactoryRunState>(File.ReadAllText(Path.Combine(workspace, "process/state.json")),
                WorkflowDefinitionStore.JsonOptions);
            Assert.Equal(prepared.State, state);
            Assert.Empty(JsonSerializer.Deserialize<List<Ticket>>(File.ReadAllText(Path.Combine(workspace, "tickets/tickets.json")),
                WorkflowDefinitionStore.JsonOptions)!);
            string eventLine = Assert.Single(File.ReadAllLines(Path.Combine(workspace, "process/events.jsonl")));
            var preparedEvent = JsonSerializer.Deserialize<FactoryRunEvent>(eventLine, WorkflowDefinitionStore.JsonOptions)!;
            Assert.Equal("project.prepared", preparedEvent.Type);
            Assert.Equal(prepared.State.RunId, preparedEvent.RunId);
            Assert.False(File.Exists(Path.Combine(workspace, "architecture.md")));
            Assert.Empty(Directory.EnumerateFiles(Path.Combine(workspace, "releases"), "*", SearchOption.AllDirectories));
            Assert.DoesNotContain(Directory.GetDirectories(parent), path => path.Contains(".prepare-", StringComparison.Ordinal));

            definition.Stages[0].Steps.Clear();
            Assert.NotEmpty(WorkflowDefinitionStore.Parse(File.ReadAllText(Path.Combine(workspace, "process/definition.json"))).Stages[0].Steps);
        }
        finally
        {
            if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task Prepare_DoesNotOverwriteExistingProject()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "Factory existing " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            File.WriteAllText(Path.Combine(workspace, "keep.txt"), "keep");
            await Assert.ThrowsAsync<IOException>(() => FactoryProjectPreparer.PrepareAsync(workspace, "Goal"));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(workspace, "keep.txt")));
            Assert.Single(Directory.GetFiles(workspace));
        }
        finally { Directory.Delete(workspace, recursive: true); }
    }

    [Fact]
    public async Task Prepare_RejectsEmptyGoalAndCancellationBeforeCreatingDirectories()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "Factory cancelled " + Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<ArgumentException>(() => FactoryProjectPreparer.PrepareAsync(workspace, " "));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FactoryProjectPreparer.PrepareAsync(workspace, "Goal",
            cancellationToken: cancelled.Token));
        Assert.False(Directory.Exists(workspace));
    }

    [Fact]
    public void Ticket_RequiresAcceptanceCriteriaAnswersAndCompletedDependencies()
    {
        var ticket = new Ticket
        {
            Id = "T-002", ProjectId = "project", Title = "Feature", Status = TicketStatus.Ready,
            DependsOn = ["T-001"],
            Questions = [new TicketQuestion { Id = "Q-1", Question = "Who may buy?" }]
        };
        var completed = new HashSet<string> { "T-001" };
        Assert.False(ticket.CanStart(completed));
        ticket.AcceptanceCriteria = "Only authenticated customers can buy.";
        Assert.False(ticket.CanStart(completed));
        ticket.Questions[0].Status = TicketQuestionStatus.Answered;
        Assert.False(ticket.CanStart(completed));
        ticket.Questions[0].Answer = "Authenticated customers";
        Assert.False(ticket.CanStart(new HashSet<string>()));
        Assert.True(ticket.CanStart(completed));
        ticket.Status = TicketStatus.Blocked;
        Assert.False(ticket.CanStart(completed));
        ticket.Status = TicketStatus.ChangesRequested;
        Assert.True(ticket.CanStart(completed));
    }

    [Fact]
    public void Ticket_RoundTripsStructuredCommentsQuestionsAndPullRequest()
    {
        var ticket = new Ticket
        {
            Id = "T-001", Type = TicketType.Foundation, Priority = TicketPriority.High,
            Comments = [new TicketComment { Author = AgentRole.QA, AddressedTo = AgentRole.Developer,
                StepId = "implementation.playwright", Message = "Login fails", EvidencePaths = ["artifacts/trace.zip"] }],
            PullRequest = new PullRequestReference { Url = "https://example.test/pr/1", HeadCommitSha = "abc" }
        };
        string json = JsonSerializer.Serialize(ticket, WorkflowDefinitionStore.JsonOptions);
        var restored = JsonSerializer.Deserialize<Ticket>(json, WorkflowDefinitionStore.JsonOptions)!;
        Assert.Equal(TicketType.Foundation, restored.Type);
        Assert.Equal(AgentRole.Developer, Assert.Single(restored.Comments).AddressedTo);
        Assert.Equal("abc", restored.PullRequest!.HeadCommitSha);
        Assert.Contains("\"type\": \"foundation\"", json);
        Assert.Equal(TestScenarioStatus.NotRun, new TestScenario().Status);
    }

    [Fact]
    public void CommandLine_PreparesAnyGoalOrPrintsDefinitionWithoutStartingAgents()
    {
        var command = FactoryCommandLine.Parse(["--prepare-project", "--goal", "Build a booking app", "--workspace", "my project"]);
        Assert.True(command.PrepareProject);
        Assert.Equal("Build a booking app", command.Goal);
        Assert.True(FactoryCommandLine.Parse(["--show-process"]).ShowProcess);
        Assert.Equal("goal.md", FactoryCommandLine.Parse(["--prepare-project", "--goal-file", "goal.md"]).GoalFile);
        Assert.True(FactoryCommandLine.Parse(["--prepare-project", "--scenario", "ai-agent-store", "--dry-run"]).DryRun);
    }

    [Fact]
    public void CommandLine_RejectsConflictingGoalsAndUnsafeModeCombinations()
    {
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse(["--prepare-project"]));
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse(["--goal", "A", "--goal-file", "goal.md"]));
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse(["--scenario", "ai-agent-store", "--goal", "A"]));
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse(["--prepare-project", "--goal", "A", "--check-harness"]));
        Assert.Throws<ArgumentException>(() => FactoryCommandLine.Parse(["--show-process", "--prepare-project", "--goal", "A"]));
    }
}
