using SoftwareFactory.Core;
using SoftwareFactory.Core.Agents;

namespace SoftwareFactory.Tests
{
    public class AgentFactoryTests
    {
        [Theory]
        [InlineData(AgentRole.Architect, CodexModels.Reasoning.High)]
        [InlineData(AgentRole.Developer, CodexModels.Reasoning.Medium)]
        [InlineData(AgentRole.Frontend, CodexModels.Reasoning.Medium)]
        [InlineData(AgentRole.QA, CodexModels.Reasoning.Low)]
        [InlineData(AgentRole.DevOpsEngineer, CodexModels.Reasoning.High)]
        [InlineData(AgentRole.MaintenanceEngineer, CodexModels.Reasoning.High)]
        [InlineData(AgentRole.QualityEngineer, CodexModels.Reasoning.High)]
        [InlineData(AgentRole.BusinessAnalyst, CodexModels.Reasoning.High)]
        public void CreateAgent_ConfiguresRoleWithoutStartingCodex(AgentRole role, string effort)
        {
            var workspace = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            var agent = AgentFactory.CreateAgent(role, workspace);

            Assert.Equal((int)role, agent.Id);
            Assert.Equal(role.ToString(), agent.AgentName);
            Assert.False(string.IsNullOrWhiteSpace(agent.Instruction));
            Assert.Equal(CodexModels.Models.Gpt61Sol, agent.Config.model);
            Assert.Equal(effort, agent.Config.reasoningEffort);
            Assert.False(Directory.Exists(workspace));
            agent.StopAgent();
        }

        [Theory]
        [InlineData(AgentRole.None)]
        [InlineData((AgentRole)999)]
        public void CreateAgent_RejectsUnsupportedRole(AgentRole role)
        {
            Assert.Throws<ArgumentException>(() => AgentFactory.CreateAgent(role, "."));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_RejectsEmptyWorkspace(string workspace)
        {
            Assert.Throws<ArgumentException>(() => new AgentBase(workspace));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public async Task StartAgent_RejectsEmptyPromptBeforeStartingCodex(string prompt)
        {
            var agent = new AgentBase(".");

            await Assert.ThrowsAsync<ArgumentException>(() => agent.StartAgent(prompt));
        }

        [Theory]
        [InlineData("", "Create an API")]
        [InlineData(" ", "Create an API")]
        [InlineData(".", "")]
        [InlineData(".", " ")]
        public async Task SoftwareSystem_RejectsEmptyInputsBeforeStartingCodex(string workspace, string goal)
        {
            var factory = new SoftwareSystem();

            await Assert.ThrowsAsync<ArgumentException>(() => factory.Start(workspace, goal));
        }
    }
}
