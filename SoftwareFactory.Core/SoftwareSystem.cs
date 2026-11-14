using SoftwareFactory.Core.Agents;

namespace SoftwareFactory.Core
{
    /// <summary>
    /// Legacy demo runner (Architect → Developer → optional QA).
    /// It does not execute or resume the versioned five-stage workflow definition.
    /// </summary>
    public class SoftwareSystem
    {
        private readonly PiHarnessOptions? _harnessOptions;
        private readonly PiModelConfig? _modelConfig;

        public SoftwareSystem(PiHarnessOptions? harnessOptions = null, PiModelConfig? modelConfig = null)
        {
            _harnessOptions = harnessOptions;
            _modelConfig = modelConfig;
        }

        public async Task Start(string projectPath, string goal, CancellationToken cancellationToken = default,
            bool runQualityAssurance = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(goal);

            string workspace = Path.GetFullPath(projectPath);
            Console.WriteLine("[Factory] Etap 1: Architect — architektura projektu.");
            await using var architect = AgentFactory.CreateAgent(AgentRole.Architect, workspace, _harnessOptions, _modelConfig);
            await architect.StartAgent($"""
                Create a software architecture for the following project:
                {goal}
                Save the architecture in ARCHITECTURE.md in the current workspace
                so that the developer agent can use it.
                """, cancellationToken);
            await architect.StopAgentAsync();

            Console.WriteLine("[Factory] Etap 2: Developer — implementacja i testy.");
            await using var developer = AgentFactory.CreateAgent(AgentRole.Developer, workspace, _harnessOptions, _modelConfig);
            await developer.StartAgent($"""
                Implement the following project using ARCHITECTURE.md in the current workspace:
                {goal}
                Create all files in the current workspace.
                Deliver a working application, not just a plan. Build the solution and run its tests.
                """, cancellationToken);
            await developer.StopAgentAsync();

            if (runQualityAssurance)
            {
                Console.WriteLine("[Factory] Etap 3: QA — build, testy i smoke test HTTP.");
                await using var qa = AgentFactory.CreateAgent(AgentRole.QA, workspace, _harnessOptions, _modelConfig);
                await qa.StartAgent($"""
                    Verify the implemented application against the following acceptance criteria:
                    {goal}
                    Work only inside the current workspace. Read ARCHITECTURE.md and README.md.
                    Actually execute restore, build and tests; do not rely on the developer's claims.
                    Fix discovered compilation, test and acceptance failures, then rerun verification.
                    Smoke-test the web application locally and stop any server you started afterwards.
                    Write QA_REPORT.md with actual commands, exit codes, test counts, HTTP results,
                    remaining limitations and an honest final PASS/FAIL status. Report blockers as FAIL.
                    Do not claim successful verification for commands you did not execute.
                    """, cancellationToken);
            }
        }
    }
}