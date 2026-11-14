using SoftwareFactory.Core.Agents;

namespace SoftwareFactory.Core.Projects;

/// <summary>Persisted as an array in tickets/tickets.json using workflow JSON options.</summary>
public class Ticket
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AcceptanceCriteria { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public TicketType Type { get; set; } = TicketType.Feature;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public int Order { get; set; }
    public List<string> DependsOn { get; set; } = [];
    public bool RequiresFrontend { get; set; }
    public bool RequiresBrowserTests { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TicketQuestion> Questions { get; set; } = [];
    public List<TicketComment> Comments { get; set; } = [];
    public List<string> EvidencePaths { get; set; } = [];
    public string? BranchName { get; set; }
    public PullRequestReference? PullRequest { get; set; }
    public string? SourceReleaseId { get; set; }
    public int ReworkCount { get; set; }

    public bool CanStart(IReadOnlySet<string> completedTicketIds)
    {
        ArgumentNullException.ThrowIfNull(completedTicketIds);
        return Status is TicketStatus.Ready or TicketStatus.Todo or TicketStatus.ChangesRequested
            && !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(ProjectId)
            && !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(AcceptanceCriteria)
            && Order >= 0 && DependsOn.All(id => id != Id && completedTicketIds.Contains(id))
            && !Questions.Any(question => question.IsBlocking
                && (question.Status != TicketQuestionStatus.Answered || string.IsNullOrWhiteSpace(question.Answer)));
    }
}

public sealed class TicketQuestion
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public bool IsBlocking { get; set; } = true;
    public TicketQuestionStatus Status { get; set; } = TicketQuestionStatus.Open;
    public string? Answer { get; set; }
    public AgentRole AskedBy { get; set; } = AgentRole.Architect;
    public AgentRole? AnsweredBy { get; set; }
    public List<string> Sources { get; set; } = [];
}

public sealed class TicketComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public AgentRole Author { get; set; }
    public AgentRole? AddressedTo { get; set; }
    public string StepId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> EvidencePaths { get; set; } = [];
}

public sealed class PullRequestReference
{
    public string Provider { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string HeadCommitSha { get; set; } = string.Empty;
    public string? ApprovedHeadCommitSha { get; set; }
    public string? MergeCommitSha { get; set; }
}

public enum TicketType { Foundation, Feature, Bug, Change }
public enum TicketPriority { Critical, High, Normal, Low }
public enum TicketQuestionStatus { Open, Answered, NeedsOwner }

public enum TicketStatus
{
    New = 0,
    Ready = 1,
    Todo = 2,
    InProgress = 3,
    ReadyForReview = 4,
    ReadyForTesting = 5,
    ReadyForDeployment = 6,
    Done = 7,
    ReadyForFrontend = 8,
    ChangesRequested = 9,
    Blocked = 10,
    Cancelled = 11
}
