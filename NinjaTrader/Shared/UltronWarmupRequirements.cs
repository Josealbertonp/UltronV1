namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>
    /// MTF readiness before <see cref="UltronIndicatorBundle.BuildSnapshot"/> (EmaMtf index access).
    /// </summary>
    public static class UltronWarmupRequirements
    {
        /// <summary>
        /// True when <c>CurrentBars[1]</c> allows safe reads of
        /// <c>EmaMtf[0]</c> and <c>EmaMtf[<see cref="UltronIndicatorBundle.MtfEmaMaxBarsAgo"/>]</c>.
        /// </summary>
        public static bool IsMtfSeriesReady(int mtfCurrentBar) =>
            mtfCurrentBar >= UltronIndicatorBundle.MtfEmaMaxBarsAgo;
    }
}
