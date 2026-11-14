using SoftwareFactory.Core;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;


string taskDescription = """
                Create a simple ASP.NET Core Web API.
                Requirements:
                - .NET
                - Products CRUD
                - In-memory storage
                - Swagger/OpenAPI
                - Unit tests
                Create all files in the current workspace.
                """;
string workspace = Path.GetFullPath("./workspace2");
var agnetCoder = new AgentSystem(workspace);
await agnetCoder.StartAgent(taskDescription);
        