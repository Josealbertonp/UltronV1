using System;
using System.Globalization;
using UltronML;
using UltronV1.Core.Engine;
using UltronV1.Core.Signals;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Host adapter for Shadow ML — observes signals, never blocks trades.</summary>
    public sealed class UltronShadowMlHost : IDisposable
    {
        readonly ShadowMlService _service;
        readonly UltronShadowPredictionSink _sink;
        readonly Action<string> _log;
        double _latencySum;
        int _latencyCount;
        double _latencyMax;
        readonly System.Collections.Generic.List<double> _latencies = new System.Collections.Generic.List<double>();

        public UltronShadowMlHost(ShadowMlConfig config, Action<string> log)
        {
            _service = new ShadowMlService(config);
            _sink = new UltronShadowPredictionSink();
            _log = log;
        }

        public bool IsEnabled => _service.Config.EnableShadowMl;
        public string? LoadError => _service.LoadError;
        public string? ShadowFilePath => _sink.FilePath;

        public void OpenSession(string sessionDirectory)
        {
            if (!IsEnabled)
                return;
            _sink.Open(sessionDirectory);
        }

        public MlPredictionResult RunShadow(BarEvaluationResult result, DateTime barTime)
        {
            if (!IsEnabled || result.Signal == null)
                return MlPredictionResult.PassThrough;

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
            prediction.Decision = MlDecision.PassThrough;

            TrackLatency(prediction.LatencyMs);
            LogPrediction(prediction);

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
                    Threshold = prediction.Threshold,
                    ModelVersion = prediction.ModelVersion,
                    InferenceTimeMs = prediction.LatencyMs,
                    Decision = prediction.Decision,
                    Features = features
                });
            }

            return prediction;
        }

        public void OnOutcome(UltronV1.Core.Dataset.TradeOutcome outcome, string? atmLabel) =>
            _sink.EnrichOutcome(outcome, atmLabel);

        void TrackLatency(double ms)
        {
            _latencyCount++;
            _latencySum += ms;
            if (ms > _latencyMax) _latencyMax = ms;
            _latencies.Add(ms);
        }

        public void LogLatencySummary()
        {
            if (_latencyCount == 0)
                return;

            double avg = _latencySum / _latencyCount;
            double p95 = Percentile(_latencies, 0.95);
            double p99 = Percentile(_latencies, 0.99);

            _log(string.Format(CultureInfo.InvariantCulture,
                "[ML] Latency summary avg={0:F2}ms max={1:F2}ms p95={2:F2}ms p99={3:F2}ms n={4}",
                avg, _latencyMax, p95, p99, _latencyCount));
        }

        static double Percentile(System.Collections.Generic.List<double> values, double p)
        {
            if (values.Count == 0) return 0;
            var sorted = values.ToArray();
            Array.Sort(sorted);
            int idx = (int)Math.Ceiling(p * sorted.Length) - 1;
            idx = Math.Max(0, Math.Min(sorted.Length - 1, idx));
            return sorted[idx];
        }

        void LogPrediction(MlPredictionResult p)
        {
            _log("[ML]");
            _log("Model Version=" + p.ModelVersion);
            _log(string.Format(CultureInfo.InvariantCulture, "Confidence={0:F4}", p.Confidence));
            _log(string.Format(CultureInfo.InvariantCulture, "Latency={0:F2}ms", p.LatencyMs));
            _log(string.Format(CultureInfo.InvariantCulture, "Threshold={0:F2}", p.Threshold));
            _log("Decision=" + p.Decision);
            if (!string.IsNullOrEmpty(p.Reason) && p.Reason != "ShadowMode")
                _log("Note=" + p.Reason);
        }

        public void Dispose()
        {
            LogLatencySummary();
            _sink.Dispose();
            _service.Dispose();
        }
    }
}
