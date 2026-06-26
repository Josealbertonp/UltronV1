using System;
using UltronV1.Core.OrderFlow;

namespace UltronV1.Core.Domain
{
    /// <summary>
    /// Immutable snapshot of market state at bar evaluation time.
    /// Phase 0: bar OHLCV + order flow only; gates/scoring added in Phase 1.
    /// </summary>
    public readonly struct MarketContext
    {
        public int BarIndex { get; }
        public DateTime BarTime { get; }
        public double Open { get; }
        public double High { get; }
        public double Low { get; }
        public double Close { get; }
        public long BarVolume { get; }
        public OrderFlowState OrderFlow { get; }

        public MarketContext(
            int barIndex,
            DateTime barTime,
            double open,
            double high,
            double low,
            double close,
            long barVolume,
            OrderFlowState orderFlow)
        {
            BarIndex = barIndex;
            BarTime = barTime;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            BarVolume = barVolume;
            OrderFlow = orderFlow;
        }
    }
}
