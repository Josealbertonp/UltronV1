using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Gates;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using Xunit;

namespace UltronV1.Tests.Gates
{
    public class HardGateEvaluatorTests
    {
        readonly HardGateEvaluator _evaluator = new HardGateEvaluator();
        readonly UltronConfig _cfg = UltronConfig.Default;

        [Fact]
        public void EmaDirection_BlocksLongWhenPriceBelowEmaFast()
        {
            var snap = MakeSnapshot(close: 19990, emaFast: 20000, adx: 25);
            var result = _evaluator.Evaluate(snap, _cfg);

            Assert.False(result.LongAllowed);
        }

        [Fact]
        public void AdxGate_BlocksWhenBelowMinimum()
        {
            var snap = MakeSnapshot(close: 20010, emaFast: 20000, adx: 15);
            var result = _evaluator.Evaluate(snap, _cfg);

            Assert.False(result.LongAllowed);
            Assert.False(result.ShortAllowed);
        }

        [Fact]
        public void ChopFilter_BlocksBothWhenTooManyFlips()
        {
            var snap = MakeSnapshot(close: 20010, emaFast: 20000, adx: 25);
            snap.ChopFlips = 5;
            var result = _evaluator.Evaluate(snap, _cfg);

            Assert.False(result.LongAllowed);
            Assert.False(result.ShortAllowed);
        }

        static BarSnapshot MakeSnapshot(double close, double emaFast, double adx)
        {
            var features = new FeatureVector();
            return new BarSnapshot
            {
                Close = close,
                High = close + 1,
                Low = close - 1,
                EfficiencyRatio = 0.5,
                EfficiencyRatioPrev = 0.4,
                ChopFlips = 0,
                Features = features,
                Indicators = new IndicatorSnapshot(
                    emaFast, emaFast - 5, adx, adx - 3, 55, 5,
                    emaFast, 20, emaFast + 10, emaFast - 10, emaFast + 8, emaFast - 8,
                    80, emaFast, emaFast - 0.5, true),
                OrderFlow = OrderFlowState.Empty,
                ContinuationLong = close > emaFast && emaFast > emaFast - 5
            };
        }
    }
}
