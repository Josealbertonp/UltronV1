using System;
using System.Collections.Generic;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Ml
{
    /// <summary>
    /// Advisory ML layer — boost/penalty score only, never veto.
    /// </summary>
    public sealed class MlAdvisor : IMlAdvisor, IDisposable
    {
        readonly ModelManifestBundle _bundle;
        readonly OnnxInferenceEngine _engine;
        readonly MlInferenceConfig _cfg;
        readonly IUltronLogger _logger;
        bool _disposed;

        public MlAdvisor(string modelDirectory, IUltronLogger? logger = null)
        {
            _logger = logger ?? NullUltronLogger.Instance;
            try
            {
                _bundle = ManifestLoader.Load(modelDirectory, _logger);
                _cfg = _bundle.Config;
                _engine = new OnnxInferenceEngine(_bundle, _logger);
            }
            catch (Exception ex)
            {
                UltronStructuredLog.Error(
                    _logger,
                    UltronLogComponents.MlAdvisor,
                    "Initialize",
                    ex,
                    UltronLogActions.Propagate,
                    SingleContext("modelDirectory", modelDirectory ?? string.Empty));
                throw;
            }
        }

        public bool IsEnabled => true;

        public MlAdvisoryResult Advise(BarSnapshot snapshot, EvaluationResult evaluation, UltronConfig config)
        {
            if (!config.EnableMlAdvisor)
                return MlAdvisoryResult.Inactive;

            if (!FeatureNormalizer.TryBuildInput(
                    snapshot.Features,
                    _cfg.FeatureSchemaVersion,
                    out float[] input,
                    out string? error))
            {
                UltronStructuredLog.Warn(
                    _logger,
                    UltronLogComponents.FeatureNormalizer,
                    "TryBuildInput",
                    null,
                    UltronLogActions.MlInactive,
                    Context(
                        "modelId", _cfg.ModelId,
                        "expectedSchemaVersion", _cfg.FeatureSchemaVersion.ToString(),
                        "engineSchemaVersion", FeatureSchema.Version.ToString(),
                        "error", error ?? string.Empty));
                return InactiveWithReason($"feature error: {error}");
            }

            double prob = _engine.PredictWinProbability(input);
            int delta = ComputeScoreDelta(prob);
            var result = new MlAdvisoryResult
            {
                Active = true,
                WinProbability = prob,
                ScoreDelta = delta,
                ModelId = _cfg.ModelId,
                Summary = FormatSummary(prob, delta)
            };

            PopulateTopContributions(snapshot.Features, result.TopContributions, _bundle);
            return result;
        }

        int ComputeScoreDelta(double probability)
        {
            if (probability >= _cfg.BoostThreshold)
            {
                double range = Math.Max(1e-6, 1.0 - _cfg.BoostThreshold);
                double t = (probability - _cfg.BoostThreshold) / range;
                return (int)Math.Round(_cfg.MaxBoostPoints * t);
            }

            if (probability <= _cfg.PenaltyThreshold)
            {
                double range = Math.Max(1e-6, _cfg.PenaltyThreshold);
                double t = (_cfg.PenaltyThreshold - probability) / range;
                return (int)Math.Round(_cfg.MaxPenaltyPoints * t);
            }

            return 0;
        }

        static void PopulateTopContributions(FeatureVector features, List<MlFeatureContribution> target, ModelManifestBundle bundle)
        {
            target.Clear();
            if (bundle.FeatureWeights.Count > 0)
            {
                foreach (var (name, weight) in bundle.FeatureWeights)
                {
                    if (target.Count >= 3)
                        break;

                    int idx = Array.IndexOf(FeatureSchema.FeatureNames, name);
                    float val = idx >= 0 ? features[idx] : 0f;
                    target.Add(new MlFeatureContribution(name, val, weight));
                }
                return;
            }

            for (int i = 0; i < Math.Min(3, FeatureSchema.Count); i++)
                target.Add(new MlFeatureContribution(FeatureSchema.FeatureNames[i], features[i], Math.Abs(features[i])));
        }

        static MlAdvisoryResult InactiveWithReason(string reason)
        {
            return new MlAdvisoryResult
            {
                Active = false,
                Summary = reason
            };
        }

        static string FormatSummary(double prob, int delta)
        {
            string action = delta > 0 ? $"boost +{delta}" : delta < 0 ? $"penalty {delta}" : "neutral";
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "ML advisory: P(win)={0:F2} → {1}",
                prob,
                action);
        }

        static Dictionary<string, string> SingleContext(string key, string value)
        {
            return new Dictionary<string, string>(1) { [key] = value ?? string.Empty };
        }

        static Dictionary<string, string> Context(string k1, string v1, string k2, string v2, string k3, string v3, string k4, string v4)
        {
            return new Dictionary<string, string>(4)
            {
                [k1] = v1 ?? string.Empty,
                [k2] = v2 ?? string.Empty,
                [k3] = v3 ?? string.Empty,
                [k4] = v4 ?? string.Empty
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _engine.Dispose();
        }
    }
}
