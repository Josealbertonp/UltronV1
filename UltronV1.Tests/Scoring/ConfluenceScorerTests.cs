using UltronV1.Core.Archetypes;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Scoring;
using UltronV1.Core.Signals;
using UltronV1.Tests.Support;
using Xunit;

namespace UltronV1.Tests.Scoring
{
    public class ConfluenceScorerTests
    {
        [Fact]
        public void Score_IncludesOrderFlowWeightsWhenAligned()
        {
            var cfg = UltronConfig.Default;
            var snap = BuildAlignedLongSnapshot();
            var gates = GateTestFactory.AllowLongOnly();
            var arch = new ArchetypeEvaluation(
                EntryArchetype.Pullback, EntryArchetype.None, true, false);

            var scorer = new ConfluenceScorer();
            var bd = scorer.Score(snap, gates, arch, cfg);

            Assert.True(bd.LongScore > 0);
            Assert.True(bd.LongFactors.ContainsKey("Delta"));
            Assert.True(bd.LongFactors.ContainsKey("RVOL"));
        }

        static BarSnapshot BuildAlignedLongSnapshot()
        {
            var of = new OrderFlowState(
                500, 200, 300, 50, 1.5,
                true, TickSide.Buy, 5,
                true, TickSide.Sell, 3,
                80100, 80090);

            return new BarSnapshot
            {
                Close = 20010,
                BarVolume = 200,
                Rvol = 1.2,
                PullbackLong = true,
                ContinuationLong = true,
                FreshBreakoutLong = false,
                Features = new FeatureVector(),
                Indicators = new IndicatorSnapshot(
                    20000, 19990, 28, 25, 58, 6,
                    19995, 30, 20020, 19980, 20015, 19985,
                    100, 20000, 19998, true),
                OrderFlow = of
            };
        }
    }
}
