namespace SoftwareFactory.Core.Scenarios;

public static class AiAgentStoreScenario
{
    public const string Name = "ai-agent-store";
    public const string DefaultWorkspace = "./workspaces/ai-agent-store";

    public static string Goal { get; } = LoadGoal();

    private static string LoadGoal()
    {
        using var stream = typeof(AiAgentStoreScenario).Assembly.GetManifestResourceStream(
            "SoftwareFactory.Core.Scenarios.ai-agent-store.md")
            ?? throw new InvalidOperationException("The AI Agent Store scenario resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
