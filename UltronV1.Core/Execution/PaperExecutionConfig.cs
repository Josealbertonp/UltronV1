namespace UltronV1.Core.Execution
{
    /// <summary>
    /// Fixed paper/replay exit rules — execution only, no entry logic.
    /// Defaults aligned with NightRanger paper validation (30/32 ticks).
    /// </summary>
    public sealed class PaperExecutionConfig
    {
        public int StopLossTicks { get; set; } = 30;
        public int TakeProfitTicks { get; set; } = 32;
        public int MaxBarsInTrade { get; set; } = 48;
        public string ExecutionMode { get; set; } = "PaperBarSim";

        public static PaperExecutionConfig Default => new PaperExecutionConfig();
    }
}
