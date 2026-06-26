namespace UltronV1.Core.Mnq
{
    /// <summary>
    /// Compile-time constants for MNQ 09-26. V1 is MNQ-only; expansion to other
    /// instruments is a future concern and must not affect these values.
    /// </summary>
    public static class MnqContractSpec
    {
        public const string Symbol = "MNQ 09-26";
        public const string SymbolRoot = "MNQ";
        public const double TickSize = 0.25;
        public const double TickValueUsd = 0.50;
        public const int PrimaryBarPeriodMinutes = 5;
        public const int MtfBarPeriodMinutes = 15;

        /// <summary>Footprint window: ±400 points from session anchor (~1600 tick slots).</summary>
        public const int FootprintTickSlots = 1600;

        /// <summary>Center index inside the footprint ring buffer.</summary>
        public const int FootprintCenterIndex = FootprintTickSlots / 2;

        /// <summary>Sweep: minimum tick displacement within the time window.</summary>
        public const int SweepMinTicks = 4;

        /// <summary>Sweep: maximum elapsed time (ms) for displacement to qualify.</summary>
        public const int SweepMaxElapsedMs = 500;

        /// <summary>Absorption: minimum bar volume (contracts) to evaluate.</summary>
        public const long AbsorptionMinVolume = 50;

        /// <summary>Absorption: max bar range in ticks for "stagnant price" condition.</summary>
        public const int AbsorptionMaxRangeTicks = 3;

        /// <summary>Iceberg: minimum refill events at the same tick level.</summary>
        public const int IcebergMinRefills = 3;

        public static int PriceToTickIndex(double price)
        {
            return (int)System.Math.Round(price / TickSize, System.MidpointRounding.AwayFromZero);
        }

        public static double TickIndexToPrice(int tickIndex)
        {
            return tickIndex * TickSize;
        }
    }
}
