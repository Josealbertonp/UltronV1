namespace UltronV1.Core.Engine
{
    using UltronV1.Core.Signals;
    using UltronV1.Core.Dataset;

    /// <summary>
    /// Ultron V1 configuration — defaults aligned with NightRanger operational philosophy.
    /// </summary>
    public sealed class UltronConfig
    {
        // Hard gates
        public double AdxMin { get; set; } = 20.0;
        public bool RequireTrendAcceleration { get; set; } = true;
        public int AccelLookbackBars { get; set; } = 8;
        public double AccelMinAdxDelta { get; set; } = 2.0;
        public double AccelMinErDelta { get; set; } = 0.0;
        public bool RequireChopFilter { get; set; } = true;
        public int ChopFlipsLookback { get; set; } = 10;
        public int ChopMaxFlips { get; set; } = 2;
        public bool RequireWyckoffFilter { get; set; } = true;
        public int WyckoffRecentBars { get; set; } = 3;
        public int WyckoffOlderBars { get; set; } = 10;
        public bool EnableErFilter { get; set; } = true;
        public int ErPeriod { get; set; } = 10;
        public double ErChopThreshold { get; set; } = 0.12;

        // Archetypes
        public bool EnableFreshBreakout { get; set; } = true;
        public int BreakoutLookbackBars { get; set; } = 5;
        public int EmaCrossLookbackBars { get; set; } = 5;
        public double PullbackToleranceTicks { get; set; } = 3.0;

        // Scoring thresholds
        public int ScoreMinEntry { get; set; } = 55;
        public int FreshBreakoutMinScore { get; set; } = 35;

        // Score weights — trend/structure (NR-aligned)
        public int W_Trend { get; set; } = 18;
        public int W_Pullback { get; set; } = 9;
        public int W_AdxStrength { get; set; } = 10;
        public int W_RsiMomentum { get; set; } = 10;
        public int W_VwapAlign { get; set; } = 8;
        public int W_EmaSpread { get; set; } = 5;
        public int W_Volume { get; set; } = 3;
        public int W_AtrExpansion { get; set; } = 3;
        public int W_FreshBreakout { get; set; } = 12;
        public int W_EmaCrossRecent { get; set; } = 8;
        public int W_MtfTrend { get; set; } = 10;
        public int W_RsiChopPenalty { get; set; } = -15;

        // Order flow weights (Ultron V1 elevation)
        public int W_DeltaAlign { get; set; } = 12;
        public int W_SweepAlign { get; set; } = 10;
        public int W_Absorption { get; set; } = 8;
        public int W_Rvol { get; set; } = 10;
        public int W_BidAskPressure { get; set; } = 6;

        public double RsiOb { get; set; } = 70.0;
        public double RsiOs { get; set; } = 30.0;
        public double EmaSpreadMinTicks { get; set; } = 4.0;
        public double MinRvol { get; set; } = 0.44;
        public bool EnableDiagnosticsLog { get; set; } = true;

        // Tier thresholds (Phase 2)
        public int TierAThreshold { get; set; } = 65;
        public int TierBThreshold { get; set; } = 55;
        public SignalTier MinSignalTier { get; set; } = SignalTier.B;

        // Order flow confirmation per archetype (Phase 2)
        public bool RequireOrderFlowConfirmation { get; set; } = true;
        public bool EnableExplainLog { get; set; } = true;

        // Dataset engine (Phase 3)
        public bool EnableDatasetEngine { get; set; } = false;
        public string ConfigVersion { get; set; } = UltronVersions.DefaultConfigVersion;
        public bool RecordAllBars { get; set; } = true;

        // ML advisory (Phase 4) — boost/penalty only, never veto
        public bool EnableMlAdvisor { get; set; } = false;
        public string MlModelDirectory { get; set; } = string.Empty;

        // Outcome collection (Phase 5) — paper/replay execution, not live capital
        public bool EnableOutcomeCollection { get; set; } = false;
        public int PaperStopLossTicks { get; set; } = 30;
        public int PaperTakeProfitTicks { get; set; } = 32;
        public int PaperMaxBarsInTrade { get; set; } = 48;

        public static UltronConfig Default => new UltronConfig();
    }
}
