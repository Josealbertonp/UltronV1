using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Ml
{
    public interface IMlAdvisor
    {
        bool IsEnabled { get; }
        MlAdvisoryResult Advise(BarSnapshot snapshot, EvaluationResult evaluation, UltronConfig config);
    }
}
