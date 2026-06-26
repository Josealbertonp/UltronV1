namespace UltronV1.Core.Domain
{
    /// <summary>
    /// Indicator values supplied by the NT8 host at bar close. Core never references NinjaTrader types.
    /// </summary>
    public readonly struct IndicatorSnapshot
    {
        public double EmaFast { get; }
        public double EmaSlow { get; }
        public double Adx { get; }
        public double AdxBarsAgo { get; }
        public double Rsi { get; }
        public double Atr { get; }
        public double Vwap { get; }
        public int VwapBarCount { get; }
        public double DonchianUpper { get; }
        public double DonchianLower { get; }
        public double DonchianUpperPrev { get; }
        public double DonchianLowerPrev { get; }
        public double VolSma { get; }
        public double EmaMtf { get; }
        public double EmaMtfPrev { get; }
        public bool IsValid { get; }

        public IndicatorSnapshot(
            double emaFast,
            double emaSlow,
            double adx,
            double adxBarsAgo,
            double rsi,
            double atr,
            double vwap,
            int vwapBarCount,
            double donchianUpper,
            double donchianLower,
            double donchianUpperPrev,
            double donchianLowerPrev,
            double volSma,
            double emaMtf,
            double emaMtfPrev,
            bool isValid)
        {
            EmaFast = emaFast;
            EmaSlow = emaSlow;
            Adx = adx;
            AdxBarsAgo = adxBarsAgo;
            Rsi = rsi;
            Atr = atr;
            Vwap = vwap;
            VwapBarCount = vwapBarCount;
            DonchianUpper = donchianUpper;
            DonchianLower = donchianLower;
            DonchianUpperPrev = donchianUpperPrev;
            DonchianLowerPrev = donchianLowerPrev;
            VolSma = volSma;
            EmaMtf = emaMtf;
            EmaMtfPrev = emaMtfPrev;
            IsValid = isValid;
        }

        public static IndicatorSnapshot Empty => default;
    }
}
