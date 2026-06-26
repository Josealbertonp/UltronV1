using System;
using UltronV1.Core.Dataset;
using UltronV1.Core.Mnq;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Execution
{
    /// <summary>
    /// Builds TradeOutcome from closed paper cycle — deterministic, replay-safe.
    /// </summary>
    public static class OutcomeBuilder
    {
        public static TradeOutcome Build(
            TradeCycleState state,
            double exitPrice,
            string exitReason,
            DateTime closedAtUtc,
            int exitBarIndex)
        {
            double pnlTicks = SignedPnLTicks(state.Direction, state.EntryPrice, exitPrice);
            double rMultiple = state.RiskTicks > 0 ? pnlTicks / state.RiskTicks : 0.0;
            int barsInTrade = Math.Max(1, exitBarIndex - state.EntryBarIndex);
            int seconds = (int)Math.Max(0, (closedAtUtc - state.EntryAtUtc).TotalSeconds);

            return new TradeOutcome
            {
                SignalId = state.SignalId,
                Direction = state.Direction,
                EntryAtUtc = state.EntryAtUtc,
                ClosedAtUtc = closedAtUtc,
                EntryPrice = state.EntryPrice,
                ExitPrice = exitPrice,
                StopPrice = state.StopPrice,
                TargetPrice = state.TargetPrice,
                IsWin = pnlTicks > 0,
                RealizedPnLTicks = pnlTicks,
                RealizedPnLUsd = pnlTicks * MnqContractSpec.TickValueUsd,
                MfeTicks = state.MfeTicks,
                MaeTicks = state.MaeTicks,
                RMultiple = rMultiple,
                BarsInTrade = barsInTrade,
                SecondsInTrade = seconds,
                ExitReason = exitReason,
                ExecutionMode = state.ExecutionMode,
                Label = pnlTicks > 0 ? 1 : 0
            };
        }

        public static double SignedPnLTicks(TradeDirection direction, double entryPrice, double exitPrice)
        {
            double raw = (exitPrice - entryPrice) / MnqContractSpec.TickSize;
            return direction == TradeDirection.Long ? raw : -raw;
        }

        public static double FavorableTicks(TradeDirection direction, double entryPrice, double high, double low)
        {
            if (direction == TradeDirection.Long)
                return Math.Max(0, (high - entryPrice) / MnqContractSpec.TickSize);
            return Math.Max(0, (entryPrice - low) / MnqContractSpec.TickSize);
        }

        public static double AdverseTicks(TradeDirection direction, double entryPrice, double high, double low)
        {
            if (direction == TradeDirection.Long)
                return Math.Max(0, (entryPrice - low) / MnqContractSpec.TickSize);
            return Math.Max(0, (high - entryPrice) / MnqContractSpec.TickSize);
        }
    }
}
