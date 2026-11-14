using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareFactory.Core.Agents
{
    public static class PiModels
    {
        public static class Models
        {
            public const string Gpt61Sol = "gpt-6.1-sol";
            public const string Gpt6Sol = "gpt-6-sol";
            public const string Gpt6Luna = "gpt-6-luna";
            public const string Gpt6Astra = "gpt-6-astra";

            public const string Gpt56 = "gpt-5.6";
            public const string Gpt56Terra = "gpt-5.6-terra";
            public const string Gpt56Luna = "gpt-5.6-luna";
        }

        public static class Reasoning
        {
            public const string Off = "off";
            public const string Minimal = "minimal";
            public const string Low = "low";
            public const string Medium = "medium";
            public const string High = "high";
            public const string ExtraHigh = "xhigh";
            public const string Max = "max";
        }

       
    }

    public sealed record PiModelConfig(
        string Model = PiModels.Models.Gpt61Sol,
        string ThinkingLevel = PiModels.Reasoning.Medium,
        string Provider = "openai")
    {
        internal void Validate()
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Model);
            ArgumentException.ThrowIfNullOrWhiteSpace(Provider);
            if (ThinkingLevel is not ("off" or "minimal" or "low" or "medium" or "high" or "xhigh" or "max"))
                throw new ArgumentException($"Unsupported Pi thinking level: {ThinkingLevel}", nameof(ThinkingLevel));
            // Model availability is checked by Pi, including custom providers/models.
        }
    }
}
