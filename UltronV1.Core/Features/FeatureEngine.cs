using System;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;

namespace UltronV1.Core.Features
{
    /// <summary>
    /// Centralizes all feature computation into a single FeatureVector per bar.
    /// </summary>
    public sealed class FeatureEngine
    {
        readonly BarHistoryBuffer _history = new BarHistoryBuffer();
        readonly long[] _volumeWindow = new long[100];
        readonly long[] _rvolSortScratch = new long[100];
        int _volumeHead;
        int _volumeCount;

        readonly FeatureVector _vector = new FeatureVector();
        readonly BarSnapshot _snapshot = new BarSnapshot();

        public FeatureVector Vector => _vector;

        public BarSnapshot BuildSnapshot(
            int barIndex,
            DateTime barTime,
            double open,
            double high,
            double low,
            double close,
            long barVolume,
            long ticksProcessed,
            in OrderFlowState orderFlow,
            in IndicatorSnapshot indicators,
            UltronConfig config)
        {
            _vector.Clear();

            double tick = MnqContractSpec.TickSize;
            double er = ComputeEfficiencyRatio(close, config.ErPeriod);
            double erPrev = ComputeEfficiencyRatioBarsAgo(config.AccelLookbackBars, config.ErPeriod);
            double rvol = ComputeRvol(barVolume);

            bool freshLong = IsFreshDonchianBreak(true, high, low, indicators, config.BreakoutLookbackBars);
            bool freshShort = IsFreshDonchianBreak(false, high, low, indicators, config.BreakoutLookbackBars);

            double pullTol = config.PullbackToleranceTicks * tick;
            bool pullLong = low <= indicators.EmaFast + pullTol && close >= indicators.EmaFast;
            bool pullShort = high >= indicators.EmaFast - pullTol && close <= indicators.EmaFast;
            bool contLong = close > indicators.EmaFast && indicators.EmaFast > indicators.EmaSlow;
            bool contShort = close < indicators.EmaFast && indicators.EmaFast < indicators.EmaSlow;
            bool crossLong = RecentEmaCrossCurrent(indicators, true, config.EmaCrossLookbackBars);
            bool crossShort = RecentEmaCrossCurrent(indicators, false, config.EmaCrossLookbackBars);

            int chopFlips = ComputeChopFlips(indicators, config.ChopFlipsLookback);
            WyckoffState wyckoff = ComputeWyckoff(close, high, low, config.WyckoffRecentBars, config.WyckoffOlderBars);

            WriteFeatures(orderFlow, indicators, close, high, low, barVolume, ticksProcessed, tick, er, erPrev, rvol,
                freshLong, freshShort, pullLong, pullShort, contLong, contShort, crossLong, crossShort,
                chopFlips, wyckoff, config);

            _history.Push(high, low, close, barVolume, indicators.EmaFast, indicators.EmaSlow);
            PushVolume(barVolume);

            _snapshot.BarIndex = barIndex;
            _snapshot.BarTime = barTime;
            _snapshot.Open = open;
            _snapshot.High = high;
            _snapshot.Low = low;
            _snapshot.Close = close;
            _snapshot.BarVolume = barVolume;
            _snapshot.TicksProcessed = ticksProcessed;
            _snapshot.OrderFlow = orderFlow;
            _snapshot.Indicators = indicators;
            _snapshot.Features = _vector;
            _snapshot.FreshBreakoutLong = freshLong;
            _snapshot.FreshBreakoutShort = freshShort;
            _snapshot.PullbackLong = pullLong;
            _snapshot.PullbackShort = pullShort;
            _snapshot.ContinuationLong = contLong;
            _snapshot.ContinuationShort = contShort;
            _snapshot.RecentCrossLong = crossLong;
            _snapshot.RecentCrossShort = crossShort;
            _snapshot.ChopFlips = chopFlips;
            _snapshot.WyckoffBlockLong = wyckoff.BlockLong;
            _snapshot.WyckoffBlockShort = wyckoff.BlockShort;
            _snapshot.EfficiencyRatio = er;
            _snapshot.EfficiencyRatioPrev = erPrev;
            _snapshot.Rvol = rvol;

            return _snapshot;
        }

        public void Reset()
        {
            _history.Reset();
            _volumeHead = 0;
            _volumeCount = 0;
            Array.Clear(_volumeWindow, 0, _volumeWindow.Length);
            _vector.Clear();
        }

        void WriteFeatures(
            in OrderFlowState of,
            in IndicatorSnapshot ind,
            double close,
            double high,
            double low,
            long barVolume,
            long ticksProcessed,
            double tick,
            double er,
            double erPrev,
            double rvol,
            bool freshLong,
            bool freshShort,
            bool pullLong,
            bool pullShort,
            bool contLong,
            bool contShort,
            bool crossLong,
            bool crossShort,
            int chopFlips,
            WyckoffState wyckoff,
            UltronConfig config)
        {
            float[] f = _vector.Values;

            f[FeatureSchema.Delta] = of.Delta;
            f[FeatureSchema.BuyVolume] = of.BuyVolume;
            f[FeatureSchema.SellVolume] = of.SellVolume;
            f[FeatureSchema.BidAskPressure] = (float)of.BidAskPressureRatio;
            f[FeatureSchema.SweepDetected] = of.SweepDetected ? 1f : 0f;
            f[FeatureSchema.SweepTicks] = of.SweepTicks;
            f[FeatureSchema.AbsorptionDetected] = of.AbsorptionDetected ? 1f : 0f;
            f[FeatureSchema.IcebergRefills] = of.IcebergRefillCount;

            f[FeatureSchema.Rvol] = (float)rvol;
            f[FeatureSchema.Atr] = (float)ind.Atr;
            f[FeatureSchema.AtrTicks] = ind.Atr > 0 ? (float)(ind.Atr / tick) : 0f;
            f[FeatureSchema.Adx] = (float)ind.Adx;
            f[FeatureSchema.Er] = (float)er;

            f[FeatureSchema.Rsi] = (float)ind.Rsi;
            f[FeatureSchema.VwapDistanceTicks] = ind.Vwap > 0
                ? (float)((close - ind.Vwap) / tick)
                : 0f;
            f[FeatureSchema.PriceAboveVwap] = ind.Vwap > 0 && close > ind.Vwap ? 1f : 0f;

            double spreadTicks = Math.Abs(ind.EmaFast - ind.EmaSlow) / tick;
            f[FeatureSchema.EmaSpreadTicks] = (float)spreadTicks;
            f[FeatureSchema.PriceAboveEmaFast] = close > ind.EmaFast ? 1f : 0f;
            f[FeatureSchema.TrendAlignedLong] = contLong ? 1f : 0f;
            f[FeatureSchema.TrendAlignedShort] = contShort ? 1f : 0f;
            f[FeatureSchema.VolRatio] = ind.VolSma > 0 ? (float)(barVolume / ind.VolSma) : 0f;

            f[FeatureSchema.DonchianUpper] = (float)ind.DonchianUpper;
            f[FeatureSchema.DonchianLower] = (float)ind.DonchianLower;
            f[FeatureSchema.FreshBreakoutLong] = freshLong ? 1f : 0f;
            f[FeatureSchema.FreshBreakoutShort] = freshShort ? 1f : 0f;
            f[FeatureSchema.PullbackLong] = pullLong ? 1f : 0f;
            f[FeatureSchema.PullbackShort] = pullShort ? 1f : 0f;
            f[FeatureSchema.ContinuationLong] = contLong ? 1f : 0f;
            f[FeatureSchema.ContinuationShort] = contShort ? 1f : 0f;
            f[FeatureSchema.RecentCrossLong] = crossLong ? 1f : 0f;
            f[FeatureSchema.RecentCrossShort] = crossShort ? 1f : 0f;
            f[FeatureSchema.ChopFlips] = chopFlips;

            f[FeatureSchema.WyckoffBlockLong] = wyckoff.BlockLong ? 1f : 0f;
            f[FeatureSchema.WyckoffBlockShort] = wyckoff.BlockShort ? 1f : 0f;
            f[FeatureSchema.AdxRising] = (ind.Adx - ind.AdxBarsAgo) >= config.AccelMinAdxDelta ? 1f : 0f;
            f[FeatureSchema.ErRising] = (er - erPrev) >= config.AccelMinErDelta ? 1f : 0f;
            f[FeatureSchema.AdxStrong] = ind.Adx >= config.AdxMin + 5 ? 1f : 0f;
            f[FeatureSchema.MtfEmaRising] = ind.EmaMtf > ind.EmaMtfPrev ? 1f : 0f;
            f[FeatureSchema.BarRangeTicks] = (float)((high - low) / tick);
            f[FeatureSchema.TickCount] = ticksProcessed;
        }

        double ComputeEfficiencyRatio(double close, int period)
        {
            if (_history.Count <= period)
                return 1.0;

            double direction = Math.Abs(close - _history[period].Close);
            double volatility = 0;
            for (int i = 0; i < period; i++)
            {
                double c0 = i == 0 ? close : _history[i].Close;
                double c1 = _history[i + 1].Close;
                volatility += Math.Abs(c0 - c1);
            }

            return volatility > 0 ? direction / volatility : 1.0;
        }

        double ComputeEfficiencyRatioBarsAgo(int barsAgo, int period)
        {
            if (_history.Count <= period + barsAgo)
                return 1.0;

            double closeAt = _history[barsAgo].Close;
            double closePrev = _history[barsAgo + period].Close;
            double direction = Math.Abs(closeAt - closePrev);
            double volatility = 0;
            for (int i = barsAgo; i < barsAgo + period; i++)
            {
                volatility += Math.Abs(_history[i].Close - _history[i + 1].Close);
            }

            return volatility > 0 ? direction / volatility : 1.0;
        }

        void PushVolume(long volume)
        {
            _volumeHead = (_volumeHead + 1) % _volumeWindow.Length;
            _volumeWindow[_volumeHead] = volume;
            if (_volumeCount < _volumeWindow.Length)
                _volumeCount++;
        }

        double ComputeRvol(long currentVolume)
        {
            if (_volumeCount == 0)
                return 1.0;

            for (int i = 0; i < _volumeCount; i++)
            {
                int idx = _volumeHead - i;
                if (idx < 0)
                    idx += _volumeWindow.Length;
                _rvolSortScratch[i] = _volumeWindow[idx];
            }

            Array.Sort(_rvolSortScratch, 0, _volumeCount);
            long median = _rvolSortScratch[_volumeCount / 2];
            return median > 0 ? (double)currentVolume / median : 1.0;
        }

        static bool IsFreshDonchianBreak(bool checkLong, double high, double low, in IndicatorSnapshot ind, int lookback)
        {
            if (!ind.IsValid)
                return false;

            // Phase 2: compare against previous bar channel (extend to multi-bar in calibration)
            if (checkLong)
                return high > ind.DonchianUpperPrev;
            return low < ind.DonchianLowerPrev;
        }

        bool RecentEmaCross(bool checkLongCross, int lookbackBars)
        {
            int max = Math.Min(lookbackBars, _history.Count - 1);
            for (int i = 0; i < max; i++)
            {
                var cur = _history[i];
                var prev = _history[i + 1];
                if (checkLongCross)
                {
                    if (cur.EmaFast >= cur.EmaSlow && prev.EmaFast < prev.EmaSlow)
                        return true;
                }
                else
                {
                    if (cur.EmaFast <= cur.EmaSlow && prev.EmaFast > prev.EmaSlow)
                        return true;
                }
            }

            return false;
        }

        bool RecentEmaCrossCurrent(in IndicatorSnapshot ind, bool checkLongCross, int lookbackBars)
        {
            if (_history.Count >= 1)
            {
                var prev = _history[0];
                if (checkLongCross)
                {
                    if (ind.EmaFast >= ind.EmaSlow && prev.EmaFast < prev.EmaSlow)
                        return true;
                }
                else
                {
                    if (ind.EmaFast <= ind.EmaSlow && prev.EmaFast > prev.EmaSlow)
                        return true;
                }
            }

            return RecentEmaCross(checkLongCross, lookbackBars);
        }

        int ComputeChopFlips(in IndicatorSnapshot ind, int lookback)
        {
            if (_history.Count < lookback)
                return 0;

            int flips = 0;
            double prevDiff = _history[lookback - 1].EmaFast - _history[lookback - 1].EmaSlow;
            for (int i = lookback - 2; i >= 0; i--)
            {
                double diff = _history[i].EmaFast - _history[i].EmaSlow;
                if (Math.Sign(diff) != Math.Sign(prevDiff) && diff != 0 && prevDiff != 0)
                    flips++;
                prevDiff = diff;
            }

            double curDiff = ind.EmaFast - ind.EmaSlow;
            if (Math.Sign(curDiff) != Math.Sign(prevDiff) && curDiff != 0 && prevDiff != 0)
                flips++;

            return flips;
        }

        WyckoffState ComputeWyckoff(double close, double high, double low, int recentBars, int olderBars)
        {
            int total = recentBars + olderBars;
            if (_history.Count < total - 1)
                return default;

            double recentLow = low, recentHigh = high;
            for (int i = 0; i < recentBars - 1 && i < _history.Count; i++)
            {
                recentLow = Math.Min(recentLow, _history[i].Low);
                recentHigh = Math.Max(recentHigh, _history[i].High);
            }

            double olderLow = double.MaxValue, olderHigh = double.MinValue;
            for (int i = recentBars - 1; i < total - 1 && i < _history.Count; i++)
            {
                olderLow = Math.Min(olderLow, _history[i].Low);
                olderHigh = Math.Max(olderHigh, _history[i].High);
            }

            if (olderLow == double.MaxValue)
                return default;

            bool blockLong = recentLow < olderLow && close > olderLow;
            bool blockShort = recentHigh > olderHigh && close < olderHigh;
            return new WyckoffState(blockLong, blockShort);
        }

        readonly struct WyckoffState
        {
            public bool BlockLong { get; }
            public bool BlockShort { get; }

            public WyckoffState(bool blockLong, bool blockShort)
            {
                BlockLong = blockLong;
                BlockShort = blockShort;
            }
        }
    }
}
