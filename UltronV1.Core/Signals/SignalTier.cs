namespace UltronV1.Core.Signals
{
    public enum SignalTier : byte
    {
        None = 0,
        C = 1,
        B = 2,
        A = 3
    }

    public enum MarketRegime : byte
    {
        Unknown = 0,
        Trending = 1,
        Chop = 2,
        Quiet = 3
    }

    public enum RejectionStage : byte
    {
        None = 0,
        HardGate = 1,
        Score = 2,
        OrderFlow = 3
    }
}
