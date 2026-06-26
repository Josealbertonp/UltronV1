using UltronV1.Core.Dataset;
using UltronV1.Core.Engine;
using UltronV1.Core.Mnq;
using UltronV1.Core.Signals;
using UltronV1.Tests.Support;
using Xunit;

namespace UltronV1.Tests.Integration
{
    public class OutcomePipelineTests
    {
        readonly OutcomePipelineFixture _fixture = new OutcomePipelineFixture();

        [Fact]
        public void FullPipeline_TakeProfit_LinksWinOutcomeToAcceptedOpportunity()
        {
            var ctx = _fixture.CreateContext();
            BarEvaluationResult signal = _fixture.ProcessAcceptedSignalBar(ctx);
            TradeOutcome outcome = _fixture.RunPipelineFromSignal(ctx, signal, OutcomePipelineFixture.TakeProfitBars());

            _fixture.AssertOutcomeContract(ctx, signal, outcome);

            Assert.Equal("take_profit", outcome.ExitReason);
            Assert.True(outcome.IsWin);
            Assert.Equal(1, outcome.Label);
            Assert.Equal(32.0, outcome.RealizedPnLTicks, 1);
            Assert.Equal(32.0 / 30.0, outcome.RMultiple, 2);
            Assert.Equal(32.0 * MnqContractSpec.TickValueUsd, outcome.RealizedPnLUsd, 2);
            Assert.True(outcome.MfeTicks > 0);
            Assert.True(outcome.MaeTicks >= 0);
            Assert.Equal(outcome.EntryPrice + 32 * MnqContractSpec.TickSize, outcome.ExitPrice, 3);
        }

        [Fact]
        public void FullPipeline_StopLoss_LinksLossOutcomeToAcceptedOpportunity()
        {
            var ctx = _fixture.CreateContext();
            BarEvaluationResult signal = _fixture.ProcessAcceptedSignalBar(ctx);
            TradeOutcome outcome = _fixture.RunPipelineFromSignal(ctx, signal, OutcomePipelineFixture.StopLossBars());

            _fixture.AssertOutcomeContract(ctx, signal, outcome);

            Assert.Equal("stop_loss", outcome.ExitReason);
            Assert.False(outcome.IsWin);
            Assert.Equal(0, outcome.Label);
            Assert.Equal(-30.0, outcome.RealizedPnLTicks, 1);
            Assert.Equal(-1.0, outcome.RMultiple, 2);
            Assert.Equal(-30.0 * MnqContractSpec.TickValueUsd, outcome.RealizedPnLUsd, 2);
        }

        [Fact]
        public void FullPipeline_MaxBars_ExitsAtClose()
        {
            const int maxBars = 3;
            var ctx = _fixture.CreateContext(maxBarsInTrade: maxBars);
            BarEvaluationResult signal = _fixture.ProcessAcceptedSignalBar(ctx);
            TradeOutcome outcome = _fixture.RunPipelineFromSignal(
                ctx,
                signal,
                OutcomePipelineFixture.MaxBarsSequence(maxBars));

            _fixture.AssertOutcomeContract(ctx, signal, outcome);

            Assert.Equal("max_bars", outcome.ExitReason);
            Assert.Equal(signal.Snapshot.Close, outcome.EntryPrice, 3);
            Assert.Equal(signal.Snapshot.Close, outcome.ExitPrice, 3);
            Assert.Equal(maxBars, outcome.BarsInTrade);
        }

        [Fact]
        public void FullPipeline_SameBarStopAndTarget_StopFirstStillEnriches()
        {
            const int stopTicks = 10;
            const int targetTicks = 10;
            var ctx = _fixture.CreateContext(stopTicks: stopTicks, takeProfitTicks: targetTicks);
            BarEvaluationResult signal = _fixture.ProcessAcceptedSignalBar(ctx);
            TradeOutcome outcome = _fixture.RunPipelineFromSignal(
                ctx,
                signal,
                OutcomePipelineFixture.AmbiguousStopTargetBar(stopTicks, targetTicks));

            _fixture.AssertOutcomeContract(ctx, signal, outcome);

            Assert.Equal("stop_loss", outcome.ExitReason);
            Assert.False(outcome.IsWin);
            Assert.Equal(outcome.EntryPrice - stopTicks * MnqContractSpec.TickSize, outcome.ExitPrice, 3);
        }
    }
}
