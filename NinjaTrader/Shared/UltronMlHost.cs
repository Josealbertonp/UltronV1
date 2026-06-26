using System;
using System.Globalization;
using UltronML;
using UltronV1.Core.Engine;
using UltronV1.Core.Signals;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Host ML adapter — Shadow observe + Safe Filter veto (Phase 4B).</summary>
    public sealed class UltronMlHost : IDisposable
    {
        readonly MlHostConfig _config;
        readonly ShadowMlService _service;
        readonly UltronShadowPredictionSink _sink;
        readonly MlFilterStatistics _stats = new MlFilterStatistics();
        readonly Action<string> _log;
        readonly System.Collections.Generic.List<double> _latencies = new System.Collections.Generic.List<double>();

        public UltronMlHost(MlHostConfig config, Action<string> log)
        {
            _config = config;
            _service = new ShadowMlService(config);
            _sink = new UltronShadowPredictionSink();
            _log = log;
        }

        public bool IsEnabled => _config.ShouldRunInference;
        public string? LoadError => _service.LoadError;
        public MlFilterStatistics Statistics => _stats;

        public void OpenSession(string sessionDirectory)
        {
            if (!IsEnabled) return;
            _sink.Open(sessionDirectory);
        }

        /// <summary>Evaluate ML at entry gate. May set AllowEntry=false only in Active+Filter mode.</summary>
        public MlFilterResult EvaluateEntry(BarEvaluationResult result, DateTime barTime)
        {
            if (!IsEnabled || result.Signal == null)
            {
                return new MlFilterResult
                {
                    AllowEntry = true,
                    Decision = MlDecision.PassThrough,
                    Reason = "MlDisabled"
                };
            }

            var featureNames = _service.GetFeatureNames();
            float[] features = UltronShadowFeatureBuilder.Build(result, featureNames, barTime);

            var input = new MlPredictionInput
            {
                Features = features,
                Score = result.Signal.Score,
                Tier = result.Signal.Tier.ToString(),
                Direction = result.Signal.Direction.ToString(),
                FeatureSchemaVersion = UltronV1.Core.Features.FeatureSchema.Version
            };

            var prediction = _service.Predict(input);
            var filter = MlFilterEvaluator.Evaluate(_config, prediction);

            _latencies.Add(prediction.LatencyMs);
            _stats.RecordFilterDecision(filter);

            LogFilter(result, filter);

            if (!string.IsNullOrEmpty(result.LastSignalId))
            {
                _sink.CaptureEntry(new ShadowPendingRecord
                {
                    TradeId = result.LastSignalId,
                    TimestampUtc = barTime.ToUniversalTime().ToString("O"),
                    Score = result.Signal.Score,
                    Tier = result.Signal.Tier.ToString(),
                    Direction = result.Signal.Direction.ToString(),
                    Confidence = prediction.Confidence,
                    Threshold = filter.Threshold,
                    ModelVersion = prediction.ModelVersion,
                    InferenceTimeMs = prediction.LatencyMs,
                    Decision = filter.Decision,
                    FilterMode = filter.FilterMode,
                    WouldBlockInActive = filter.WouldBlockInActive,
                    FilterReason = filter.Reason,
                    EntryAllowed = filter.AllowEntry,
                    Features = features
                });
            }

            return filter;
        }

        public void OnPassedOutcome(UltronV1.Core.Dataset.TradeOutcome outcome, string? atmLabel)
        {
            _stats.RecordPassedOutcome(outcome.RealizedPnLTicks);
            _sink.EnrichOutcome(outcome, atmLabel);
        }

        public void OnBlockedSignal(string tradeId)
        {
            _sink.MarkBlocked(tradeId);
        }

        void LogFilter(BarEvaluationResult result, MlFilterResult filter)
        {
            _log("[ML FILTER]");
            _log("TradeId=" + result.LastSignalId);
            _log("Score=" + result.Signal!.Score);
            _log("Tier=" + result.Signal.Tier);
            _log(string.Format(CultureInfo.InvariantCulture, "Confidence={0:F4}", filter.Prediction.Confidence));
            _log(string.Format(CultureInfo.InvariantCulture, "Threshold={0:F2}", filter.Threshold));
            _log("Decision=" + filter.Decision);
            if (!string.IsNullOrEmpty(filter.Reason))
                _log("Reason=" + filter.Reason);
            if (_config.Mode == MlFilterMode.Shadow && filter.WouldBlockInActive)
                _log("ShadowNote=WouldBlockInActive");
        }

        public void LogSessionSummary()
        {
            if (_latencies.Count == 0 && _stats.TradesPassed == 0 && _stats.TradesBlocked == 0)
                return;

            if (_latencies.Count > 0)
            {
                double sum = 0;
                foreach (double l in _latencies) sum += l;
                _log(string.Format(CultureInfo.InvariantCulture,
                    "[ML FILTER] Latency avg={0:F2}ms n={1}", sum / _latencies.Count, _latencies.Count));
            }

            _log("[ML FILTER] Stats Passed=" + _stats.TradesPassed + " Blocked=" + _stats.TradesBlocked);
            foreach (var kv in _stats.ToDictionary())
                _log("[ML FILTER] " + kv.Key + "=" + kv.Value);
        }

        public void Dispose()
        {
            LogSessionSummary();
            _sink.Dispose();
            _service.Dispose();
        }
    }
}
