using SoftwareFactory.Core.Agents;

namespace SoftwareFactory.Core
{
    public class SoftwareSystem
    {
        public async Task Start(string projectPath, string goal)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(goal);

            string workspace = Path.GetFullPath(projectPath);
            var architect = AgentFactory.CreateAgent(AgentRole.Architect, workspace);
            await architect.StartAgent($"""
                Create a software architecture for the following project:
                {goal}
                Save the architecture in ARCHITECTURE.md in the current workspace
                so that the developer agent can use it.
                """);

            var developer = AgentFactory.CreateAgent(AgentRole.Developer, workspace);
            await developer.StartAgent($"""
                Implement the following project using ARCHITECTURE.md in the current workspace:
                {goal}
                Create all files in the current workspace.
                """);
        }
    }
}