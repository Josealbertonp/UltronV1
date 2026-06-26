using UltronV1.Core.Dataset;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;
using Xunit;

namespace UltronV1.Tests.Dataset
{
    public class DatasetRecordBuilderTests
    {
        [Fact]
        public void Build_GateRejection_UsesNoneDirection_NoScoreBreakdown()
        {
            var evaluator = new SignalEvaluator();
            var snap = new BarSnapshot
            {
                Close = 20010,
                High = 20011,
                Low = 20009,
                BarVolume = 100,
                EfficiencyRatio = 0.05,
                EfficiencyRatioPrev = 0.04,
                ChopFlips = 0,
                Rvol = 1.0,
                Features = new FeatureVector(),
                ContinuationLong = true,
                Indicators = new IndicatorSnapshot(
                    20000, 19990, 10, 8, 58, 6,
                    20000, 20, 20010, 19990, 20008, 19992,
                    80, 20000, 19999, true),
                OrderFlow = OrderFlowState.Empty
            };

            EvaluationResult evaluation = evaluator.Evaluate(snap, UltronConfig.Default);
            var barResult = new BarEvaluationResult
            {
                Snapshot = snap,
                Evaluation = evaluation,
                ContractValid = true
            };

            OpportunityRecord record = DatasetRecordBuilder.Build(
                barResult, "sess1", "MNQ 09-26", UltronConfig.Default, new ShadowRecordEntry());

            Assert.Equal(RejectionStage.HardGate, record.RejectionStage);
            Assert.Equal(DatasetDecision.Rejected, record.Decision);
            Assert.Equal(TradeDirection.None, record.Direction);
            Assert.Equal(EntryArchetype.None, record.Archetype);
            Assert.Equal(0, record.Score);
            Assert.Empty(record.ScoreBreakdown);
            Assert.Contains("-N", record.SignalId);
        }

        [Fact]
        public void Build_AcceptedSignal_MatchesSignalFields()
        {
            var evaluator = new SignalEvaluator();
            var of = new OrderFlowState(
                500, 200, 300, 50, 1.5,
                true, TickSide.Buy, 5,
                true, TickSide.Sell, 3,
                80100, 80090);

            var snap = new BarSnapshot
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

            EvaluationResult evaluation = evaluator.Evaluate(snap, UltronConfig.Default);
            Assert.NotNull(evaluation.Signal);

            var barResult = new BarEvaluationResult
            {
                Snapshot = snap,
                Evaluation = evaluation,
                ContractValid = true
            };

            OpportunityRecord record = DatasetRecordBuilder.Build(
                barResult, "sess1", "MNQ 09-26", UltronConfig.Default, new ShadowRecordEntry());

            Assert.Equal(DatasetDecision.Accepted, record.Decision);
            Assert.Equal(evaluation.Signal!.Direction, record.Direction);
            Assert.Equal(evaluation.Signal.Archetype, record.Archetype);
            Assert.Equal(evaluation.Signal.Score, record.Score);
            Assert.NotEmpty(record.ScoreBreakdown);
            Assert.Contains("-L", record.SignalId);
        }

        [Fact]
        public void Build_ScoreInsufficient_UsesNoneDirection_WithLongBreakdownOnly()
        {
            var evaluator = new SignalEvaluator();
            var snap = new BarSnapshot
            {
                Close = 20010,
                High = 20011,
                Low = 20009,
                BarVolume = 100,
                EfficiencyRatio = 0.5,
                EfficiencyRatioPrev = 0.4,
                ChopFlips = 0,
                Rvol = 1.0,
                PullbackLong = true,
                ContinuationLong = true,
                Features = new FeatureVector(),
                Indicators = new IndicatorSnapshot(
                    20000, 19990, 25, 22, 58, 6,
                    20000, 20, 20010, 19990, 20008, 19992,
                    80, 20000, 19999, true),
                OrderFlow = OrderFlowState.Empty
            };

            var cfg = UltronConfig.Default;
            cfg.ScoreMinEntry = 99;

            EvaluationResult evaluation = evaluator.Evaluate(snap, cfg);
            var barResult = new BarEvaluationResult
            {
                Snapshot = snap,
                Evaluation = evaluation,
                ContractValid = true
            };

            OpportunityRecord record = DatasetRecordBuilder.Build(
                barResult, "sess1", "MNQ 09-26", cfg, new ShadowRecordEntry());

            Assert.Equal(RejectionStage.Score, record.RejectionStage);
            Assert.Equal(TradeDirection.None, record.Direction);
            Assert.Equal(0, record.Score);
            Assert.NotEmpty(record.ScoreBreakdown);
        }
    }
}
