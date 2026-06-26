using System;
using UltronV1.Core.Archetypes;
using UltronV1.Core.Dataset;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Gates;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Scoring;
using UltronV1.Core.Signals;

namespace UltronV1.Tests.Support
{
    internal static class DatasetEngineFixture
    {
        internal static (DatasetEngine Engine, InMemoryDatasetSink Sink, UltronConfig Config) CreateStartedEngine()
        {
            var sink = new InMemoryDatasetSink();
            var cfg = UltronConfig.Default;
            var engine = new DatasetEngine(sink);
            engine.StartSession(MnqContractSpec.Symbol, cfg);
            return (engine, sink, cfg);
        }

        internal static string RecordAccepted(DatasetEngine engine, UltronConfig cfg)
        {
            BarEvaluationResult result = BuildBarResult(accepted: true);
            return engine.RecordEvaluation(result, MnqContractSpec.Symbol, cfg);
        }

        internal static string RecordRejected(DatasetEngine engine, UltronConfig cfg)
        {
            BarEvaluationResult result = BuildBarResult(accepted: false);
            return engine.RecordEvaluation(result, MnqContractSpec.Symbol, cfg);
        }

        internal static BarEvaluationResult BuildBarResult(bool accepted)
        {
            var (snap, eval) = BuildEvaluation(accepted);
            return new BarEvaluationResult
            {
                Snapshot = snap,
                Evaluation = eval,
                ContractValid = true
            };
        }

        internal static TradeOutcome BuildMinimalOutcome(string signalId)
        {
            return new TradeOutcome
            {
                SignalId = signalId,
                Direction = TradeDirection.Long,
                EntryAtUtc = new DateTime(2026, 6, 25, 14, 30, 0, DateTimeKind.Utc),
                ClosedAtUtc = new DateTime(2026, 6, 25, 14, 35, 0, DateTimeKind.Utc),
                EntryPrice = 20000.0,
                ExitPrice = 20008.0,
                StopPrice = 19992.5,
                TargetPrice = 20008.0,
                IsWin = true,
                RealizedPnLTicks = 32,
                RealizedPnLUsd = 16,
                MfeTicks = 20,
                MaeTicks = 5,
                RMultiple = 32.0 / 30.0,
                BarsInTrade = 2,
                SecondsInTrade = 300,
                ExitReason = "take_profit",
                ExecutionMode = "PaperBarSim",
                Label = 1
            };
        }

        static (BarSnapshot, EvaluationResult) BuildEvaluation(bool accepted)
        {
            var of = new OrderFlowState(
                200, 100, 100, 30, 1.3,
                true, TickSide.Buy, 4,
                false, TickSide.Unknown, 0,
                80000, 79990);

            var snap = new BarSnapshot
            {
                BarIndex = 42,
                BarTime = new DateTime(2026, 6, 25, 14, 30, 0, DateTimeKind.Utc),
                Close = 20000,
                High = 20001,
                Low = 19999,
                BarVolume = 150,
                Rvol = 1.1,
                EfficiencyRatio = 0.4,
                EfficiencyRatioPrev = 0.35,
                ChopFlips = 1,
                ContinuationLong = true,
                PullbackLong = true,
                Features = new FeatureVector(),
                Indicators = new IndicatorSnapshot(
                    19995, 19990, 25, 22, 55, 5,
                    19990, 20, 20010, 19980, 20005, 19985,
                    100, 19995, 19993, true),
                OrderFlow = of
            };

            var gates = new GateEvaluationResult();
            gates.LongAllowed = true;
            gates.ShortAllowed = false;

            var eval = new EvaluationResult
            {
                Gates = gates,
                Archetypes = new ArchetypeEvaluation(EntryArchetype.Pullback, EntryArchetype.None, true, false),
                Score = new ScoreBreakdown { LongScore = 60, ShortScore = 10 },
                OrderFlowConfirmation = OrderFlowConfirmationResult.Pass("test")
            };

            eval.Explanation.LongScore = 60;
            eval.Explanation.ShortScore = 10;
            eval.Explanation.EvaluatedDirection = TradeDirection.Long;
            eval.Explanation.Regime = MarketRegime.Trending;
            eval.Explanation.Tier = SignalTier.B;
            eval.Explanation.Accepted = accepted;
            eval.Explanation.RejectionStage = accepted ? RejectionStage.None : RejectionStage.Score;
            eval.Explanation.RejectionSummary = accepted ? string.Empty : "tier below min";
            eval.Explanation.LongContributions.Add(new ScoreContribution("Trend", 18, true));
            eval.Explanation.LongContributions.Add(new ScoreContribution("Delta", 12, true));
            eval.Gates.Checks.Add(new GateCheckResult("EMA", true, false, "ok"));

            if (accepted)
            {
                eval.Signal = new UltronSignal
                {
                    Direction = TradeDirection.Long,
                    Archetype = EntryArchetype.Pullback,
                    Score = 60,
                    Tier = SignalTier.B,
                    IsValid = true
                };
                eval.DatasetFields.Direction = TradeDirection.Long;
                eval.DatasetFields.Archetype = EntryArchetype.Pullback;
                eval.DatasetFields.PrimaryScore = 60;
                eval.DatasetFields.ScoreBreakdownSide = TradeDirection.Long;
            }
            else
            {
                eval.DatasetFields.Direction = TradeDirection.Long;
                eval.DatasetFields.Archetype = EntryArchetype.Pullback;
                eval.DatasetFields.PrimaryScore = 60;
                eval.DatasetFields.ScoreBreakdownSide = TradeDirection.Long;
            }

            return (snap, eval);
        }
    }
}
