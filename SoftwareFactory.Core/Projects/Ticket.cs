using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareFactory.Core.Projects
{
    internal class Ticket
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AcceptanceCriteria { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string AssignedTo { get; set; } = string.Empty;
        public TicketStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<string> Comments { get; set; } = new List<string>();
    }

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
    }
}
