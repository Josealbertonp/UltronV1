using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using UltronV1.Core.Domain;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Standard Ultron indicator set + snapshot builder (Indicator and Paper).</summary>
    public sealed class UltronIndicatorBundle
    {
        public EMA EmaFast { get; private set; }
        public EMA EmaSlow { get; private set; }
        public EMA EmaMtf { get; private set; }
        public ADX Adx { get; private set; }
        public ATR Atr { get; private set; }
        public RSI Rsi { get; private set; }
        public DonchianChannel Donchian { get; private set; }
        public SMA VolSma { get; private set; }

        public void Create(NinjaScriptBase script)
        {
            EmaFast = script.EMA(script.Close, 9);
            EmaSlow = script.EMA(script.Close, 21);
            Adx = script.ADX(script.Close, 14);
            Atr = script.ATR(script.Close, 14);
            Rsi = script.RSI(script.Close, 14, 3);
            Donchian = script.DonchianChannel(script.Close, 20);
            VolSma = script.SMA(script.Volume, 20);
            EmaMtf = script.EMA(script.Closes[1], 21);
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
                Donchian.IsValidDataPoint(1) ? Donchian.Upper[1] : Donchian.Upper[0],
                Donchian.IsValidDataPoint(1) ? Donchian.Lower[1] : Donchian.Lower[0],
                VolSma.IsValidDataPoint(0) ? VolSma[0] : barVolume,
                EmaMtf.IsValidDataPoint(0) ? EmaMtf[0] : EmaFast[0],
                EmaMtf.IsValidDataPoint(2) ? EmaMtf[2] : EmaMtf[0],
                true);
        }
    }
}
