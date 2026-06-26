namespace UltronV1.Core.Signals
{
    public enum TradeDirection : byte
    {
        None = 0,
        Long = 1,
        Short = 2
    }

    public enum EntryArchetype : byte
    {
        None = 0,
        Pullback = 1,
        Breakout = 2,
        Continuation = 3
    }
}
