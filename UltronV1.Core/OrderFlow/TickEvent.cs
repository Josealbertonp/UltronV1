namespace UltronV1.Core.OrderFlow
{
    /// <summary>
    /// Immutable tick payload passed from the NT8 adapter into the hot path.
    /// Pre-compute TickIndex in the adapter to avoid repeated division on the engine side.
    /// </summary>
    public readonly struct TickEvent
    {
        public long TimestampUtcTicks { get; }
        public double Price { get; }
        public long Volume { get; }
        public double Bid { get; }
        public double Ask { get; }
        public int TickIndex { get; }

        public TickEvent(long timestampUtcTicks, double price, long volume, double bid, double ask, int tickIndex)
        {
            TimestampUtcTicks = timestampUtcTicks;
            Price = price;
            Volume = volume;
            Bid = bid;
            Ask = ask;
            TickIndex = tickIndex;
        }
    }
}
