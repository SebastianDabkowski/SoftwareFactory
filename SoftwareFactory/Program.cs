using SoftwareFactory.Core;


string taskDescription = """
                Create a simple ASP.NET Core Web API.
                Requirements:
                - .NET
                - Web Shop applicaion to sell AI agent 
                - In-memory storage              
                - Unit tests
                Create all files in the current workspace.
                """;
string workspace = Path.GetFullPath("./workspace3");
var factory = new SoftwareSystem();
await factory.Start(workspace, taskDescription);
        