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
        public Dictionary<DateTime, TestScenarioStatus> ExecutionHistory { get; set; } = new Dictionary<DateTime, TestScenarioStatus>();
    }

    public enum TestScenarioStatus
    {
        Fail = 0,
        Pass = 1,
    }
}
