namespace UltronV1.Core.Features
{
    /// <summary>
    /// Single source of truth for Ultron feature indices and names (ultron_mnq_v1).
    /// FeatureVector, Dataset Engine, Python Training/Validation, ML manifests, and ONNX input order
    /// must all align with this definition. After edits, run tools/ExportFeatureSchema.
    /// Version bumps require retraining.
    /// </summary>
    public static class FeatureSchema
    {
        public const int Version = 1;
        public const int Count = 40;

        // Order flow (0-7)
        public const int Delta = 0;
        public const int BuyVolume = 1;
        public const int SellVolume = 2;
        public const int BidAskPressure = 3;
        public const int SweepDetected = 4;
        public const int SweepTicks = 5;
        public const int AbsorptionDetected = 6;
        public const int IcebergRefills = 7;

        // Volume / volatility (8-12)
        public const int Rvol = 8;
        public const int Atr = 9;
        public const int AtrTicks = 10;
        public const int Adx = 11;
        public const int Er = 12;

        // Momentum / trend (13-20)
        public const int Rsi = 13;
        public const int VwapDistanceTicks = 14;
        public const int PriceAboveVwap = 15;
        public const int EmaSpreadTicks = 16;
        public const int PriceAboveEmaFast = 17;
        public const int TrendAlignedLong = 18;
        public const int TrendAlignedShort = 19;
        public const int VolRatio = 20;

        // Structure / archetypes (21-31)
        public const int DonchianUpper = 21;
        public const int DonchianLower = 22;
        public const int FreshBreakoutLong = 23;
        public const int FreshBreakoutShort = 24;
        public const int PullbackLong = 25;
        public const int PullbackShort = 26;
        public const int ContinuationLong = 27;
        public const int ContinuationShort = 28;
        public const int RecentCrossLong = 29;
        public const int RecentCrossShort = 30;
        public const int ChopFlips = 31;

        // Regime flags (32-39)
        public const int WyckoffBlockLong = 32;
        public const int WyckoffBlockShort = 33;
        public const int AdxRising = 34;
        public const int ErRising = 35;
        public const int AdxStrong = 36;
        public const int MtfEmaRising = 37;
        public const int BarRangeTicks = 38;
        public const int TickCount = 39;

        public static readonly string[] FeatureNames =
        {
            "Delta", "BuyVolume", "SellVolume", "BidAskPressure", "SweepDetected", "SweepTicks",
            "AbsorptionDetected", "IcebergRefills", "Rvol", "Atr", "AtrTicks", "Adx", "Er",
            "Rsi", "VwapDistanceTicks", "PriceAboveVwap", "EmaSpreadTicks", "PriceAboveEmaFast",
            "TrendAlignedLong", "TrendAlignedShort", "VolRatio", "DonchianUpper", "DonchianLower",
            "FreshBreakoutLong", "FreshBreakoutShort", "PullbackLong", "PullbackShort",
            "ContinuationLong", "ContinuationShort", "RecentCrossLong", "RecentCrossShort",
            "ChopFlips", "WyckoffBlockLong", "WyckoffBlockShort", "AdxRising", "ErRising",
            "AdxStrong", "MtfEmaRising", "BarRangeTicks", "TickCount"
        };
    }
}
