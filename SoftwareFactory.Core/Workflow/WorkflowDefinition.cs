using SoftwareFactory.Core.Agents;

namespace SoftwareFactory.Core.Workflow;

public sealed record WorkflowDefinition
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Name { get; init; }
    public required string EntryStepId { get; init; }
    public required WorkflowPolicy Policy { get; init; }
    public required List<WorkflowStage> Stages { get; init; }
}

public sealed record WorkflowPolicy
{
    public int MaxClarificationRounds { get; init; } = 3;
    public int MaxTicketReworkCycles { get; init; } = 3;
    public int MaxReleaseCycles { get; init; } = 3;
    public string IntegrationBranch { get; init; } = "develop";
    public string TicketBranchPattern { get; init; } = "ticket/{ticketId}-{slug}";
    public bool RequireEvidenceForPass { get; init; } = true;
    public bool BlockOnUnansweredBusinessQuestions { get; init; } = true;
}

public sealed record WorkflowStage
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required List<WorkflowStep> Steps { get; init; }
}

public sealed record WorkflowStep
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required AgentRole Role { get; init; }
    public required string Instructions { get; init; }
    public List<string> Inputs { get; init; } = [];
    public List<string> Outputs { get; init; } = [];
    public bool RequiresApproval { get; init; }
    public required List<WorkflowTransition> Transitions { get; init; }
}

public sealed record WorkflowTransition
{
    public required string Outcome { get; init; }
    public required string Target { get; init; }
}

public static class WorkflowTerminals
{
    public const string Completed = "$completed";
    public const string Blocked = "$blocked";
    public const string Failed = "$failed";

    public static bool IsTerminal(string target) => target is Completed or Blocked or Failed;
}
