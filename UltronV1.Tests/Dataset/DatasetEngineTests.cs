using System;
using System.IO;
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
using Xunit;

namespace UltronV1.Tests.Dataset
{
    public class DatasetEngineTests
    {
        [Fact]
        public void RecordEvaluation_WritesVersionedRecord()
        {
            var sink = new InMemoryDatasetSink();
            var dataset = new DatasetEngine(sink);
            var cfg = UltronConfig.Default;
            cfg.EnableDatasetEngine = true;
            cfg.ConfigVersion = "1.0.0-test";

            dataset.StartSession(MnqContractSpec.Symbol, cfg);

            var eval = BuildEvaluation(accepted: true);
            var result = new BarEvaluationResult { Snapshot = eval.Item1, Evaluation = eval.Item2, ContractValid = true };

            string signalId = dataset.RecordEvaluation(result, MnqContractSpec.Symbol, cfg);

            Assert.Single(sink.Opportunities);
            var rec = sink.Opportunities[0];
            Assert.Equal(signalId, rec.SignalId);
            Assert.Equal(UltronVersions.UltronVersion, rec.UltronVersion);
            Assert.Equal(UltronVersions.FeatureSchemaVersion, rec.FeatureSchemaVersion);
            Assert.Equal(UltronVersions.ScoreVersion, rec.ScoreVersion);
            Assert.Equal(ScoreProfileHash.Compute(cfg), rec.ScoreProfileHash);
            Assert.Equal(ScoreProfileHash.Compute(cfg), dataset.Manifest.ScoreProfileHash);
            Assert.Equal("1.0.0-test", rec.ConfigVersion);
            Assert.Equal(40, rec.Features.Length);
            Assert.Equal(DatasetDecision.Accepted, rec.Decision);
            Assert.NotEmpty(rec.ScoreBreakdown);
            Assert.NotEmpty(rec.GateChecks);
        }

        [Fact]
        public void EnrichOutcome_LinksToAcceptedSignal()
        {
            var sink = new InMemoryDatasetSink();
            var dataset = new DatasetEngine(sink);
            var cfg = UltronConfig.Default;

            dataset.StartSession(MnqContractSpec.Symbol, cfg);
            var eval = BuildEvaluation(accepted: true);
            var result = new BarEvaluationResult { Snapshot = eval.Item1, Evaluation = eval.Item2, ContractValid = true };
            string signalId = dataset.RecordEvaluation(result, MnqContractSpec.Symbol, cfg);

            bool ok = dataset.EnrichOutcome(new TradeOutcome
            {
                SignalId = signalId,
                Direction = TradeDirection.Long,
                EntryAtUtc = DateTime.UtcNow.AddMinutes(-5),
                ClosedAtUtc = DateTime.UtcNow,
                EntryPrice = 20000,
                ExitPrice = 20008,
                StopPrice = 19992.5,
                TargetPrice = 20008,
                IsWin = true,
                MfeTicks = 20,
                MaeTicks = 5,
                RMultiple = 2.0,
                RealizedPnLTicks = 32,
                RealizedPnLUsd = 16,
                BarsInTrade = 4,
                SecondsInTrade = 120,
                ExitReason = "take_profit",
                ExecutionMode = "PaperBarSim"
            });

            Assert.True(ok);
            Assert.Single(sink.Outcomes);
            Assert.Equal(1, sink.Outcomes[0].Label);
            Assert.Equal(1, dataset.OutcomeCount);
        }

        [Fact]
        public void JsonLinesSink_WritesManifestAndOpportunities()
        {
            string dir = Path.Combine(Path.GetTempPath(), "UltronV1_Test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var sink = new JsonLinesDatasetSink(dir);
                var dataset = new DatasetEngine(sink);
                var cfg = UltronConfig.Default;
                dataset.StartSession(MnqContractSpec.Symbol, cfg);

                var eval = BuildEvaluation(accepted: false);
                var result = new BarEvaluationResult { Snapshot = eval.Item1, Evaluation = eval.Item2, ContractValid = true };
                dataset.RecordEvaluation(result, MnqContractSpec.Symbol, cfg);
                dataset.EndSession();

                string sessionDir = Path.Combine(dir, "sessions", dataset.SessionId);
                Assert.True(File.Exists(Path.Combine(sessionDir, "manifest.json")));
                Assert.True(File.Exists(Path.Combine(sessionDir, "opportunities.jsonl")));
                Assert.True(File.Exists(Path.Combine(sessionDir, "outcomes.jsonl")));

                string manifestJson = File.ReadAllText(Path.Combine(sessionDir, "manifest.json"));
                string oppLine = File.ReadAllLines(Path.Combine(sessionDir, "opportunities.jsonl"))[0];
                string expectedHash = ScoreProfileHash.Compute(cfg);
                Assert.Contains("\"scoreProfileHash\":\"" + expectedHash + "\"", manifestJson);
                Assert.Contains("\"scoreProfileHash\":\"" + expectedHash + "\"", oppLine);
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        static Tuple<BarSnapshot, EvaluationResult> BuildEvaluation(bool accepted)
        {
            var of = new OrderFlowState(
                200, 100, 100, 30, 1.3,
                true, TickSide.Buy, 4,
                false, TickSide.Unknown, 0,
                80000, 79990);

            var snap = new BarSnapshot
            {
                BarIndex = 42,
                BarTime = new DateTime(2026, 6, 25, 14, 30, 0),
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

            return Tuple.Create(snap, eval);
        }
    }
}
