using System.Text.Json;
using System.Text.Json.Serialization;
using SoftwareFactory.Core.Agents;

namespace SoftwareFactory.Core.Workflow;

public static class WorkflowDefinitionStore
{
    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public static WorkflowDefinition LoadDefault()
    {
        using var stream = typeof(WorkflowDefinitionStore).Assembly.GetManifestResourceStream(
            "SoftwareFactory.Core.Workflow.Definitions.software-factory.v0.1.json")
            ?? throw new InvalidOperationException("Embedded Software Factory workflow is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public static WorkflowDefinition Parse(string json)
    {
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOptions)
            ?? throw new InvalidDataException("Workflow definition is empty.");
        Validate(definition);
        return definition;
    }

    public static string Serialize(WorkflowDefinition definition)
    {
        Validate(definition);
        return JsonSerializer.Serialize(definition, JsonOptions);
    }

    public static void Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.Name)
            || !Version.TryParse(definition.Version, out _) || definition.Policy is null
            || definition.Stages is not { Count: > 0 })
            throw new InvalidDataException("Workflow needs an id, name, numeric version, policy and stages.");
        if (definition.Policy.MaxClarificationRounds < 1 || definition.Policy.MaxTicketReworkCycles < 1
            || definition.Policy.MaxReleaseCycles < 1 || string.IsNullOrWhiteSpace(definition.Policy.IntegrationBranch)
            || string.IsNullOrWhiteSpace(definition.Policy.TicketBranchPattern)
            || !definition.Policy.RequireEvidenceForPass || !definition.Policy.BlockOnUnansweredBusinessQuestions)
            throw new InvalidDataException("Workflow must bound retry loops and require evidence and business clarification.");

        var stageIds = new HashSet<string>(StringComparer.Ordinal);
        var steps = new Dictionary<string, WorkflowStep>(StringComparer.Ordinal);
        foreach (var stage in definition.Stages)
        {
            if (stage is null || string.IsNullOrWhiteSpace(stage.Id) || !stageIds.Add(stage.Id)
                || string.IsNullOrWhiteSpace(stage.Name) || stage.Steps is not { Count: > 0 })
                throw new InvalidDataException("Workflow stage ids must be unique; stages must have a name and steps.");
            foreach (var step in stage.Steps)
            {
                if (step is null || string.IsNullOrWhiteSpace(step.Id) || WorkflowTerminals.IsTerminal(step.Id)
                    || !steps.TryAdd(step.Id, step) || string.IsNullOrWhiteSpace(step.Name)
                    || string.IsNullOrWhiteSpace(step.Instructions) || !Enum.IsDefined(step.Role) || step.Role == AgentRole.None
                    || step.Transitions is not { Count: > 0 } || step.Inputs is null || step.Outputs is null)
                    throw new InvalidDataException("Workflow steps need unique ids, a role, instructions, artifacts and transitions.");
                if (step.Inputs.Concat(step.Outputs).Any(path => string.IsNullOrWhiteSpace(path)
                    || Path.IsPathRooted(path) || path.Contains(':') || path.Replace('\\', '/').Split('/').Contains("..")))
                    throw new InvalidDataException($"Artifacts for {step.Id} must stay inside the project workspace.");
            }
        }
        if (string.IsNullOrWhiteSpace(definition.EntryStepId) || !steps.ContainsKey(definition.EntryStepId))
            throw new InvalidDataException("Workflow entry step does not exist.");
        foreach (var step in steps.Values)
        {
            var outcomes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var transition in step.Transitions)
            {
                if (transition is null || string.IsNullOrWhiteSpace(transition.Outcome) || !outcomes.Add(transition.Outcome)
                    || string.IsNullOrWhiteSpace(transition.Target)
                    || (!WorkflowTerminals.IsTerminal(transition.Target) && !steps.ContainsKey(transition.Target)))
                    throw new InvalidDataException($"Invalid or duplicate outcome/target for step {step.Id}.");
            }
        }

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(definition.EntryStepId);
        while (pending.TryDequeue(out string? id))
        {
            if (WorkflowTerminals.IsTerminal(id) || !reachable.Add(id)) continue;
            foreach (var transition in steps[id].Transitions) pending.Enqueue(transition.Target);
        }
        if (reachable.Count != steps.Count)
            throw new InvalidDataException("Workflow contains steps unreachable from its entry point.");

        var canFinish = steps.Values.Where(step => step.Transitions.Any(t => WorkflowTerminals.IsTerminal(t.Target)))
            .Select(step => step.Id).ToHashSet(StringComparer.Ordinal);
        bool changed;
        do
        {
            changed = false;
            foreach (var step in steps.Values)
                if (step.Transitions.Any(t => canFinish.Contains(t.Target))) changed |= canFinish.Add(step.Id);
        } while (changed);
        if (canFinish.Count != steps.Count)
            throw new InvalidDataException("Workflow contains a loop with no path to a terminal state.");
        if (!steps.Values.Any(step => step.Transitions.Any(t => t.Target == WorkflowTerminals.Completed)))
            throw new InvalidDataException("Workflow needs an explicit completion outcome.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
