using System;
using UltronV1.Core.Features;
using UltronV1.Core.OrderFlow;

namespace UltronV1.Core.Domain
{
    /// <summary>
    /// Consolidated per-bar view consumed by gates, scoring, ML and audit trail.
    /// Built once per bar close — modules must not recalculate underlying data.
    /// </summary>
    public sealed class BarSnapshot
    {
        public int BarIndex { get; internal set; }
        public DateTime BarTime { get; internal set; }
        public double Open { get; internal set; }
        public double High { get; internal set; }
        public double Low { get; internal set; }
        public double Close { get; internal set; }
        public long BarVolume { get; internal set; }
        public long TicksProcessed { get; internal set; }
        public OrderFlowState OrderFlow { get; internal set; }
        public IndicatorSnapshot Indicators { get; internal set; }
        public FeatureVector Features { get; internal set; } = null!;

        // Archetype flags (derived by FeatureEngine, cached for gates/scoring)
        public bool FreshBreakoutLong { get; internal set; }
        public bool FreshBreakoutShort { get; internal set; }
        public bool PullbackLong { get; internal set; }
        public bool PullbackShort { get; internal set; }
        public bool ContinuationLong { get; internal set; }
        public bool ContinuationShort { get; internal set; }
        public bool RecentCrossLong { get; internal set; }
        public bool RecentCrossShort { get; internal set; }

        // Regime helpers
        public int ChopFlips { get; internal set; }
        public bool WyckoffBlockLong { get; internal set; }
        public bool WyckoffBlockShort { get; internal set; }
        public double EfficiencyRatio { get; internal set; }
        public double EfficiencyRatioPrev { get; internal set; }
        public double Rvol { get; internal set; }
    }
}
