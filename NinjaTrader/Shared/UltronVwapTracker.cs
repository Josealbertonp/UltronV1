namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Session VWAP accumulator — reset policy remains host-specific.</summary>
    public sealed class UltronVwapTracker
    {
        double _numerator;
        double _denominator;
        int _barCount;

        public int BarCount => _barCount;
        public bool HasVolume => _denominator > 0;

        public double Vwap => _denominator > 0 ? _numerator / _denominator : 0;

        public void Reset()
        {
            _numerator = 0;
            _denominator = 0;
            _barCount = 0;
        }

        public void UpdateBar(double high, double low, double close, double volume, bool treatZeroVolumeAsOne)
        {
            double typical = (high + low + close) / 3.0;
            double vol = treatZeroVolumeAsOne
                ? (volume > 0 ? volume : 1)
                : volume;

            _numerator += typical * vol;
            _denominator += vol;
            _barCount++;
        }
    }
}
