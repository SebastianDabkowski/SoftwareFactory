namespace SoftwareFactory.Core.Agents;

public static class AgentFactory
{
    public static AgentBase CreateAgent(AgentRole agentType, string workspace,
        PiHarnessOptions? harnessOptions = null, PiModelConfig? modelConfig = null)
    {
        var (instruction, thinkingLevel) = agentType switch
        {
            AgentRole.Architect => ("You are an architect. Create the software architecture for the project.", PiModels.Reasoning.High),
            AgentRole.Developer => ("You are a developer. Implement the software architecture.", PiModels.Reasoning.Medium),
            AgentRole.Frontend => ("You are a frontend developer. Implement the frontend of the application.", PiModels.Reasoning.Medium),
            AgentRole.QA => ("You are a QA engineer. Test the application.", PiModels.Reasoning.Low),
            AgentRole.DevOpsEngineer => ("You are a DevOps engineer. Manage deployment and operations.", PiModels.Reasoning.High),
            AgentRole.MaintenanceEngineer => ("You are a maintenance engineer. Maintain the software.", PiModels.Reasoning.High),
            AgentRole.QualityEngineer => ("You are a quality engineer. Ensure the quality of the software.", PiModels.Reasoning.High),
            AgentRole.BusinessAnalyst => ("You are a business analyst. Analyze business goals, research with sources, answer architectural questions and clarify ticket acceptance criteria. Distinguish facts from assumptions; escalate missing owner decisions instead of inventing answers.", PiModels.Reasoning.High),
            AgentRole.Monitoring => ("You are a monitoring agent. Inspect configured health checks, logs, metrics and traces in read-only mode. Report evidence and create issue descriptions. Do not deploy, restart services or change infrastructure. Missing telemetry is a blocker, not a passing result.", PiModels.Reasoning.High),
            _ => throw new ArgumentException($"Unknown agent type: {agentType}", nameof(agentType))
        };
        instruction += $" Your team role id is {(int)agentType}. Share results with other agents through files in the workspace.";
        var config = modelConfig ?? new PiModelConfig(ThinkingLevel: thinkingLevel);
        config.Validate();
        return new AgentBase(workspace, harnessOptions)
        {
            Id = (int)agentType,
            AgentName = agentType.ToString(),
            Instruction = instruction,
            Config = config
        };
    }
}
