using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Ml
{
    public sealed class NullMlAdvisor : IMlAdvisor
    {
        public static NullMlAdvisor Instance { get; } = new NullMlAdvisor();

        NullMlAdvisor() { }

        public bool IsEnabled => false;

        public MlAdvisoryResult Advise(BarSnapshot snapshot, EvaluationResult evaluation, UltronConfig config)
            => MlAdvisoryResult.Inactive;
    }
}
