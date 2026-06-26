using System;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using Xunit;

namespace UltronV1.Tests.OrderFlow
{
    public class MnqTickProcessorTests
    {
        static TickEvent MakeTick(long timeMs, double price, long vol, double bid, double ask)
        {
            int tickIndex = MnqContractSpec.PriceToTickIndex(price);
            long utcTicks = timeMs * TimeSpan.TicksPerMillisecond;
            return new TickEvent(utcTicks, price, vol, bid, ask, tickIndex);
        }

        [Fact]
        public void ProcessTick_ClassifiesBuyAtAsk()
        {
            var proc = new MnqTickProcessor();
            proc.ProcessTick(MakeTick(0, 20000.00, 10, 19999.75, 20000.00));

            Assert.Equal(10, proc.CurrentBarState.BuyVolume);
            Assert.Equal(0, proc.CurrentBarState.SellVolume);
            Assert.Equal(10, proc.CurrentBarState.Delta);
        }

        [Fact]
        public void ProcessTick_ClassifiesSellAtBid()
        {
            var proc = new MnqTickProcessor();
            proc.ProcessTick(MakeTick(0, 19999.75, 5, 19999.75, 20000.00));

            Assert.Equal(0, proc.CurrentBarState.BuyVolume);
            Assert.Equal(5, proc.CurrentBarState.SellVolume);
            Assert.Equal(-5, proc.CurrentBarState.Delta);
        }

        [Fact]
        public void ProcessTick_DetectsSweepWithinWindow()
        {
            var proc = new MnqTickProcessor();
            double basePrice = 20000.00;

            proc.ProcessTick(MakeTick(0, basePrice, 1, basePrice - 0.25, basePrice));
            proc.ProcessTick(MakeTick(100, basePrice + 1.00, 1, basePrice + 0.75, basePrice + 1.00));

            Assert.True(proc.CurrentBarState.SweepDetected);
            Assert.Equal(TickSide.Buy, proc.CurrentBarState.SweepDirection);
            Assert.True(proc.CurrentBarState.SweepTicks >= MnqContractSpec.SweepMinTicks);
        }

        [Fact]
        public void FinalizeBar_ResetsAccumulators()
        {
            var proc = new MnqTickProcessor();
            proc.ProcessTick(MakeTick(0, 20000.00, 10, 19999.75, 20000.00));

            var finalized = proc.FinalizeBar();
            Assert.Equal(10, finalized.BuyVolume);
            Assert.Equal(0, proc.CurrentBarState.BuyVolume);
            Assert.Equal(0, proc.CurrentBarState.TickCount);
        }

        [Fact]
        public void ProcessTick_DetectsAbsorptionOnTightRangeHighVolume()
        {
            var proc = new MnqTickProcessor();
            double bid = 20000.00;
            double ask = 20000.25;

            for (int i = 0; i < 60; i++)
            {
                proc.ProcessTick(MakeTick(i * 10, 20000.00, 2, bid, ask));
                proc.ProcessTick(MakeTick(i * 10 + 5, 20000.25, 1, bid, ask));
            }

            Assert.True(proc.CurrentBarState.AbsorptionDetected);
        }
    }
}
