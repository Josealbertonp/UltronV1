using UltronV1.Core.Domain;
using UltronV1.Core.Gates;
using UltronV1.Core.Scoring;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Engine
{
    /// <summary>
    /// Result of a single bar evaluation pipeline.
    /// </summary>
    public sealed class BarEvaluationResult
    {
        public BarSnapshot Snapshot { get; internal set; } = null!;
        public EvaluationResult Evaluation { get; internal set; } = null!;
        public UltronSignal? Signal => Evaluation?.Signal;
        public SignalExplanation Explanation => Evaluation!.Explanation;
        public GateEvaluationResult Gates => Evaluation!.Gates;
        public ScoreBreakdown Score => Evaluation!.Score;
        public bool ContractValid { get; internal set; }

        /// <summary>Dataset SignalId when dataset engine recorded this bar.</summary>
        public string? LastSignalId { get; internal set; }
    }
}
