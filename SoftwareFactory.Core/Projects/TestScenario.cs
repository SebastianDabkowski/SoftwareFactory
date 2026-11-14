using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareFactory.Core.Projects
{
    public class TestScenario
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TicketId { get; set; } = string.Empty;
        public string Preconditions { get; set; } = string.Empty;
        public List<TestScenarioStep> Steps { get; set; } = [];
        public string Automation { get; set; } = string.Empty;
        public List<string> EvidencePaths { get; set; } = [];
        public TestScenarioStatus Status { get; set; } = TestScenarioStatus.NotRun;
        public Dictionary<DateTime, TestScenarioStatus> ExecutionHistory { get; set; } = new Dictionary<DateTime, TestScenarioStatus>();
    }

    public sealed record TestScenarioStep(string Action, string ExpectedResult);

    public enum TestScenarioStatus
    {
        Fail = 0,
        Pass = 1,
        NotRun = 2,
        Blocked = 3,
        NotApplicable = 4,
    }
}
