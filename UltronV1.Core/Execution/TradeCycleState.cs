using System;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Execution
{
    /// <summary>
    /// Open paper trade state — MFE/MAE tracked bar-by-bar until exit.
    /// </summary>
    public sealed class TradeCycleState
    {
        public string SignalId { get; internal set; } = string.Empty;
        public TradeDirection Direction { get; internal set; }
        public double EntryPrice { get; internal set; }
        public double StopPrice { get; internal set; }
        public double TargetPrice { get; internal set; }
        public int RiskTicks { get; internal set; }
        public DateTime EntryAtUtc { get; internal set; }
        public int EntryBarIndex { get; internal set; }
        public double MfeTicks { get; internal set; }
        public double MaeTicks { get; internal set; }
        public string ExecutionMode { get; internal set; } = "PaperBarSim";
    }
}
