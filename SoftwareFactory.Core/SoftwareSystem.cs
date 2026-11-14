using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SoftwareFactory.Core
{
    public class SoftwareSystem
    {
        public async Task Start(string projectPath)
        {
            string _workspace = Path.GetFullPath(projectPath);
            var architect = new AgentSystem(_workspace);
            architect.AgentName = "Architect";
            architect.Instruction = "You are an architect. You will create a software architecture for the project.";



            string prompt = """
                Create a simple ASP.NET Core Web API.
                Requirements:
                - .NET
                - Products CRUD
                - In-memory storage
                - Swagger/OpenAPI
                - Unit tests
                Create all files in the current workspace.
                """;
            var agnetCoder = new AgentSystem(_workspace);
            await agnetCoder.StartAgent(prompt);


        }
    }
}