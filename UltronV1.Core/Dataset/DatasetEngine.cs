using System;
using System.Collections.Generic;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Scoring;

namespace UltronV1.Core.Dataset
{
    /// <summary>
    /// Single source of truth for ML datasets, replay, backtest, audit and statistics.
    /// </summary>
    public sealed class DatasetEngine
    {
        readonly IDatasetSink _sink;
        readonly IUltronLogger _logger;
        readonly ShadowPipeline _shadow = new ShadowPipeline();
        readonly Dictionary<string, OpportunityRecord> _acceptedIndex = new Dictionary<string, OpportunityRecord>(64);

        DatasetManifest _manifest = new DatasetManifest();
        string _sessionId = string.Empty;
        bool _sessionOpen;

        public DatasetEngine(IDatasetSink? sink = null, IUltronLogger? logger = null)
        {
            _sink = sink ?? NullDatasetSink.Instance;
            _logger = logger ?? NullUltronLogger.Instance;
        }

        public string SessionId => _sessionId;
        public DatasetManifest Manifest => _manifest;
        public int RecordCount => _manifest.OpportunityCount;
        public int AcceptedCount => _manifest.AcceptedCount;
        public int OutcomeCount => _manifest.OutcomeCount;

        public void StartSession(string instrument, UltronConfig config, DateTime? sessionStartUtc = null)
        {
            _sessionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            _acceptedIndex.Clear();

            _manifest = new DatasetManifest
            {
                SessionId = _sessionId,
                Instrument = instrument,
                StartedAtUtc = sessionStartUtc ?? DateTime.UtcNow,
                ConfigVersion = config.ConfigVersion,
                ScoreProfileHash = ScoreProfileHash.Compute(config),
                OpportunityCount = 0,
                AcceptedCount = 0,
                OutcomeCount = 0
            };

            _sink.OpenSession(_manifest);
            _sessionOpen = true;
        }

        /// <summary>
        /// Records every bar evaluation as a structured opportunity (ML-ready).
        /// Returns SignalId for accepted signals (for outcome linking in Phase 5).
        /// </summary>
        public string RecordEvaluation(
            BarEvaluationResult result,
            string instrument,
            UltronConfig config)
        {
            if (!_sessionOpen)
            {
                var ex = new InvalidOperationException("Dataset session not started.");
                UltronStructuredLog.Error(
                    _logger,
                    UltronLogComponents.DatasetEngine,
                    "RecordEvaluation",
                    ex,
                    UltronLogActions.Propagate,
                    new Dictionary<string, string>(1) { ["sessionId"] = _sessionId });
                throw ex;
            }

            ShadowRecordEntry shadow = _shadow.Evaluate(result.Snapshot, config);
            OpportunityRecord record = DatasetRecordBuilder.Build(
                result, _sessionId, instrument, config, shadow);

            _sink.AppendOpportunity(record);

            _manifest.OpportunityCount++;
            if (record.Decision == DatasetDecision.Accepted)
            {
                _manifest.AcceptedCount++;
                _acceptedIndex[record.SignalId] = record;
            }

            if (_manifest.OpportunityCount % 50 == 0)
                _sink.UpdateManifest(_manifest);

            return record.SignalId;
        }

        /// <summary>
        /// Links trade outcome to a prior accepted signal (Phase 5+). Writes to outcomes.jsonl.
        /// </summary>
        public bool EnrichOutcome(TradeOutcome outcome)
        {
            if (!_sessionOpen || string.IsNullOrEmpty(outcome.SignalId))
            {
                UltronStructuredLog.Warn(
                    _logger,
                    UltronLogComponents.DatasetEngine,
                    "EnrichOutcome",
                    null,
                    UltronLogActions.SkipOutcome,
                    new Dictionary<string, string>(2)
                    {
                        ["signalId"] = outcome?.SignalId ?? string.Empty,
                        ["reason"] = !_sessionOpen ? "session_not_open" : "empty_signal_id"
                    });
                return false;
            }

            if (!_acceptedIndex.ContainsKey(outcome.SignalId))
            {
                UltronStructuredLog.Warn(
                    _logger,
                    UltronLogComponents.DatasetEngine,
                    "EnrichOutcome",
                    null,
                    UltronLogActions.SkipOutcome,
                    new Dictionary<string, string>(2)
                    {
                        ["signalId"] = outcome.SignalId,
                        ["reason"] = "signal_not_found"
                    });
                return false;
            }

            if (outcome.Label < 0)
                outcome.Label = outcome.IsWin ? 1 : 0;

            _sink.AppendOutcome(outcome);
            _manifest.OutcomeCount++;
            _sink.UpdateManifest(_manifest);
            return true;
        }

        public void EndSession()
        {
            if (!_sessionOpen)
                return;

            _sink.UpdateManifest(_manifest);
            _sink.CloseSession();
            _sessionOpen = false;
        }

        public bool TryGetAcceptedRecord(string signalId, out OpportunityRecord record)
            => _acceptedIndex.TryGetValue(signalId, out record!);
    }
}
