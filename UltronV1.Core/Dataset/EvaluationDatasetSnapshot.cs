using UltronV1.Core.Signals;

namespace UltronV1.Core.Dataset
{
    /// <summary>
    /// Canonical dataset fields — populated by SignalEvaluator only (C1).
    /// DatasetRecordBuilder must not infer direction/score from raw evaluation state.
    /// </summary>
    public sealed class EvaluationDatasetSnapshot
    {
        public TradeDirection Direction { get; internal set; }
        public EntryArchetype Archetype { get; internal set; }
        public int PrimaryScore { get; internal set; }
        /// <summary>Side whose score contributions are recorded; None = empty breakdown.</summary>
        public TradeDirection ScoreBreakdownSide { get; internal set; }
    }
}
