using System.Collections.Generic;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Gates
{
    public readonly struct GateCheckResult
    {
        public string Name { get; }
        public bool LongPass { get; }
        public bool ShortPass { get; }
        public string Detail { get; }

        public GateCheckResult(string name, bool longPass, bool shortPass, string detail)
        {
            Name = name;
            LongPass = longPass;
            ShortPass = shortPass;
            Detail = detail;
        }
    }

    public sealed class GateEvaluationResult
    {
        public bool LongAllowed { get; internal set; }
        public bool ShortAllowed { get; internal set; }
        public string BlockReason { get; internal set; } = string.Empty;
        public List<GateCheckResult> Checks { get; } = new List<GateCheckResult>(8);
    }
}
