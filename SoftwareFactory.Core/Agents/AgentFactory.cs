using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareFactory.Core.Agents
{
    public class AgentFactory
    {
        private static string _basePrompt = "In your team you are working with other agents. " +
            "Your id is {0}. You can communicate with other agents to achieve your goal. " +
            "You will use the following id to communicate with other agents: {1}";
        private static string _baseAgent = string.Join(", ", Enum.GetValues<AgentRole>().Select(x => $"{x} = {(int)x}"));
        public static AgentBase CreateAgent(AgentRole agentType, string workspace)
        {
            return agentType switch
            {
                AgentRole.Architect => CreateArchitect(workspace),
                AgentRole.Developer => CreateDeveloper(workspace),
                AgentRole.Frontend => CreateFrontend(workspace),
                AgentRole.QA => CreateQA(workspace),
                AgentRole.DevOpsEngineer => CreateDevOpsEngineer(workspace),
                AgentRole.MaintenanceEngineer => CreateMaintenanceEngineer(workspace),
                AgentRole.QualityEngineer => CreateQualityEngineer(workspace),
                AgentRole.BusinessAnalyst => CreateBusinessAnalyst(workspace),
                _ => throw new ArgumentException($"Unknown agent type: {agentType}")
            };
        }

        private static AgentBase CreateArchitect(string workspace)
        {
            string instruction = "You are an architect. You will create a software architecture for the project.";
            instruction += string.Format(" " + _basePrompt, (int)AgentRole.Architect, _baseAgent);
            return new AgentBase(workspace)
            {
                Id = (int)AgentRole.Architect,
                AgentName = "Architect",
                Instruction = instruction,
                Config = new CodexModelConfig(CodexModels.Models.Gpt61Sol, CodexModels.Reasoning.High)
            };
        }

        private static AgentBase CreateBusinessAnalyst(string workspace)
        {
            return new AgentBase(workspace)
            {
                Id = (int)AgentRole.BusinessAnalyst,
                AgentName = "BusinessAnalyst",
                Instruction = "You are a business analyst. You will analyze the business requirements.",
                Config = new CodexModelConfig(CodexModels.Models.Gpt61Sol, CodexModels.Reasoning.High)
            };
        }

        private static AgentBase CreateQualityEngineer(string workspace)
        {
            return new AgentBase(workspace)
            {
                Id = (int)AgentRole.QualityEngineer,
                AgentName = "QualityEngineer",
                Instruction = "You are a quality engineer. You will ensure the quality of the software.",
                Config = new CodexModelConfig(CodexModels.Models.Gpt61Sol, CodexModels.Reasoning.High)
            };
        }

        private static AgentBase CreateMaintenanceEngineer(string workspace)
        {
            return new AgentBase(workspace)
            {
                Id = (int)AgentRole.MaintenanceEngineer,
                AgentName = "MaintenanceEngineer",
                Instruction = "You are a maintenance engineer. You will maintain the software.",
                Config = new CodexModelConfig(CodexModels.Models.Gpt61Sol, CodexModels.Reasoning.High)
            };
        }

        private static AgentBase CreateDevOpsEngineer(string workspace)
        {
            return new AgentBase(workspace)
            {
                Id = (int)AgentRole.DevOpsEngineer,
                AgentName = "DevOpsEngineer",
                Instruction = "You are a DevOps engineer. You will manage the deployment and operations of the software.",
                Config = new CodexModelConfig(CodexModels.Models.Gpt61Sol, CodexModels.Reasoning.High)
            };
        }


        private static AgentBase CreateDeveloper(string workspace)
        {
            return new AgentBase(workspace)
            {
                Id = (int)AgentRole.Developer,
                AgentName = "Developer",
                Instruction = "You are a developer. You will implement the software architecture.",
                Config = new CodexModelConfig(CodexModels.Models.Gpt61Sol, CodexModels.Reasoning.Medium)
            };
        }

        private static AgentBase CreateFrontend(string workspace)
        {
            return new AgentBase(workspace)
            {
                Id = (int)AgentRole.Frontend,
                AgentName = "Frontend",
                Instruction = "You are a frontend developer. You will implement the frontend of the application.",
                Config = new CodexModelConfig(CodexModels.Models.Gpt61Sol, CodexModels.Reasoning.Medium)
            };
        }

        private static AgentBase CreateQA(string workspace)
        {
            return new AgentBase(workspace)
            {
                Id = (int)AgentRole.QA,
                AgentName = "QA",
                Instruction = "You are a QA engineer. You will test the application.",
                Config = new CodexModelConfig(CodexModels.Models.Gpt61Sol, CodexModels.Reasoning.Low)
            };
        }
    }
}
