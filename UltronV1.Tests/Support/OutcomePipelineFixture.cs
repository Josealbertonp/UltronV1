using System;
using System.Collections.Generic;
using System.Linq;
using UltronV1.Core.Dataset;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Execution;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;
using Xunit;

namespace UltronV1.Tests.Support
{
    /// <summary>
    /// In-memory orchestration of Signal → Dataset → TradeCycleTracker → EnrichOutcome.
    /// All prices use the MNQ tick grid (multiples of 0.25).
    /// </summary>
    internal sealed class OutcomePipelineFixture
    {
        internal const int DefaultStopTicks = 30;
        internal const int DefaultTargetTicks = 32;
        internal const int DefaultMaxBars = 48;

        internal OutcomePipelineContext CreateContext(
            int stopTicks = DefaultStopTicks,
            int takeProfitTicks = DefaultTargetTicks,
            int maxBarsInTrade = DefaultMaxBars)
        {
            var sink = new InMemoryDatasetSink();
            var cfg = FeatureEngineFixture.CreateConfig();
            cfg.EnableDatasetEngine = true;
            cfg.EnableOutcomeCollection = true;

            var dataset = new DatasetEngine(sink);
            var engine = new UltronEngine(cfg, new ListUltronLogger(), dataset);
            engine.Initialize(MnqContractSpec.Symbol);

            var paperCfg = new PaperExecutionConfig
            {
                StopLossTicks = stopTicks,
                TakeProfitTicks = takeProfitTicks,
                MaxBarsInTrade = maxBarsInTrade,
                ExecutionMode = "PaperBarSim"
            };

            return new OutcomePipelineContext(
                sink,
                dataset,
                engine,
                new TradeCycleTracker(paperCfg),
                cfg,
                paperCfg);
        }

        internal BarEvaluationResult ProcessAcceptedSignalBar(OutcomePipelineContext ctx, int signalBarIndex = 12)
        {
            const int warmupBars = 12;
            for (int i = 0; i < warmupBars; i++)
            {
                ctx.Engine.ProcessBarClose(
                    i,
                    FeatureEngineFixture.BaseTimeUtc.AddMinutes(5 * i),
                    FeatureEngineFixture.Price(0),
                    FeatureEngineFixture.Price(1),
                    FeatureEngineFixture.Price(0),
                    FeatureEngineFixture.Price(0),
                    barVolume: 150,
                    BuildWarmupIndicators());
            }

            FeedStrongLongTicks(ctx.Engine, FeatureEngineFixture.Price(40));

            var result = ctx.Engine.ProcessBarClose(
                signalBarIndex,
                FeatureEngineFixture.BaseTimeUtc.AddMinutes(5 * signalBarIndex),
                FeatureEngineFixture.Price(0),
                FeatureEngineFixture.Price(44),
                FeatureEngineFixture.Price(0),
                FeatureEngineFixture.Price(40),
                barVolume: 200,
                BuildSignalBarIndicators());

            if (result.Signal == null || string.IsNullOrEmpty(result.LastSignalId))
                throw new InvalidOperationException(
                    "Expected accepted signal from engine path. Signal or LastSignalId missing.");

            return result;
        }

        internal TradeOutcome RunPipeline(
            OutcomePipelineContext ctx,
            IReadOnlyList<PostEntryBar> postEntryBars)
        {
            BarEvaluationResult signal = ProcessAcceptedSignalBar(ctx);
            return RunPipelineFromSignal(ctx, signal, postEntryBars);
        }

        internal TradeOutcome RunPipelineFromSignal(
            OutcomePipelineContext ctx,
            BarEvaluationResult signal,
            IReadOnlyList<PostEntryBar> postEntryBars)
        {
            DateTime entryAtUtc = ToUtc(signal.Snapshot.BarTime);
            int entryBarIndex = signal.Snapshot.BarIndex;
            double entryPrice = signal.Snapshot.Close;

            if (!ctx.Tracker.TryOpen(
                    signal.LastSignalId!,
                    signal.Signal!.Direction,
                    entryPrice,
                    entryAtUtc,
                    entryBarIndex))
            {
                throw new InvalidOperationException("TradeCycleTracker.TryOpen failed.");
            }

            TradeOutcome? outcome = null;
            foreach (PostEntryBar bar in postEntryBars)
            {
                outcome = ctx.Tracker.OnBar(
                    bar.High,
                    bar.Low,
                    bar.Close,
                    entryAtUtc.AddMinutes(5 * bar.BarsAfterEntry),
                    entryBarIndex + bar.BarsAfterEntry);

                if (outcome != null)
                    break;
            }

            if (outcome == null)
                throw new InvalidOperationException("Trade did not close within post-entry bar sequence.");

            if (!ctx.Dataset.EnrichOutcome(outcome))
                throw new InvalidOperationException("DatasetEngine.EnrichOutcome returned false.");

            return outcome;
        }

        internal void AssertOutcomeContract(
            OutcomePipelineContext ctx,
            BarEvaluationResult signal,
            TradeOutcome outcome)
        {
            Assert.Single(ctx.Sink.Outcomes);
            Assert.True(ctx.Dataset.TryGetAcceptedRecord(signal.LastSignalId!, out OpportunityRecord opportunity));
            Assert.Equal(DatasetDecision.Accepted, opportunity.Decision);
            Assert.Equal(1, ctx.Sink.Opportunities.Count(o => o.Decision == DatasetDecision.Accepted));
            Assert.Equal(signal.LastSignalId, opportunity.SignalId);
            Assert.Equal(outcome.SignalId, opportunity.SignalId);
            Assert.Equal("PaperBarSim", outcome.ExecutionMode);
            Assert.Equal(signal.Signal!.Direction, outcome.Direction);
            Assert.True(outcome.ClosedAtUtc > outcome.EntryAtUtc);
            Assert.Equal(ToUtc(signal.Snapshot.BarTime), outcome.EntryAtUtc);
            Assert.Equal(1, ctx.Dataset.OutcomeCount);
            Assert.Equal(1, ctx.Sink.Manifest!.OutcomeCount);
        }

        internal static IReadOnlyList<PostEntryBar> TakeProfitBars()
        {
            double entry = FeatureEngineFixture.Price(40);
            double target = entry + DefaultTargetTicks * FeatureEngineFixture.Tick;
            return new[]
            {
                new PostEntryBar(1, entry + FeatureEngineFixture.Tick, entry - FeatureEngineFixture.Tick, entry),
                new PostEntryBar(2, target, entry, target - FeatureEngineFixture.Tick)
            };
        }

        internal static IReadOnlyList<PostEntryBar> StopLossBars()
        {
            double entry = FeatureEngineFixture.Price(40);
            double stop = entry - DefaultStopTicks * FeatureEngineFixture.Tick;
            return new[]
            {
                new PostEntryBar(1, entry + FeatureEngineFixture.Tick, stop, stop + FeatureEngineFixture.Tick)
            };
        }

        internal static IReadOnlyList<PostEntryBar> MaxBarsSequence(int maxBarsInTrade)
        {
            double entry = FeatureEngineFixture.Price(40);
            var bars = new PostEntryBar[maxBarsInTrade];
            for (int i = 0; i < maxBarsInTrade; i++)
            {
                bars[i] = new PostEntryBar(
                    i + 1,
                    entry + FeatureEngineFixture.Tick,
                    entry - FeatureEngineFixture.Tick,
                    entry);
            }

            return bars;
        }

        internal static IReadOnlyList<PostEntryBar> AmbiguousStopTargetBar(int stopTicks, int targetTicks)
        {
            double entry = FeatureEngineFixture.Price(40);
            double stop = entry - stopTicks * FeatureEngineFixture.Tick;
            double target = entry + targetTicks * FeatureEngineFixture.Tick;
            return new[]
            {
                new PostEntryBar(1, target + FeatureEngineFixture.Tick, stop, entry)
            };
        }

        static void FeedStrongLongTicks(UltronEngine engine, double price)
        {
            int tickIndex = MnqContractSpec.PriceToTickIndex(price);
            double bid = price - FeatureEngineFixture.Tick;
            long timeBase = FeatureEngineFixture.BaseTimeUtc.Ticks;

            for (int i = 0; i < 30; i++)
            {
                engine.ProcessTick(new TickEvent(
                    timeBase + i * 10_000,
                    price,
                    volume: 50,
                    bid,
                    ask: price,
                    tickIndex));
            }
        }

        static IndicatorSnapshot BuildWarmupIndicators()
        {
            return FeatureEngineFixture.CreateIndicators(
                emaFast: FeatureEngineFixture.Price(0),
                emaSlow: FeatureEngineFixture.Price(-40),
                adx: 26.0,
                adxBarsAgo: 24.0,
                vwap: FeatureEngineFixture.Price(-4),
                volSma: 100.0);
        }

        static IndicatorSnapshot BuildSignalBarIndicators()
        {
            return FeatureEngineFixture.CreateIndicators(
                emaFast: FeatureEngineFixture.Price(0),
                emaSlow: FeatureEngineFixture.Price(-40),
                adx: 28.0,
                adxBarsAgo: 25.0,
                rsi: 58.0,
                atr: 6.0,
                vwap: FeatureEngineFixture.Price(-4),
                donchianUpper: FeatureEngineFixture.Price(80),
                donchianLower: FeatureEngineFixture.Price(-80),
                donchianUpperPrev: FeatureEngineFixture.Price(60),
                donchianLowerPrev: FeatureEngineFixture.Price(-60),
                volSma: 100.0,
                emaMtf: FeatureEngineFixture.Price(0),
                emaMtfPrev: FeatureEngineFixture.Price(-4));
        }

        static DateTime ToUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

        internal readonly struct PostEntryBar
        {
            public int BarsAfterEntry { get; }
            public double High { get; }
            public double Low { get; }
            public double Close { get; }

            public PostEntryBar(int barsAfterEntry, double high, double low, double close)
            {
                BarsAfterEntry = barsAfterEntry;
                High = high;
                Low = low;
                Close = close;
            }
        }

        internal sealed class OutcomePipelineContext
        {
            public InMemoryDatasetSink Sink { get; }
            public DatasetEngine Dataset { get; }
            public UltronEngine Engine { get; }
            public TradeCycleTracker Tracker { get; }
            public UltronConfig Config { get; }
            public PaperExecutionConfig PaperConfig { get; }

            public OutcomePipelineContext(
                InMemoryDatasetSink sink,
                DatasetEngine dataset,
                UltronEngine engine,
                TradeCycleTracker tracker,
                UltronConfig config,
                PaperExecutionConfig paperConfig)
            {
                Sink = sink;
                Dataset = dataset;
                Engine = engine;
                Tracker = tracker;
                Config = config;
                PaperConfig = paperConfig;
            }
        }
    }
}
