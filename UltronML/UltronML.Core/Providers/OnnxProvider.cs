using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace UltronML.Providers
{
    /// <summary>ONNX Runtime inference — RC2 Phase 4A Shadow Mode.</summary>
    public sealed class OnnxProvider : IMLProvider
    {
        InferenceSession? _session;
        ModelArtifacts? _artifacts;
        string _inputName = "features";
        bool _disposed;

        public string ProviderName => "ONNX";
        public bool IsLoaded => _session != null && _artifacts != null;

        public void LoadModel(string modelDirectory)
        {
            Unload();

            if (!ModelArtifacts.TryLoad(modelDirectory, out var artifacts, out string? error))
                throw new InvalidOperationException(error ?? "Failed to load model artifacts");

            _artifacts = artifacts;
            _session = new InferenceSession(artifacts!.OnnxPath);
            _inputName = _session.InputMetadata.Keys.FirstOrDefault() ?? "features";
        }

        public MlPredictionResult Predict(MlPredictionInput input)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (_session == null || _artifacts == null)
                    return FailSafe("Model not loaded", sw);

                if (input.Features == null || input.Features.Count == 0)
                    return FailSafe("Empty features", sw);

                if (_artifacts.FeatureNames.Count > 0
                    && input.Features.Count != _artifacts.FeatureNames.Count)
                    return FailSafe(
                        $"Feature count mismatch: {input.Features.Count} vs {_artifacts.FeatureNames.Count}",
                        sw);

                double confidence = RunInference(input.Features);
                sw.Stop();

                return new MlPredictionResult
                {
                    Active = true,
                    Confidence = confidence,
                    ProviderName = ProviderName,
                    ModelVersion = _artifacts.Version,
                    LatencyMs = sw.Elapsed.TotalMilliseconds,
                    Threshold = _artifacts.RecommendedThreshold,
                    Decision = MlDecision.PassThrough,
                    Reason = "ShadowMode"
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                return FailSafe(ex.Message, sw);
            }
        }

        MlPredictionResult FailSafe(string reason, Stopwatch sw)
        {
            sw.Stop();
            return new MlPredictionResult
            {
                Active = false,
                Confidence = 1.0,
                ProviderName = ProviderName,
                ModelVersion = _artifacts?.Version ?? "error",
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                Threshold = _artifacts?.RecommendedThreshold ?? 0.50,
                Decision = MlDecision.PassThrough,
                Reason = reason
            };
        }

        double RunInference(IReadOnlyList<float> features)
        {
            var array = features is float[] fa ? fa : features.ToArray();
            var tensor = new DenseTensor<float>(array, new[] { 1, array.Length });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, tensor)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session!.Run(inputs);
            var output = results.First();
            float[] probs = output.AsEnumerable<float>().ToArray();

            if (probs.Length >= 2)
                return Clamp01(probs[1]);
            if (probs.Length == 1)
                return Clamp01(probs[0]);

            var dense = output.AsTensor<float>();
            if (dense.Dimensions.Length == 2 && dense.Dimensions[1] >= 2)
                return Clamp01(dense[0, 1]);
            return Clamp01(dense.GetValue(0));
        }

        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        public void Unload()
        {
            _session?.Dispose();
            _session = null;
            _artifacts = null;
        }

        public string GetModelVersion() => _artifacts?.Version ?? "not-loaded";

        public IReadOnlyList<string> GetFeatureNames() =>
            _artifacts?.FeatureNames ?? Array.Empty<string>();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unload();
        }
    }
}
