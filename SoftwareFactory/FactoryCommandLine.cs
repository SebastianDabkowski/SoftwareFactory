using SoftwareFactory.Core.Scenarios;

namespace SoftwareFactory;

public sealed record FactoryCommandLine(
    string? ScenarioName, string? WorkspacePath, bool DryRun, bool CheckHarness, bool ShowHelp,
    string? Goal = null, string? GoalFile = null, bool ShowProcess = false, bool PrepareProject = false)
{
    public static FactoryCommandLine Parse(string[] args)
    {
        string? scenario = null;
        string? workspace = null;
        string? goal = null, goalFile = null;
        bool dryRun = false, checkHarness = false, showHelp = false, showProcess = false, prepareProject = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (!seen.Add(argument))
                throw new ArgumentException($"Powtórzony argument: {argument}");
            switch (argument)
            {
                case "--scenario":
                    scenario = ReadValue(args, ref index);
                    if (scenario != AiAgentStoreScenario.Name)
                        throw new ArgumentException($"Nieznany scenariusz: {scenario}. Dostępny: {AiAgentStoreScenario.Name}.");
                    break;
                case "--workspace":
                    workspace = ReadValue(args, ref index);
                    break;
                case "--goal": goal = ReadValue(args, ref index); break;
                case "--goal-file": goalFile = ReadValue(args, ref index); break;
                case "--show-process": showProcess = true; break;
                case "--prepare-project": prepareProject = true; break;
                case "--dry-run": dryRun = true; break;
                case "--check-harness": checkHarness = true; break;
                case "--help": showHelp = true; break;
                default: throw new ArgumentException($"Nieznany argument: {argument}. Użyj --help.");
            }
        }
        if (dryRun && checkHarness)
            throw new ArgumentException("Wybierz --dry-run albo --check-harness; to osobne tryby.");
        if (new[] { scenario, goal, goalFile }.Count(value => value is not null) > 1)
            throw new ArgumentException("Wybierz jedno źródło celu: --scenario, --goal albo --goal-file.");
        if (prepareProject && checkHarness)
            throw new ArgumentException("Przygotowanie projektu nie uruchamia harnessu; nie łącz z --check-harness.");
        if (prepareProject && scenario is null && goal is null && goalFile is null)
            throw new ArgumentException("Podaj cel projektu przez --goal, --goal-file albo --scenario.");
        if (showProcess && (prepareProject || dryRun || checkHarness || scenario is not null || goal is not null
            || goalFile is not null || workspace is not null))
            throw new ArgumentException("--show-process jest osobnym trybem podglądu definicji.");
        return new FactoryCommandLine(scenario, workspace, dryRun, checkHarness, showHelp,
            goal, goalFile, showProcess, prepareProject);
    }

    private static string ReadValue(string[] args, ref int index)
    {
        string option = args[index];
        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Brak wartości dla {option}.");
        return args[index];
    }
}
