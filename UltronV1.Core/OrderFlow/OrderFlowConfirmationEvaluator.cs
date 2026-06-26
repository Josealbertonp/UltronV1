using UltronV1.Core.Archetypes;
using UltronV1.Core.Domain;
using UltronV1.Core.Signals;

namespace UltronV1.Core.OrderFlow
{
    public readonly struct OrderFlowConfirmationResult
    {
        public bool Passed { get; }
        public string Reason { get; }
        public string Detail { get; }

        public OrderFlowConfirmationResult(bool passed, string reason, string detail)
        {
            Passed = passed;
            Reason = reason;
            Detail = detail;
        }

        public static OrderFlowConfirmationResult Pass(string detail)
            => new OrderFlowConfirmationResult(true, "OK", detail);

        public static OrderFlowConfirmationResult Fail(string reason, string detail)
            => new OrderFlowConfirmationResult(false, reason, detail);
    }

    /// <summary>
    /// Per-archetype order-flow confirmation. Required after score eligibility (Phase 2).
    /// </summary>
    public sealed class OrderFlowConfirmationEvaluator
    {
        public OrderFlowConfirmationResult Evaluate(
            BarSnapshot snap,
            TradeDirection direction,
            EntryArchetype archetype)
        {
            if (direction == TradeDirection.None || archetype == EntryArchetype.None)
                return OrderFlowConfirmationResult.Fail("NoDirection", "archetype/direction missing");

            var of = snap.OrderFlow;
            bool isLong = direction == TradeDirection.Long;

            switch (archetype)
            {
                case EntryArchetype.Pullback:
                    return EvaluatePullback(of, isLong);
                case EntryArchetype.Breakout:
                    return EvaluateBreakout(of, isLong);
                case EntryArchetype.Continuation:
                    return EvaluateContinuation(of, isLong);
                default:
                    return EvaluateGeneric(of, isLong);
            }
        }

        static OrderFlowConfirmationResult EvaluatePullback(OrderFlowState of, bool isLong)
        {
            // Pullback: delta reverting in trade direction (buyers/sellers stepping back in)
            bool deltaOk = isLong ? of.Delta > 0 : of.Delta < 0;
            if (deltaOk)
                return OrderFlowConfirmationResult.Pass($"delta={of.Delta} reverting");

            return OrderFlowConfirmationResult.Fail(
                "DeltaNotReverting",
                $"pullback requires delta {(isLong ? ">" : "<")} 0, got {of.Delta}");
        }

        static OrderFlowConfirmationResult EvaluateBreakout(OrderFlowState of, bool isLong)
        {
            bool sweepOk = of.SweepDetected && (
                isLong ? of.SweepDirection == TickSide.Buy : of.SweepDirection == TickSide.Sell);
            bool deltaOk = isLong ? of.Delta > 0 : of.Delta < 0;

            if (sweepOk && deltaOk)
                return OrderFlowConfirmationResult.Pass($"sweep {of.SweepTicks}t delta={of.Delta}");

            if (!sweepOk && !deltaOk)
                return OrderFlowConfirmationResult.Fail("NoSweepNoDelta", "breakout requires sweep + delta align");
            if (!sweepOk)
                return OrderFlowConfirmationResult.Fail("NoSweep", "breakout requires liquidity sweep");
            return OrderFlowConfirmationResult.Fail("DeltaMisalign", $"delta={of.Delta}");
        }

        static OrderFlowConfirmationResult EvaluateContinuation(OrderFlowState of, bool isLong)
        {
            bool pressureOk = isLong
                ? of.BidAskPressureRatio >= 1.0
                : of.BidAskPressureRatio <= 1.0;
            bool deltaOk = isLong ? of.Delta >= 0 : of.Delta <= 0;

            if (pressureOk && deltaOk)
                return OrderFlowConfirmationResult.Pass($"pressure={of.BidAskPressureRatio:F2} delta={of.Delta}");

            return OrderFlowConfirmationResult.Fail(
                "PressureMisalign",
                $"continuation pressure={of.BidAskPressureRatio:F2} delta={of.Delta}");
        }

        static OrderFlowConfirmationResult EvaluateGeneric(OrderFlowState of, bool isLong)
        {
            bool deltaOk = isLong ? of.Delta > 0 : of.Delta < 0;
            return deltaOk
                ? OrderFlowConfirmationResult.Pass($"delta={of.Delta}")
                : OrderFlowConfirmationResult.Fail("DeltaMisalign", $"delta={of.Delta}");
        }
    }
}
