using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareFactory.Core.Agents
{
    public static class CodexModels
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
            public const string Minimal = "minimal";
            public const string Low = "low";
            public const string Medium = "medium";
            public const string High = "high";
            public const string ExtraHigh = "xhigh";
            public const string Max = "max";
        }

       
    }

    public sealed record CodexModelConfig(string model, string reasoningEffort);
}
