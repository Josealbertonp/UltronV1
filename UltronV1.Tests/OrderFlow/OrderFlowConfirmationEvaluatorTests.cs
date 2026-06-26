using UltronV1.Core.Domain;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;
using Xunit;

namespace UltronV1.Tests.OrderFlow
{
    public class OrderFlowConfirmationEvaluatorTests
    {
        readonly OrderFlowConfirmationEvaluator _eval = new OrderFlowConfirmationEvaluator();

        [Fact]
        public void PullbackLong_RequiresPositiveDelta()
        {
            var snap = BuildSnap(delta: 50, sweep: false, pressure: 1.2);
            var r = _eval.Evaluate(snap, TradeDirection.Long, EntryArchetype.Pullback);
            Assert.True(r.Passed);
        }

        [Fact]
        public void PullbackLong_FailsOnNegativeDelta()
        {
            var snap = BuildSnap(delta: -10, sweep: false, pressure: 0.8);
            var r = _eval.Evaluate(snap, TradeDirection.Long, EntryArchetype.Pullback);
            Assert.False(r.Passed);
            Assert.Equal("DeltaNotReverting", r.Reason);
        }

        [Fact]
        public void BreakoutLong_RequiresSweepAndDelta()
        {
            var of = new OrderFlowState(100, 50, 50, 10, 1.5, true, TickSide.Buy, 5, false, TickSide.Unknown, 0, 80000, 79990);
            var snap = new BarSnapshot { OrderFlow = of, Close = 20000 };
            var r = _eval.Evaluate(snap, TradeDirection.Long, EntryArchetype.Breakout);
            Assert.True(r.Passed);
        }

        static BarSnapshot BuildSnap(long delta, bool sweep, double pressure)
        {
            long buy = delta > 0 ? 100 : 40;
            long sell = delta > 0 ? 40 : 100;
            var of = new OrderFlowState(
                buy, sell, delta, 20, pressure,
                sweep, sweep ? TickSide.Buy : TickSide.Unknown, 5,
                false, TickSide.Unknown, 0, 80000, 79990);
            return new BarSnapshot { OrderFlow = of, Close = 20000 };
        }
    }
}
