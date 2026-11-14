using SoftwareFactory;
using SoftwareFactory.Core;
using SoftwareFactory.Core.Agents;
using SoftwareFactory.Core.Scenarios;
using SoftwareFactory.Core.Workflow;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var options = FactoryCommandLine.Parse(args);
    if (options.ShowHelp)
    {
        Console.WriteLine("""
            Software Factory — agenci pi.dev
            --show-process             Pokaż definicję pełnego procesu 0.1 (bez uruchamiania).
            --prepare-project          Przygotuj projekt i stan procesu; nie uruchamia agentów ani wdrożeń.
            --goal <tekst>             Cel własnej aplikacji.
            --goal-file <plik>         Wczytaj cel z pliku UTF-8 zamiast --goal/--scenario.
            --scenario ai-agent-store  Demonstracyjny sklep AgentHub; można łączyć z --prepare-project.
            --workspace <ścieżka>      Katalog wynikowy; dla scenariusza musi być pusty lub nie istnieć.
            --dry-run                  Pokaż zlecenie bez uruchamiania Pi i zapisywania plików.
            --check-harness            Sprawdź gotowość Pi bez wysyłania promptu do modelu.
            --help                     Pokaż tę pomoc.
            Bez --prepare-project działa wcześniejszy runner demo Architect → Developer → opcjonalnie QA.
            Domyślny katalog runnera demo: ./workspace4. --prepare-project wymaga jawnego celu.
            """);
        return;
    }

    if (options.ShowProcess)
    {
        Console.WriteLine(WorkflowDefinitionStore.Serialize(WorkflowDefinitionStore.LoadDefault()));
        return;
    }

    bool isStoreScenario = options.ScenarioName == AiAgentStoreScenario.Name;
    string taskDescription = options.Goal
        ?? (options.GoalFile is not null ? await File.ReadAllTextAsync(options.GoalFile, cancellation.Token) : null)
        ?? (isStoreScenario ? AiAgentStoreScenario.Goal : """
        Create a simple ASP.NET Core Web API.
        Requirements:
        - .NET
        - Web Shop application to sell AI agents
        - In-memory storage
        - Unit tests
        Create all files in the current workspace.
        """);
    ArgumentException.ThrowIfNullOrWhiteSpace(taskDescription);
    string workspace = Path.GetFullPath(options.WorkspacePath
        ?? (isStoreScenario ? AiAgentStoreScenario.DefaultWorkspace
            : options.PrepareProject ? "./workspaces/project" : "./workspace4"));
    var workflow = options.PrepareProject ? WorkflowDefinitionStore.LoadDefault() : null;

    Console.WriteLine($"Scenariusz: {options.ScenarioName ?? "domyślny"}");
    Console.WriteLine($"Workspace: {workspace}");
    Console.WriteLine(workflow is null
        ? $"Tryb demo: Architect → Developer{(isStoreScenario ? " → QA" : "")}. To nie jest pełny proces 0.1."
        : $"Przygotowanie procesu {workflow.Id} {workflow.Version}: Biznes → Architektura → Kodowanie → Test → Stage.");
    if (options.DryRun)
    {
        Console.WriteLine("DRY RUN — bez procesów Pi, wywołań modeli i zmian w workspace.");
        Console.WriteLine(taskDescription);
        return;
    }

    if (workflow is not null)
    {
        var prepared = await FactoryProjectPreparer.PrepareAsync(workspace, taskDescription, workflow, cancellation.Token);
        Console.WriteLine($"Przygotowano projekt {prepared.Project.Id}; run {prepared.State.RunId}.");
        Console.WriteLine($"Status: {prepared.State.Status}; pierwszy krok: {prepared.State.CurrentStepId}.");
        Console.WriteLine("Zapisano BRIEF.md, project.json, process/definition.json, process/state.json, process/events.jsonl i pusty backlog.");
        Console.WriteLine("Nie uruchomiono agentów, Git, PR, testów ani wdrożeń. Pełny wykonawca procesu i integracje serwerowe będą dodane osobno.");
        return;
    }

    if (options.CheckHarness)
    {
        await using var agent = AgentFactory.CreateAgent(AgentRole.Developer, workspace);
        await agent.StartHarnessAsync(cancellation.Token);
        Console.WriteLine("Pi harness is ready. No model prompt was sent.");
        return;
    }

    if (File.Exists(Path.Combine(workspace, "process", "state.json")))
        throw new InvalidOperationException("To projekt wersjonowanego procesu. Runner demo nie obsługuje jego wznowienia.");

    if (isStoreScenario)
    {
        if (Directory.Exists(workspace) && Directory.EnumerateFileSystemEntries(workspace).Any())
            throw new IOException("Workspace scenariusza nie jest pusty. Wybierz nowy katalog przez --workspace; istniejące pliki nie zostały zmienione.");
        Directory.CreateDirectory(workspace);
        await File.WriteAllTextAsync(Path.Combine(workspace, "BRIEF.md"), taskDescription, cancellation.Token);
    }

    Console.WriteLine("Uruchamiam agentów. Ten tryb wysyła prompty do modelu i może zużywać płatne tokeny. Ctrl+C anuluje pracę.");
    var factory = new SoftwareSystem();
    await factory.Start(workspace, taskDescription, cancellation.Token, runQualityAssurance: isStoreScenario);

    if (isStoreScenario)
    {
        Console.WriteLine($"Agenci zakończyli pracę. Sprawdź raport: {Path.Combine(workspace, "QA_REPORT.md")}");
        Console.WriteLine("Uruchom wygenerowaną aplikację:");
        Console.WriteLine($"  cd \"{workspace}\"");
        Console.WriteLine("  dotnet run --project src/AiAgentStore.Web --urls http://localhost:5080");
        Console.WriteLine("Adres: http://localhost:5080");
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
    Console.WriteLine("Software Factory stopped.");
    Environment.ExitCode = 130;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Software Factory failed: {exception.Message}");
    Environment.ExitCode = 1;
}
