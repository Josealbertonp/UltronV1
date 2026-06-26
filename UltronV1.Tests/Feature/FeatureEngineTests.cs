using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Tests.Support;
using Xunit;

namespace UltronV1.Tests.Feature
{
    public class FeatureEngineTests
    {
        readonly UltronConfig _cfg = FeatureEngineFixture.CreateConfig();

        [Fact]
        public void BuildSnapshot_AlwaysProduces40Features()
        {
            var engine = FeatureEngineFixture.CreateEngine();
            BarSnapshot snap = FeatureEngineFixture.FeedSequence(
                engine,
                FeatureEngineFixture.SingleBarBaseline,
                _cfg);

            Assert.Equal(FeatureSchema.Count, snap.Features.Values.Length);
            for (int i = 0; i < FeatureSchema.Count; i++)
            {
                Assert.True(float.IsFinite(snap.Features[i]), $"Feature index {i} must be finite");
            }
        }

        [Fact]
        public void OrderFlowFeatures_MappedDirectlyFromState()
        {
            var engine = FeatureEngineFixture.CreateEngine();
            var orderFlow = FeatureEngineFixture.CreateOrderFlow(
                buyVolume: 500,
                sellVolume: 200,
                delta: 300,
                bidAskPressure: 1.5,
                sweepDetected: true,
                sweepTicks: 5,
                absorptionDetected: true,
                icebergRefills: 3);

            BarSnapshot snap = FeatureEngineFixture.FeedSequence(
                engine,
                FeatureEngineFixture.SingleBarBaseline,
                _cfg,
                orderFlowFactory: (_, __) => orderFlow);

            Assert.Equal(300f, snap.Features[FeatureSchema.Delta]);
            Assert.Equal(500f, snap.Features[FeatureSchema.BuyVolume]);
            Assert.Equal(200f, snap.Features[FeatureSchema.SellVolume]);
            Assert.Equal(1.5f, snap.Features[FeatureSchema.BidAskPressure], 3);
            Assert.Equal(1f, snap.Features[FeatureSchema.SweepDetected]);
            Assert.Equal(5f, snap.Features[FeatureSchema.SweepTicks]);
            Assert.Equal(1f, snap.Features[FeatureSchema.AbsorptionDetected]);
            Assert.Equal(3f, snap.Features[FeatureSchema.IcebergRefills]);
        }

        [Fact]
        public void IndicatorDerivedFeatures_ComputedFromSnapshot()
        {
            var engine = FeatureEngineFixture.CreateEngine();
            double close = FeatureEngineFixture.Price(40);
            double high = FeatureEngineFixture.Price(44);
            double low = FeatureEngineFixture.Price(36);
            var step = new FeatureEngineFixture.BarStep(
                FeatureEngineFixture.Price(36),
                high,
                low,
                close,
                volume: 200,
                ticksProcessed: 17);

            var indicators = FeatureEngineFixture.CreateIndicators(
                emaFast: FeatureEngineFixture.Price(0),
                emaSlow: FeatureEngineFixture.Price(-40),
                atr: 5.0,
                vwap: FeatureEngineFixture.Price(0),
                volSma: 100.0);

            BarSnapshot snap = FeatureEngineFixture.FeedSequence(
                engine,
                new[] { step },
                _cfg,
                (_, __) => indicators);

            Assert.Equal(20f, snap.Features[FeatureSchema.AtrTicks], 3);
            Assert.Equal(40f, snap.Features[FeatureSchema.VwapDistanceTicks], 3);
            Assert.Equal(1f, snap.Features[FeatureSchema.PriceAboveVwap]);
            Assert.Equal(40f, snap.Features[FeatureSchema.EmaSpreadTicks], 3);
            Assert.Equal(1f, snap.Features[FeatureSchema.PriceAboveEmaFast]);
            Assert.Equal(2f, snap.Features[FeatureSchema.VolRatio], 3);
            Assert.Equal(8f, snap.Features[FeatureSchema.BarRangeTicks], 3);
            Assert.Equal(17f, snap.Features[FeatureSchema.TickCount]);
        }

        [Fact]
        public void FreshBreakoutLong_WhenHighExceedsDonchianUpperPrev()
        {
            var engine = FeatureEngineFixture.CreateEngine();
            var step = new FeatureEngineFixture.BarStep(
                FeatureEngineFixture.Price(0),
                high: FeatureEngineFixture.Price(48),
                low: FeatureEngineFixture.Price(0),
                close: FeatureEngineFixture.Price(40),
                volume: 100,
                ticksProcessed: 1);

            var indicators = FeatureEngineFixture.CreateIndicators(
                donchianUpperPrev: FeatureEngineFixture.Price(44));

            BarSnapshot snap = FeatureEngineFixture.FeedSequence(
                engine,
                new[] { step },
                _cfg,
                (_, __) => indicators);

            Assert.Equal(1f, snap.Features[FeatureSchema.FreshBreakoutLong]);
            Assert.Equal(0f, snap.Features[FeatureSchema.FreshBreakoutShort]);
            Assert.True(snap.FreshBreakoutLong);
            Assert.False(snap.FreshBreakoutShort);
        }

        [Fact]
        public void PullbackLong_WhenLowWithinToleranceAndCloseAboveEma()
        {
            var engine = FeatureEngineFixture.CreateEngine();
            double emaFast = FeatureEngineFixture.Price(0);
            var step = new FeatureEngineFixture.BarStep(
                emaFast,
                high: FeatureEngineFixture.Price(8),
                low: FeatureEngineFixture.Price(2),
                close: FeatureEngineFixture.Price(4),
                volume: 100,
                ticksProcessed: 1);

            var indicators = FeatureEngineFixture.CreateIndicators(emaFast: emaFast, emaSlow: FeatureEngineFixture.Price(-8));

            BarSnapshot snap = FeatureEngineFixture.FeedSequence(
                engine,
                new[] { step },
                _cfg,
                (_, __) => indicators);

            Assert.Equal(1f, snap.Features[FeatureSchema.PullbackLong]);
            Assert.True(snap.PullbackLong);
        }

        [Fact]
        public void ContinuationLong_WhenTrendAligned()
        {
            var engine = FeatureEngineFixture.CreateEngine();
            double emaFast = FeatureEngineFixture.Price(0);
            double emaSlow = FeatureEngineFixture.Price(-40);
            var step = new FeatureEngineFixture.BarStep(
                emaFast,
                high: FeatureEngineFixture.Price(44),
                low: FeatureEngineFixture.Price(36),
                close: FeatureEngineFixture.Price(40),
                volume: 100,
                ticksProcessed: 1);

            var indicators = FeatureEngineFixture.CreateIndicators(emaFast: emaFast, emaSlow: emaSlow);

            BarSnapshot snap = FeatureEngineFixture.FeedSequence(
                engine,
                new[] { step },
                _cfg,
                (_, __) => indicators);

            Assert.Equal(1f, snap.Features[FeatureSchema.TrendAlignedLong]);
            Assert.Equal(1f, snap.Features[FeatureSchema.ContinuationLong]);
            Assert.True(snap.ContinuationLong);
        }

        [Fact]
        public void EfficiencyRatio_LowerInChoppySequenceThanTrendingSequence()
        {
            const int barCount = 12;
            var trendingEngine = FeatureEngineFixture.CreateEngine();
            BarSnapshot trendingSnap = FeatureEngineFixture.FeedSequence(
                trendingEngine,
                FeatureEngineFixture.TrendingCloses(barCount),
                _cfg);

            var choppyEngine = FeatureEngineFixture.CreateEngine();
            BarSnapshot choppySnap = FeatureEngineFixture.FeedSequence(
                choppyEngine,
                FeatureEngineFixture.ChoppyOscillatingCloses(barCount),
                _cfg);

            Assert.True(trendingSnap.EfficiencyRatio > choppySnap.EfficiencyRatio);
            Assert.True(trendingSnap.Features[FeatureSchema.Er] > choppySnap.Features[FeatureSchema.Er]);
            Assert.Equal(trendingSnap.EfficiencyRatio, trendingSnap.Features[FeatureSchema.Er], 3);
            Assert.Equal(choppySnap.EfficiencyRatio, choppySnap.Features[FeatureSchema.Er], 3);
        }

        [Fact]
        public void Rvol_HigherWhenCurrentVolumeExceedsHistoricalMedian()
        {
            var engine = FeatureEngineFixture.CreateEngine();
            BarSnapshot snap = FeatureEngineFixture.FeedSequence(
                engine,
                FeatureEngineFixture.UniformVolumeThenSpike(uniformBars: 5, uniformVolume: 100, spikeVolume: 500),
                _cfg);

            Assert.Equal(5.0, snap.Rvol, 3);
            Assert.Equal(5f, snap.Features[FeatureSchema.Rvol], 3);
        }
    }
}
