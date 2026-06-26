namespace UltronV1.Core.OrderFlow
{
    /// <summary>
    /// Read-only snapshot of order-flow metrics for the current (or last closed) bar.
    /// Struct to avoid heap allocations on the evaluation path.
    /// </summary>
    public readonly struct OrderFlowState
    {
        public long BuyVolume { get; }
        public long SellVolume { get; }
        public long Delta { get; }
        public int TickCount { get; }
        public double BidAskPressureRatio { get; }
        public bool SweepDetected { get; }
        public TickSide SweepDirection { get; }
        public int SweepTicks { get; }
        public bool AbsorptionDetected { get; }
        public TickSide AbsorptionSide { get; }
        public int IcebergRefillCount { get; }
        public int BarHighTickIndex { get; }
        public int BarLowTickIndex { get; }

        public OrderFlowState(
            long buyVolume,
            long sellVolume,
            long delta,
            int tickCount,
            double bidAskPressureRatio,
            bool sweepDetected,
            TickSide sweepDirection,
            int sweepTicks,
            bool absorptionDetected,
            TickSide absorptionSide,
            int icebergRefillCount,
            int barHighTickIndex,
            int barLowTickIndex)
        {
            BuyVolume = buyVolume;
            SellVolume = sellVolume;
            Delta = delta;
            TickCount = tickCount;
            BidAskPressureRatio = bidAskPressureRatio;
            SweepDetected = sweepDetected;
            SweepDirection = sweepDirection;
            SweepTicks = sweepTicks;
            AbsorptionDetected = absorptionDetected;
            AbsorptionSide = absorptionSide;
            IcebergRefillCount = icebergRefillCount;
            BarHighTickIndex = barHighTickIndex;
            BarLowTickIndex = barLowTickIndex;
        }

        public static OrderFlowState Empty =>
            new OrderFlowState(0, 0, 0, 0, 1.0, false, TickSide.Unknown, 0, false, TickSide.Unknown, 0, 0, 0);
    }
}
