using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using UltronV1.Core.Engine;

namespace UltronV1.Core.Ml
{
    /// <summary>
    /// ONNX Runtime inference for exported LightGBM models.
    /// Falls back to feature-importance heuristic when ONNX unavailable.
    /// </summary>
    public sealed class OnnxInferenceEngine : IDisposable
    {
        readonly InferenceSession? _session;
        readonly ModelManifestBundle _bundle;
        readonly IUltronLogger _logger;
        bool _disposed;

        public bool UsesOnnx => _session != null;

        public OnnxInferenceEngine(ModelManifestBundle bundle, IUltronLogger? logger = null)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _logger = logger ?? NullUltronLogger.Instance;

            if (!File.Exists(bundle.OnnxPath))
            {
                UltronStructuredLog.Warn(
                    _logger,
                    UltronLogComponents.OnnxInferenceEngine,
                    "LoadSession",
                    null,
                    UltronLogActions.DisableOnnx,
                    Context("onnxPath", bundle.OnnxPath, "modelDirectory", bundle.ModelDirectory));
                return;
            }

            try
            {
                _session = new InferenceSession(bundle.OnnxPath);
            }
            catch (Exception ex)
            {
                _session = null;
                UltronStructuredLog.Warn(
                    _logger,
                    UltronLogComponents.OnnxInferenceEngine,
                    "LoadSession",
                    ex,
                    UltronLogActions.DisableOnnx,
                    Context("onnxPath", bundle.OnnxPath, "modelDirectory", bundle.ModelDirectory));
            }
        }

        public double PredictWinProbability(float[] features)
        {
            if (_session != null)
            {
                try
                {
                    return RunOnnx(features);
                }
                catch (Exception ex)
                {
                    UltronStructuredLog.Warn(
                        _logger,
                        UltronLogComponents.OnnxInferenceEngine,
                        "Predict",
                        ex,
                        UltronLogActions.FallbackHeuristic,
                        Context("onnxPath", _bundle.OnnxPath, "featureCount", features?.Length.ToString() ?? "0"));
                }
            }

            return HeuristicProbability(features ?? Array.Empty<float>(), _bundle);
        }

        double RunOnnx(float[] features)
        {
            var inputName = _session!.InputMetadata.Keys.First();
            var tensor = new DenseTensor<float>(features, new[] { 1, features.Length });
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
            var output = results.First();
            var probs = output.AsEnumerable<float>().ToArray();
            if (probs.Length >= 2)
                return probs[1];
            if (probs.Length == 1)
                return probs[0];

            var dense = output.AsTensor<float>();
            if (dense.Dimensions.Length == 2 && dense.Dimensions[1] >= 2)
                return dense[0, 1];
            return dense[0];
        }

        static double HeuristicProbability(float[] features, ModelManifestBundle bundle)
        {
            if (bundle.FeatureWeights.Count == 0)
            {
                double avg = features.Length > 0 ? features.Average() : 0;
                return Sigmoid(avg);
            }

            double score = 0;
            int n = Math.Min(features.Length, bundle.FeatureWeights.Count);
            for (int i = 0; i < n; i++)
                score += features[i] * bundle.FeatureWeights[i].Weight;

            return Sigmoid(score * 0.01);
        }

        static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));

        static Dictionary<string, string> Context(string k1, string v1, string k2, string v2)
        {
            return new Dictionary<string, string>(2)
            {
                [k1] = v1 ?? string.Empty,
                [k2] = v2 ?? string.Empty
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            try
            {
                _session?.Dispose();
            }
            catch (Exception ex)
            {
                UltronStructuredLog.Warn(
                    _logger,
                    UltronLogComponents.OnnxInferenceEngine,
                    "Dispose",
                    ex,
                    UltronLogActions.Propagate,
                    Context("onnxPath", _bundle.OnnxPath, "modelDirectory", _bundle.ModelDirectory));
            }
        }
    }
}
