using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SoftwareFactory.Core.Projects;

namespace SoftwareFactory.Core.Workflow;

public sealed record PreparedFactoryProject(Project Project, FactoryRunState State);

/// <summary>Creates a version-pinned project. Does not run agents, Git, CI or deployments.</summary>
public static class FactoryProjectPreparer
{
    public static async Task<PreparedFactoryProject> PrepareAsync(string workspace, string goal,
        WorkflowDefinition? definition = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        cancellationToken.ThrowIfCancellationRequested();
        definition ??= WorkflowDefinitionStore.LoadDefault();
        string snapshot = WorkflowDefinitionStore.Serialize(definition) + "\n";
        string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace));
        var parent = Directory.GetParent(target)
            ?? throw new ArgumentException("A filesystem root cannot be a project workspace.", nameof(workspace));
        if (File.Exists(target) || Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            throw new IOException("Project workspace must be empty or absent. Existing project files were not changed.");

        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = Path.GetFileName(target),
            Goal = goal,
            Path = target,
            ProcessId = definition.Id,
            ProcessVersion = definition.Version,
            CreatedAt = now
        };
        var state = new FactoryRunState
        {
            RunId = Guid.NewGuid().ToString("N"),
            ProjectId = project.Id,
            ProcessId = definition.Id,
            ProcessVersion = definition.Version,
            DefinitionSha256 = Hash(snapshot),
            GoalSha256 = Hash(goal),
            CurrentStepId = definition.EntryStepId,
            CreatedAt = now,
            UpdatedAt = now
        };
        var preparedEvent = new FactoryRunEvent(Guid.NewGuid().ToString("N"), state.RunId,
            state.CurrentStepId, "project.prepared", now,
            "Project prepared. No agents, Git commands, tests or deployments have been executed.");

        // Build the complete initial state in a sibling folder; publish it only after all writes succeed.
        Directory.CreateDirectory(parent.FullName);
        string staging = Path.Combine(parent.FullName, $".{project.Name}.prepare-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            foreach (string directory in new[]
            {
                "process", "tickets", "docs/business", "docs/architecture", "docs/adr", "docs/design",
                "artifacts/tickets", "artifacts/releases", "releases/test", "releases/stage"
            })
                Directory.CreateDirectory(Path.Combine(staging, directory));
            await WriteAsync("BRIEF.md", goal);
            await WriteAsync("project.json", JsonSerializer.Serialize(project, WorkflowDefinitionStore.JsonOptions));
            await WriteAsync("process/definition.json", snapshot);
            await WriteAsync("process/state.json", JsonSerializer.Serialize(state, WorkflowDefinitionStore.JsonOptions));
            // Events are JSONL, unlike the pretty-printed state/definition documents.
            var eventOptions = new JsonSerializerOptions(WorkflowDefinitionStore.JsonOptions) { WriteIndented = false };
            await WriteAsync("process/events.jsonl", JsonSerializer.Serialize(preparedEvent, eventOptions) + "\n");
            await WriteAsync("tickets/tickets.json", "[]\n");
            await WriteAsync("README.factory.md", $"""
                # Prepared Software Factory project

                Process: {definition.Id} {definition.Version}
                Run: {state.RunId}
                Status: Prepared
                First step: {state.CurrentStepId}

                BRIEF.md is the project goal. process/definition.json is the pinned workflow snapshot.
                process/state.json is the initial state. process/events.jsonl contains preparation events.
                tickets/tickets.json is intentionally empty; the architect will create the backlog.
                Empty documentation/release folders are not evidence of completed work.

                Preparation has not run agents, created branches/PRs, executed tests or deployed software.
                The full workflow executor and server/CI integrations are not implemented yet.
                Do not use the legacy SoftwareSystem.Start runner to resume this prepared workflow:
                it runs only the earlier Architect/Developer/optional QA demonstration.
                """);

            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(target))
                Directory.Delete(target, recursive: false); // Fails safely if it is no longer empty.
            Directory.Move(staging, target);
            return new PreparedFactoryProject(project, state);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }

        Task WriteAsync(string relativePath, string content) => File.WriteAllTextAsync(
            Path.Combine(staging, relativePath), content, new UTF8Encoding(false), cancellationToken);
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
