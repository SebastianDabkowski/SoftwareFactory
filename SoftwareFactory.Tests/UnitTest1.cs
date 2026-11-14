using SoftwareFactory.Core;
using SoftwareFactory.Core.Agents;

namespace SoftwareFactory.Tests
{
    public class AgentFactoryTests
    {
        [Theory]
        [InlineData(AgentRole.Architect, PiModels.Reasoning.High)]
        [InlineData(AgentRole.Developer, PiModels.Reasoning.Medium)]
        [InlineData(AgentRole.Frontend, PiModels.Reasoning.Medium)]
        [InlineData(AgentRole.QA, PiModels.Reasoning.Low)]
        [InlineData(AgentRole.DevOpsEngineer, PiModels.Reasoning.High)]
        [InlineData(AgentRole.MaintenanceEngineer, PiModels.Reasoning.High)]
        [InlineData(AgentRole.QualityEngineer, PiModels.Reasoning.High)]
        [InlineData(AgentRole.BusinessAnalyst, PiModels.Reasoning.High)]
        [InlineData(AgentRole.Monitoring, PiModels.Reasoning.High)]
        public void CreateAgent_ConfiguresRoleWithoutStartingPi(AgentRole role, string effort)
        {
            var workspace = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            var agent = AgentFactory.CreateAgent(role, workspace);

            Assert.Equal((int)role, agent.Id);
            Assert.Equal(role.ToString(), agent.AgentName);
            Assert.False(string.IsNullOrWhiteSpace(agent.Instruction));
            Assert.Equal(PiModels.Models.Gpt61Sol, agent.Config.Model);
            Assert.Equal("openai", agent.Config.Provider);
            Assert.Equal(effort, agent.Config.ThinkingLevel);
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
        public async Task StartAgent_RejectsEmptyPromptBeforeStartingPi(string prompt)
        {
            var agent = new AgentBase(".");

            await Assert.ThrowsAsync<ArgumentException>(() => agent.StartAgent(prompt));
        }

        [Theory]
        [InlineData("", "Create an API")]
        [InlineData(" ", "Create an API")]
        [InlineData(".", "")]
        [InlineData(".", " ")]
        public async Task SoftwareSystem_RejectsEmptyInputsBeforeStartingPi(string workspace, string goal)
        {
            var factory = new SoftwareSystem();

            await Assert.ThrowsAsync<ArgumentException>(() => factory.Start(workspace, goal));
        }
    }
}
