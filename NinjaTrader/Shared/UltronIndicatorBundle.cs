using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using global::UltronV1.Core.Domain;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Standard Ultron indicator set + snapshot builder (Indicator and Paper).</summary>
    public sealed class UltronIndicatorBundle
    {
        public const int EmaFastPeriod = 9;
        public const int EmaSlowPeriod = 21;
        public const int AdxPeriod = 14;
        public const int AtrPeriod = 14;
        public const int RsiPeriod = 14;
        public const int RsiSmooth = 3;
        public const int DonchianPeriod = 20;
        public const int VolSmaPeriod = 20;
        public const int DonchianMaxBarsAgo = 1;

        /// <summary>MTF EMA on Closes[1]; deepest barsAgo read in BuildSnapshot.</summary>
        public const int MtfEmaPeriod = 21;
        public const int MtfEmaMaxBarsAgo = 2;

        public EMA EmaFast { get; private set; }
        public EMA EmaSlow { get; private set; }
        public EMA EmaMtf { get; private set; }
        public ADX Adx { get; private set; }
        public ATR Atr { get; private set; }
        public RSI Rsi { get; private set; }
        public DonchianChannel Donchian { get; private set; }
        public SMA VolSma { get; private set; }

        public void Create(Indicator script)
        {
            EmaFast = script.EMA(script.Close, EmaFastPeriod);
            EmaSlow = script.EMA(script.Close, EmaSlowPeriod);
            Adx = script.ADX(script.Close, AdxPeriod);
            Atr = script.ATR(script.Close, AtrPeriod);
            Rsi = script.RSI(script.Close, RsiPeriod, RsiSmooth);
            Donchian = script.DonchianChannel(script.Close, DonchianPeriod);
            VolSma = script.SMA(script.Volume, VolSmaPeriod);
            EmaMtf = script.EMA(script.Closes[1], MtfEmaPeriod);
        }

        public void Create(Strategy script)
        {
            EmaFast = script.EMA(script.Close, EmaFastPeriod);
            EmaSlow = script.EMA(script.Close, EmaSlowPeriod);
            Adx = script.ADX(script.Close, AdxPeriod);
            Atr = script.ATR(script.Close, AtrPeriod);
            Rsi = script.RSI(script.Close, RsiPeriod, RsiSmooth);
            Donchian = script.DonchianChannel(script.Close, DonchianPeriod);
            VolSma = script.SMA(script.Volume, VolSmaPeriod);
            EmaMtf = script.EMA(script.Closes[1], MtfEmaPeriod);
        }

        public IndicatorSnapshot BuildSnapshot(
            UltronVwapTracker vwap,
            double closeFallback,
            double barVolume,
            int accelLookbackBars)
        {
            int accelBars = accelLookbackBars;
            double adxPrev = Adx.IsValidDataPoint(accelBars) ? Adx[accelBars] : Adx[0];

            return new IndicatorSnapshot(
                EmaFast[0], EmaSlow[0], Adx[0], adxPrev, Rsi[0], Atr[0],
                vwap.HasVolume ? vwap.Vwap : closeFallback,
                vwap.BarCount,
                Donchian.Upper[0], Donchian.Lower[0],
                Donchian.IsValidDataPoint(DonchianMaxBarsAgo) ? Donchian.Upper[DonchianMaxBarsAgo] : Donchian.Upper[0],
                Donchian.IsValidDataPoint(DonchianMaxBarsAgo) ? Donchian.Lower[DonchianMaxBarsAgo] : Donchian.Lower[0],
                VolSma.IsValidDataPoint(0) ? VolSma[0] : barVolume,
                EmaMtf[0],
                EmaMtf[MtfEmaMaxBarsAgo],
                true);
        }
    }
}
