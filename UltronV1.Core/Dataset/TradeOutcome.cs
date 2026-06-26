using System;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Dataset
{
    /// <summary>
    /// Trade outcome fields — populated when a position closes (Phase 5+).
    /// Stored separately in outcomes.jsonl, joined on SignalId.
    /// </summary>
    public sealed class TradeOutcome
    {
        public string SignalId { get; set; } = string.Empty;
        public TradeDirection Direction { get; set; }
        public DateTime EntryAtUtc { get; set; }
        public DateTime ClosedAtUtc { get; set; }
        public double EntryPrice { get; set; }
        public double ExitPrice { get; set; }
        public double StopPrice { get; set; }
        public double TargetPrice { get; set; }
        public bool IsWin { get; set; }
        public double RealizedPnLTicks { get; set; }
        public double RealizedPnLUsd { get; set; }
        public double MfeTicks { get; set; }
        public double MaeTicks { get; set; }
        public double RMultiple { get; set; }
        public int BarsInTrade { get; set; }
        public int SecondsInTrade { get; set; }
        public string ExitReason { get; set; } = string.Empty;
        public string ExecutionMode { get; set; } = "PaperBarSim";
        public int Label { get; set; } = -1; // 1=win, 0=loss, -1=unknown
    }
}
