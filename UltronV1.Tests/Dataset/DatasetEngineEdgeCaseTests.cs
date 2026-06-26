using System;
using UltronV1.Core.Dataset;
using UltronV1.Core.Engine;
using UltronV1.Core.Mnq;
using UltronV1.Core.Signals;
using UltronV1.Tests.Support;
using Xunit;

namespace UltronV1.Tests.Dataset
{
    public class DatasetEngineEdgeCaseTests
    {
        [Fact]
        public void EnrichOutcome_UnknownSignalId_DoesNotMutateSinkOrCounters()
        {
            var (engine, sink, cfg) = DatasetEngineFixture.CreateStartedEngine();
            RecordRejected(engine, cfg);

            bool linked = engine.EnrichOutcome(DatasetEngineFixture.BuildMinimalOutcome("missing-signal"));

            Assert.False(linked);
            Assert.Empty(sink.Outcomes);
            Assert.Equal(0, engine.OutcomeCount);
            Assert.Equal(1, engine.RecordCount);
            Assert.Equal(0, engine.AcceptedCount);
        }

        [Fact]
        public void EnrichOutcome_RejectedOpportunitySignalId_ReturnsFalse()
        {
            var (engine, sink, cfg) = DatasetEngineFixture.CreateStartedEngine();
            string rejectedSignalId = RecordRejected(engine, cfg);

            bool linked = engine.EnrichOutcome(DatasetEngineFixture.BuildMinimalOutcome(rejectedSignalId));

            Assert.False(linked);
            Assert.Empty(sink.Outcomes);
            Assert.Equal(0, engine.OutcomeCount);
            Assert.False(engine.TryGetAcceptedRecord(rejectedSignalId, out _));
        }

        /// <summary>
        /// Documents a known RC1 limitation: duplicate outcomes for the same signalId are not blocked.
        /// This is NOT desired long-term behavior. When deduplication is implemented, revise or replace this test.
        /// </summary>
        [Fact]
        public void DuplicateOutcome_CurrentRc1Behavior_IsNotIdempotent()
        {
            var (engine, sink, cfg) = DatasetEngineFixture.CreateStartedEngine();
            string signalId = RecordAccepted(engine, cfg);
            TradeOutcome outcome = DatasetEngineFixture.BuildMinimalOutcome(signalId);

            Assert.True(engine.EnrichOutcome(outcome));
            Assert.True(engine.EnrichOutcome(outcome));

            Assert.Equal(2, sink.Outcomes.Count);
            Assert.Equal(2, engine.OutcomeCount);
            Assert.Equal(2, sink.Manifest!.OutcomeCount);
        }

        [Fact]
        public void EnrichOutcome_AfterEndSession_ReturnsFalseWithoutWrite()
        {
            var (engine, sink, cfg) = DatasetEngineFixture.CreateStartedEngine();
            string signalId = RecordAccepted(engine, cfg);
            engine.EndSession();

            bool linked = engine.EnrichOutcome(DatasetEngineFixture.BuildMinimalOutcome(signalId));

            Assert.False(linked);
            Assert.Empty(sink.Outcomes);
            Assert.Equal(0, engine.OutcomeCount);
        }

        [Fact]
        public void EnrichOutcome_EmptySignalId_ReturnsFalseWithoutWrite()
        {
            var (engine, sink, cfg) = DatasetEngineFixture.CreateStartedEngine();
            RecordAccepted(engine, cfg);

            bool linked = engine.EnrichOutcome(DatasetEngineFixture.BuildMinimalOutcome(string.Empty));

            Assert.False(linked);
            Assert.Empty(sink.Outcomes);
            Assert.Equal(0, engine.OutcomeCount);
        }

        [Fact]
        public void RecordEvaluation_WithoutStartSession_Throws()
        {
            var sink = new InMemoryDatasetSink();
            var engine = new DatasetEngine(sink);
            BarEvaluationResult result = DatasetEngineFixture.BuildBarResult(accepted: true);

            Assert.Throws<InvalidOperationException>(() =>
                engine.RecordEvaluation(result, MnqContractSpec.Symbol, UltronConfig.Default));

            Assert.Empty(sink.Opportunities);
            Assert.Equal(0, engine.RecordCount);
        }

        [Fact]
        public void ManifestCounters_ReflectRejectedAcceptedAndOutcome()
        {
            var (engine, sink, cfg) = DatasetEngineFixture.CreateStartedEngine();
            RecordRejected(engine, cfg);
            string acceptedSignalId = RecordAccepted(engine, cfg);

            Assert.True(engine.EnrichOutcome(DatasetEngineFixture.BuildMinimalOutcome(acceptedSignalId)));

            Assert.Equal(2, engine.RecordCount);
            Assert.Equal(1, engine.AcceptedCount);
            Assert.Equal(1, engine.OutcomeCount);
            Assert.Equal(2, sink.Opportunities.Count);
            Assert.Single(sink.Outcomes);
            Assert.Equal(2, sink.Manifest!.OpportunityCount);
            Assert.Equal(1, sink.Manifest.AcceptedCount);
            Assert.Equal(1, sink.Manifest.OutcomeCount);
        }

        static string RecordAccepted(DatasetEngine engine, UltronConfig cfg) =>
            DatasetEngineFixture.RecordAccepted(engine, cfg);

        static string RecordRejected(DatasetEngine engine, UltronConfig cfg) =>
            DatasetEngineFixture.RecordRejected(engine, cfg);
    }
}
