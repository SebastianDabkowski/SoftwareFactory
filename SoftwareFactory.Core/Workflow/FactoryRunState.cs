namespace SoftwareFactory.Core.Workflow;

public sealed record FactoryRunState
{
    public required string RunId { get; init; }
    public required string ProjectId { get; init; }
    public required string ProcessId { get; init; }
    public required string ProcessVersion { get; init; }
    public required string DefinitionSha256 { get; init; }
    public required string GoalSha256 { get; init; }
    public FactoryRunStatus Status { get; init; } = FactoryRunStatus.Prepared;
    public required string CurrentStepId { get; init; }
    public string? CurrentTicketId { get; init; }
    public int ClarificationRound { get; init; }
    public int ReleaseCycle { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum FactoryRunStatus
{
    Prepared, Running, Paused, AwaitingApproval, Blocked, Failed, Cancelled, Completed
}

public sealed record FactoryRunEvent(
    string Id, string RunId, string StepId, string Type, DateTimeOffset Timestamp, string Message);
