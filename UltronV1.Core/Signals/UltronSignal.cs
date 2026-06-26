namespace UltronV1.Core.Signals
{
    public sealed class UltronSignal
    {
        public TradeDirection Direction { get; internal set; }
        public EntryArchetype Archetype { get; internal set; }
        public int Score { get; internal set; }
        public SignalTier Tier { get; internal set; }
        public bool IsValid { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
    }
}
