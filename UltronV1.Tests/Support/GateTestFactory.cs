using UltronV1.Core.Gates;

namespace UltronV1.Tests.Support
{
    internal static class GateTestFactory
    {
        internal static GateEvaluationResult AllowLongOnly()
        {
            var r = new GateEvaluationResult();
            r.LongAllowed = true;
            r.ShortAllowed = false;
            return r;
        }
    }
}
