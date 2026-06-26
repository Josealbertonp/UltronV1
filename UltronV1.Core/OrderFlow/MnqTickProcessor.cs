using System;
using System.Runtime.CompilerServices;
using UltronV1.Core.Mnq;

namespace UltronV1.Core.OrderFlow
{
    /// <summary>
    /// Hot-path tick processor for MNQ. All bar accumulators are fields (no per-tick allocation).
    /// Classify aggression: price &gt;= ask → buy, price &lt;= bid → sell (NightRanger-compatible model).
    /// </summary>
    public sealed class MnqTickProcessor
    {
        long _barBuyVolume;
        long _barSellVolume;
        int _barTickCount;
        int _barHighTickIndex = int.MinValue;
        int _barLowTickIndex = int.MaxValue;

        int _sweepStartTickIndex;
        long _sweepStartTimestampUtcTicks;
        bool _sweepTracking;

        bool _barSweepDetected;
        TickSide _barSweepDirection = TickSide.Unknown;
        int _barSweepTicks;

        int _lastTradeTickIndex = int.MinValue;
        int _icebergRefillCount;
        int _sameLevelTradeCount;

        readonly int[] _footprintBuy;
        readonly int[] _footprintSell;
        int _footprintAnchorTickIndex;

        public MnqTickProcessor()
        {
            _footprintBuy = new int[MnqContractSpec.FootprintTickSlots];
            _footprintSell = new int[MnqContractSpec.FootprintTickSlots];
            _footprintAnchorTickIndex = 0;
        }

        public OrderFlowState CurrentBarState { get; private set; } = OrderFlowState.Empty;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProcessTick(in TickEvent tick)
        {
            TickSide side = ClassifySide(tick.Price, tick.Bid, tick.Ask);

            if (side == TickSide.Buy)
                _barBuyVolume += tick.Volume;
            else if (side == TickSide.Sell)
                _barSellVolume += tick.Volume;

            _barTickCount++;

            if (tick.TickIndex > _barHighTickIndex)
                _barHighTickIndex = tick.TickIndex;
            if (tick.TickIndex < _barLowTickIndex)
                _barLowTickIndex = tick.TickIndex;

            UpdateFootprint(tick.TickIndex, side, tick.Volume);
            UpdateSweepTracking(tick);
            UpdateIcebergHeuristic(tick.TickIndex);

            CurrentBarState = BuildState();
        }

        /// <summary>
        /// Finalizes the current bar, returns its order-flow snapshot, and resets bar accumulators.
        /// </summary>
        public OrderFlowState FinalizeBar()
        {
            OrderFlowState finalized = BuildState();
            ResetBarAccumulators();
            CurrentBarState = OrderFlowState.Empty;
            return finalized;
        }

        public void ResetSession()
        {
            ResetBarAccumulators();
            Array.Clear(_footprintBuy, 0, _footprintBuy.Length);
            Array.Clear(_footprintSell, 0, _footprintSell.Length);
            _footprintAnchorTickIndex = 0;
            CurrentBarState = OrderFlowState.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static TickSide ClassifySide(double price, double bid, double ask)
        {
            if (ask > 0 && price >= ask)
                return TickSide.Buy;
            if (bid > 0 && price <= bid)
                return TickSide.Sell;
            return TickSide.Unknown;
        }

        void UpdateFootprint(int tickIndex, TickSide side, long volume)
        {
            if (_barTickCount == 1)
                _footprintAnchorTickIndex = tickIndex;

            int slot = tickIndex - _footprintAnchorTickIndex + MnqContractSpec.FootprintCenterIndex;
            if (slot < 0 || slot >= MnqContractSpec.FootprintTickSlots)
                return;

            int vol = volume > int.MaxValue ? int.MaxValue : (int)volume;
            if (side == TickSide.Buy)
                _footprintBuy[slot] += vol;
            else if (side == TickSide.Sell)
                _footprintSell[slot] += vol;
        }

        void UpdateSweepTracking(in TickEvent tick)
        {
            if (!_sweepTracking)
            {
                _sweepTracking = true;
                _sweepStartTickIndex = tick.TickIndex;
                _sweepStartTimestampUtcTicks = tick.TimestampUtcTicks;
                return;
            }

            long elapsedTicks = tick.TimestampUtcTicks - _sweepStartTimestampUtcTicks;
            double elapsedMs = elapsedTicks / (double)TimeSpan.TicksPerMillisecond;

            if (elapsedMs > MnqContractSpec.SweepMaxElapsedMs)
            {
                _sweepStartTickIndex = tick.TickIndex;
                _sweepStartTimestampUtcTicks = tick.TimestampUtcTicks;
                return;
            }

            int displacement = Math.Abs(tick.TickIndex - _sweepStartTickIndex);
            if (displacement >= MnqContractSpec.SweepMinTicks)
            {
                _barSweepDetected = true;
                _barSweepTicks = Math.Max(_barSweepTicks, displacement);
                _barSweepDirection = tick.TickIndex > _sweepStartTickIndex ? TickSide.Buy : TickSide.Sell;

                _sweepStartTickIndex = tick.TickIndex;
                _sweepStartTimestampUtcTicks = tick.TimestampUtcTicks;
            }
        }

        void UpdateIcebergHeuristic(int tickIndex)
        {
            if (tickIndex == _lastTradeTickIndex)
            {
                _sameLevelTradeCount++;
                if (_sameLevelTradeCount >= MnqContractSpec.IcebergMinRefills)
                    _icebergRefillCount = _sameLevelTradeCount;
            }
            else
            {
                _lastTradeTickIndex = tickIndex;
                _sameLevelTradeCount = 1;
            }
        }

        OrderFlowState BuildState()
        {
            long delta = _barBuyVolume - _barSellVolume;
            double pressure = _barSellVolume > 0
                ? (double)_barBuyVolume / _barSellVolume
                : (_barBuyVolume > 0 ? 999.0 : 1.0);

            bool absorption = EvaluateAbsorption(delta);

            int high = _barHighTickIndex == int.MinValue ? 0 : _barHighTickIndex;
            int low = _barLowTickIndex == int.MaxValue ? 0 : _barLowTickIndex;

            return new OrderFlowState(
                _barBuyVolume,
                _barSellVolume,
                delta,
                _barTickCount,
                pressure,
                _barSweepDetected,
                _barSweepDirection,
                _barSweepTicks,
                absorption,
                absorption ? (delta > 0 ? TickSide.Sell : TickSide.Buy) : TickSide.Unknown,
                _icebergRefillCount,
                high,
                low);
        }

        bool EvaluateAbsorption(long delta)
        {
            long totalVolume = _barBuyVolume + _barSellVolume;
            if (totalVolume < MnqContractSpec.AbsorptionMinVolume)
                return false;

            if (_barHighTickIndex == int.MinValue || _barLowTickIndex == int.MaxValue)
                return false;

            int rangeTicks = _barHighTickIndex - _barLowTickIndex;
            if (rangeTicks > MnqContractSpec.AbsorptionMaxRangeTicks)
                return false;

            // Absorption: heavy volume, tight range, delta sign implies trapped side.
            return Math.Abs(delta) >= totalVolume * 0.15;
        }

        void ResetBarAccumulators()
        {
            _barBuyVolume = 0;
            _barSellVolume = 0;
            _barTickCount = 0;
            _barHighTickIndex = int.MinValue;
            _barLowTickIndex = int.MaxValue;
            _barSweepDetected = false;
            _barSweepDirection = TickSide.Unknown;
            _barSweepTicks = 0;
            _icebergRefillCount = 0;
            _sameLevelTradeCount = 0;
            _lastTradeTickIndex = int.MinValue;
            _sweepTracking = false;
            Array.Clear(_footprintBuy, 0, _footprintBuy.Length);
            Array.Clear(_footprintSell, 0, _footprintSell.Length);
        }
    }
}
