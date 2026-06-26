using System;
using UltronV1.Core.Dataset;
using UltronV1.Core.Mnq;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Execution
{
    /// <summary>
    /// Paper trade lifecycle (PaperBarSim): open on accepted signal, track MFE/MAE, close on stop/target/time.
    /// Authoritative bar-OHLC simulator for outcomes.jsonl — deterministic and replay-safe.
    /// No entry rules — consumes Ultron signals only.
    /// </summary>
    public sealed class TradeCycleTracker
    {
        readonly PaperExecutionConfig _config;
        TradeCycleState? _open;

        public TradeCycleTracker(PaperExecutionConfig? config = null)
        {
            _config = config ?? PaperExecutionConfig.Default;
        }

        public bool IsOpen => _open != null;
        public TradeCycleState? OpenCycle => _open;

        public bool TryOpen(string signalId, TradeDirection direction, double entryPrice, DateTime entryAtUtc, int entryBarIndex)
        {
            if (_open != null || string.IsNullOrEmpty(signalId) || direction == TradeDirection.None)
                return false;

            double tick = MnqContractSpec.TickSize;
            double stopDist = _config.StopLossTicks * tick;
            double targetDist = _config.TakeProfitTicks * tick;

            double stop = direction == TradeDirection.Long ? entryPrice - stopDist : entryPrice + stopDist;
            double target = direction == TradeDirection.Long ? entryPrice + targetDist : entryPrice - targetDist;

            _open = new TradeCycleState
            {
                SignalId = signalId,
                Direction = direction,
                EntryPrice = entryPrice,
                StopPrice = stop,
                TargetPrice = target,
                RiskTicks = _config.StopLossTicks,
                EntryAtUtc = entryAtUtc,
                EntryBarIndex = entryBarIndex,
                ExecutionMode = _config.ExecutionMode
            };
            return true;
        }

        /// <summary>
        /// Updates MFE/MAE and evaluates stop/target/max-bars on bar OHLC.
        /// Returns outcome when trade closes; null while open.
        /// </summary>
        public TradeOutcome? OnBar(double high, double low, double close, DateTime barTimeUtc, int barIndex)
        {
            if (_open == null)
                return null;

            var state = _open;
            double fav = OutcomeBuilder.FavorableTicks(state.Direction, state.EntryPrice, high, low);
            double adv = OutcomeBuilder.AdverseTicks(state.Direction, state.EntryPrice, high, low);
            if (fav > state.MfeTicks) state.MfeTicks = fav;
            if (adv > state.MaeTicks) state.MaeTicks = adv;

            // Skip exit evaluation on entry bar (signal at close — no intrabar fill on same bar)
            if (barIndex <= state.EntryBarIndex)
                return null;

            TradeOutcome? outcome = TryExitOnStopTarget(state, high, low, barTimeUtc, barIndex);
            if (outcome != null)
            {
                _open = null;
                return outcome;
            }

            if (barIndex - state.EntryBarIndex >= _config.MaxBarsInTrade)
            {
                outcome = OutcomeBuilder.Build(state, close, "max_bars", barTimeUtc, barIndex);
                _open = null;
                return outcome;
            }

            return null;
        }

        static TradeOutcome? TryExitOnStopTarget(TradeCycleState state, double high, double low, DateTime barTimeUtc, int barIndex)
        {
            if (state.Direction == TradeDirection.Long)
            {
                bool stopHit = low <= state.StopPrice;
                bool targetHit = high >= state.TargetPrice;
                if (stopHit && targetHit)
                    return OutcomeBuilder.Build(state, state.StopPrice, "stop_loss", barTimeUtc, barIndex);
                if (stopHit)
                    return OutcomeBuilder.Build(state, state.StopPrice, "stop_loss", barTimeUtc, barIndex);
                if (targetHit)
                    return OutcomeBuilder.Build(state, state.TargetPrice, "take_profit", barTimeUtc, barIndex);
            }
            else
            {
                bool stopHit = high >= state.StopPrice;
                bool targetHit = low <= state.TargetPrice;
                if (stopHit && targetHit)
                    return OutcomeBuilder.Build(state, state.StopPrice, "stop_loss", barTimeUtc, barIndex);
                if (stopHit)
                    return OutcomeBuilder.Build(state, state.StopPrice, "stop_loss", barTimeUtc, barIndex);
                if (targetHit)
                    return OutcomeBuilder.Build(state, state.TargetPrice, "take_profit", barTimeUtc, barIndex);
            }

            return null;
        }

        public void Reset() => _open = null;
    }
}
