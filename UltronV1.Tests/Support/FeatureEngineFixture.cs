using System;
using System.Collections.Generic;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;

namespace UltronV1.Tests.Support
{
    /// <summary>
    /// Deterministic bar sequences and builders for FeatureEngine tests.
    /// All prices are valid MNQ grid values (multiples of <see cref="MnqContractSpec.TickSize"/> = 0.25).
    /// </summary>
    internal static class FeatureEngineFixture
    {
        internal const double Tick = MnqContractSpec.TickSize;
        internal static readonly DateTime BaseTimeUtc = new DateTime(2026, 6, 25, 14, 30, 0, DateTimeKind.Utc);

        internal static UltronConfig CreateConfig() => UltronConfig.Default;

        internal static FeatureEngine CreateEngine() => new FeatureEngine();

        internal static double Price(int tickOffsetFrom20000) =>
            MnqContractSpec.TickIndexToPrice(MnqContractSpec.PriceToTickIndex(20000.0) + tickOffsetFrom20000);

        internal static OrderFlowState CreateOrderFlow(
            long buyVolume = 0,
            long sellVolume = 0,
            long delta = 0,
            double bidAskPressure = 1.0,
            bool sweepDetected = false,
            int sweepTicks = 0,
            bool absorptionDetected = false,
            int icebergRefills = 0)
        {
            return new OrderFlowState(
                buyVolume,
                sellVolume,
                delta,
                tickCount: 10,
                bidAskPressure,
                sweepDetected,
                TickSide.Buy,
                sweepTicks,
                absorptionDetected,
                TickSide.Sell,
                icebergRefills,
                barHighTickIndex: 80100,
                barLowTickIndex: 80090);
        }

        internal static IndicatorSnapshot CreateIndicators(
            double emaFast = 20000.0,
            double emaSlow = 19990.0,
            double adx = 25.0,
            double adxBarsAgo = 22.0,
            double rsi = 55.0,
            double atr = 5.0,
            double vwap = 20000.0,
            double donchianUpper = 20020.0,
            double donchianLower = 19980.0,
            double donchianUpperPrev = 20010.0,
            double donchianLowerPrev = 19990.0,
            double volSma = 100.0,
            double emaMtf = 20000.0,
            double emaMtfPrev = 19999.0,
            bool isValid = true)
        {
            return new IndicatorSnapshot(
                emaFast,
                emaSlow,
                adx,
                adxBarsAgo,
                rsi,
                atr,
                vwap,
                vwapBarCount: 20,
                donchianUpper,
                donchianLower,
                donchianUpperPrev,
                donchianLowerPrev,
                volSma,
                emaMtf,
                emaMtfPrev,
                isValid);
        }

        /// <summary>Single MNQ-valid bar for passthrough and flag tests.</summary>
        internal static IReadOnlyList<BarStep> SingleBarBaseline => new[]
        {
            new BarStep(
                open: Price(0),
                high: Price(4),
                low: Price(0),
                close: Price(2),
                volume: 200,
                ticksProcessed: 42)
        };

        /// <summary>Flat closes on the MNQ tick grid — reusable for ER, ATR, Donchian, RVOL tests.</summary>
        internal static IReadOnlyList<BarStep> FlatSequence(int barCount)
        {
            var bars = new BarStep[barCount];
            for (int i = 0; i < barCount; i++)
            {
                bars[i] = new BarStep(Price(0), Price(0), Price(0), Price(0), volume: 100, ticksProcessed: 1);
            }

            return bars;
        }

        /// <summary>Monotonic +1 tick (+0.25) per bar — trending ER baseline.</summary>
        internal static IReadOnlyList<BarStep> TrendingCloses(int barCount)
        {
            var bars = new BarStep[barCount];
            for (int i = 0; i < barCount; i++)
            {
                int tick = i;
                bars[i] = new BarStep(
                    open: Price(tick),
                    high: Price(tick + 1),
                    low: Price(tick),
                    close: Price(tick),
                    volume: 100,
                    ticksProcessed: 1);
            }

            return bars;
        }

        /// <summary>Alternating ±1 tick — choppy ER baseline (ends above start by one tick).</summary>
        internal static IReadOnlyList<BarStep> ChoppyOscillatingCloses(int barCount)
        {
            var bars = new BarStep[barCount];
            for (int i = 0; i < barCount; i++)
            {
                int tick = i % 2;
                bars[i] = new BarStep(
                    open: Price(tick),
                    high: Price(tick + 1),
                    low: Price(tick),
                    close: Price(tick),
                    volume: 100,
                    ticksProcessed: 1);
            }

            return bars;
        }

        /// <summary>Uniform volume history followed by a spike bar (median baseline for RVOL).</summary>
        internal static IReadOnlyList<BarStep> UniformVolumeThenSpike(int uniformBars, long uniformVolume, long spikeVolume)
        {
            var bars = new List<BarStep>(uniformBars + 1);
            for (int i = 0; i < uniformBars; i++)
            {
                bars.Add(new BarStep(Price(0), Price(0), Price(0), Price(0), uniformVolume, ticksProcessed: 1));
            }

            bars.Add(new BarStep(Price(0), Price(0), Price(0), Price(0), spikeVolume, ticksProcessed: 1));
            return bars;
        }

        internal static BarSnapshot FeedSequence(
            FeatureEngine engine,
            IReadOnlyList<BarStep> sequence,
            UltronConfig config,
            Func<int, BarStep, IndicatorSnapshot>? indicatorsFactory = null,
            Func<int, BarStep, OrderFlowState>? orderFlowFactory = null)
        {
            indicatorsFactory ??= (_, __) => CreateIndicators();
            orderFlowFactory ??= (_, __) => OrderFlowState.Empty;

            BarSnapshot last = default!;
            for (int i = 0; i < sequence.Count; i++)
            {
                BarStep step = sequence[i];
                last = engine.BuildSnapshot(
                    i,
                    BaseTimeUtc.AddMinutes(5 * i),
                    step.Open,
                    step.High,
                    step.Low,
                    step.Close,
                    step.Volume,
                    step.TicksProcessed,
                    orderFlowFactory(i, step),
                    indicatorsFactory(i, step),
                    config);
            }

            return last;
        }

        internal readonly struct BarStep
        {
            public double Open { get; }
            public double High { get; }
            public double Low { get; }
            public double Close { get; }
            public long Volume { get; }
            public long TicksProcessed { get; }

            public BarStep(double open, double high, double low, double close, long volume, long ticksProcessed)
            {
                Open = open;
                High = high;
                Low = low;
                Close = close;
                Volume = volume;
                TicksProcessed = ticksProcessed;
            }
        }
    }
}
