using System;
using UltronV1.Core.Dataset;
using UltronV1.Core.Execution;
using UltronV1.Core.Signals;
using Xunit;

namespace UltronV1.Tests.Execution
{
    public class TradeCycleTrackerTests
    {
        [Fact]
        public void LongTargetHit_ProducesWinOutcome()
        {
            var tracker = new TradeCycleTracker(new PaperExecutionConfig
            {
                StopLossTicks = 30,
                TakeProfitTicks = 32
            });

            var entryTime = new DateTime(2026, 6, 25, 14, 30, 0, DateTimeKind.Utc);
            Assert.True(tracker.TryOpen("sig-1", TradeDirection.Long, 20000.0, entryTime, 10));

            TradeOutcome? outcome = tracker.OnBar(20004.0, 19998.0, 20003.0, entryTime.AddMinutes(5), 11);
            Assert.Null(outcome);

            outcome = tracker.OnBar(20009.0, 20000.0, 20008.5, entryTime.AddMinutes(10), 12);
            Assert.NotNull(outcome);
            Assert.Equal("take_profit", outcome!.ExitReason);
            Assert.True(outcome.IsWin);
            Assert.Equal(32.0, outcome.RealizedPnLTicks, 1);
            Assert.Equal(32.0 / 30.0, outcome.RMultiple, 2);
            Assert.False(tracker.IsOpen);
        }

        [Fact]
        public void LongStopHit_ProducesLossOutcome()
        {
            var tracker = new TradeCycleTracker(new PaperExecutionConfig { StopLossTicks = 30, TakeProfitTicks = 32 });
            var t0 = DateTime.UtcNow;
            tracker.TryOpen("sig-2", TradeDirection.Long, 20000.0, t0, 5);

            var outcome = tracker.OnBar(20001.0, 19992.0, 19993.0, t0.AddMinutes(5), 6);
            Assert.NotNull(outcome);
            Assert.Equal("stop_loss", outcome!.ExitReason);
            Assert.False(outcome.IsWin);
            Assert.Equal(-30.0, outcome.RealizedPnLTicks, 1);
            Assert.Equal(-1.0, outcome.RMultiple, 2);
        }

        [Fact]
        public void SameBarStopAndTarget_StopWinsConservative()
        {
            var tracker = new TradeCycleTracker(new PaperExecutionConfig { StopLossTicks = 10, TakeProfitTicks = 10 });
            var t0 = DateTime.UtcNow;
            tracker.TryOpen("sig-3", TradeDirection.Long, 20000.0, t0, 1);

            var outcome = tracker.OnBar(20003.0, 19997.0, 20000.0, t0.AddMinutes(5), 2);
            Assert.NotNull(outcome);
            Assert.Equal("stop_loss", outcome!.ExitReason);
        }

        [Fact]
        public void MaxBars_ExitsAtClose()
        {
            var tracker = new TradeCycleTracker(new PaperExecutionConfig
            {
                StopLossTicks = 100,
                TakeProfitTicks = 100,
                MaxBarsInTrade = 3
            });
            var t0 = DateTime.UtcNow;
            tracker.TryOpen("sig-4", TradeDirection.Short, 20000.0, t0, 10);

            tracker.OnBar(20001.0, 19999.0, 20000.0, t0.AddMinutes(5), 11);
            var outcome = tracker.OnBar(20001.0, 19999.5, 19999.8, t0.AddMinutes(10), 13);
            Assert.NotNull(outcome);
            Assert.Equal("max_bars", outcome!.ExitReason);
        }

        [Fact]
        public void MfeMae_TrackedDuringTrade()
        {
            var tracker = new TradeCycleTracker(new PaperExecutionConfig { StopLossTicks = 50, TakeProfitTicks = 50 });
            var t0 = DateTime.UtcNow;
            tracker.TryOpen("sig-5", TradeDirection.Long, 20000.0, t0, 1);

            tracker.OnBar(20005.0, 19998.0, 20004.0, t0.AddMinutes(5), 2);
            Assert.Equal(20.0, tracker.OpenCycle!.MfeTicks, 1);
            Assert.Equal(8.0, tracker.OpenCycle.MaeTicks, 1);
        }
    }
}
