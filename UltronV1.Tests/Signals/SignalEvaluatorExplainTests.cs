using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;
using Xunit;

namespace UltronV1.Tests.Signals
{
    public class SignalEvaluatorExplainTests
    {
        [Fact]
        public void Evaluate_RejectsWithGateExplanation()
        {
            var eval = new SignalEvaluator();
            var snap = BuildSnapshot(adx: 10, er: 0.05, close: 20010, emaFast: 20000);
            var result = eval.Evaluate(snap, UltronConfig.Default);

            Assert.Null(result.Signal);
            Assert.False(result.Explanation.Accepted);
            Assert.Equal(RejectionStage.HardGate, result.Explanation.RejectionStage);
            Assert.NotEmpty(result.Explanation.GateRejections);
        }

        [Fact]
        public void Evaluate_AcceptedSignal_HasScoreContributions()
        {
            var eval = new SignalEvaluator();
            var snap = BuildStrongLongSnapshot();
            var result = eval.Evaluate(snap, UltronConfig.Default);

            Assert.NotNull(result.Signal);
            Assert.True(result.Explanation.Accepted);
            Assert.NotEmpty(result.Explanation.LongContributions);
            Assert.Contains("ACCEPT", result.Explanation.FormatExplainBlock());
        }

        static BarSnapshot BuildSnapshot(double adx, double er, double close, double emaFast)
        {
            return new BarSnapshot
            {
                Close = close,
                High = close + 1,
                Low = close - 1,
                BarVolume = 100,
                EfficiencyRatio = er,
                EfficiencyRatioPrev = er - 0.05,
                ChopFlips = 0,
                Rvol = 1.0,
                Features = new FeatureVector(),
                ContinuationLong = close > emaFast,
                Indicators = new IndicatorSnapshot(
                    emaFast, emaFast - 5, adx, adx - 3, 58, 6,
                    emaFast, 20, emaFast + 10, emaFast - 10, emaFast + 8, emaFast - 8,
                    80, emaFast, emaFast - 0.5, true),
                OrderFlow = OrderFlowState.Empty
            };
        }

        static BarSnapshot BuildStrongLongSnapshot()
        {
            var of = new OrderFlowState(
                500, 200, 300, 50, 1.5,
                true, TickSide.Buy, 5,
                true, TickSide.Sell, 3,
                80100, 80090);

            return new BarSnapshot
            {
                Close = 20010,
                High = 20011,
                Low = 20009,
                BarVolume = 200,
                EfficiencyRatio = 0.5,
                EfficiencyRatioPrev = 0.4,
                ChopFlips = 0,
                Rvol = 1.5,
                PullbackLong = true,
                ContinuationLong = true,
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
