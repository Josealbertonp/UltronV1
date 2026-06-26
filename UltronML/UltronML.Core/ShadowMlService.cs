using System;
using System.Collections.Generic;
using UltronML.Providers;

namespace UltronML
{
    /// <summary>ONNX inference service — used by Shadow and Active modes.</summary>
    public sealed class ShadowMlService : IDisposable
    {
        readonly MlHostConfig _config;
        OnnxProvider? _provider;
        bool _disposed;
        string? _loadError;

        public MlHostConfig Config => _config;
        public bool IsLoaded => _provider?.IsLoaded == true;
        public string? LoadError => _loadError;

        public ShadowMlService(MlHostConfig? config = null)
        {
            _config = config ?? new MlHostConfig();
            TryLoadProvider();
        }

        public ShadowMlService(ShadowMlConfig legacy)
            : this(legacy.ToHostConfig())
        {
        }

        void TryLoadProvider()
        {
            if (!_config.ShouldRunInference)
                return;

            if (string.IsNullOrWhiteSpace(_config.ModelDirectory))
            {
                _loadError = "ModelDirectory not configured";
                return;
            }

            try
            {
                _provider = new OnnxProvider();
                _provider.LoadModel(_config.ModelDirectory);
                _loadError = null;
            }
            catch (Exception ex)
            {
                _loadError = ex.Message;
                _provider?.Dispose();
                _provider = null;
            }
        }

        public MlPredictionResult Predict(MlPredictionInput input)
        {
            if (!_config.ShouldRunInference)
            {
                return FailSafeResult("InferenceDisabled");
            }

            if (_provider == null || !_provider.IsLoaded)
            {
                return FailSafeResult(_loadError ?? "ProviderNotLoaded");
            }

            var result = _provider.Predict(input);
            if (_config.ThresholdOverride.HasValue)
                result.Threshold = _config.ThresholdOverride.Value;
            return result;
        }

        MlPredictionResult FailSafeResult(string reason) => new MlPredictionResult
        {
            Active = false,
            Confidence = 1.0,
            ProviderName = "ONNX",
            ModelVersion = "not-loaded",
            Decision = MlDecision.Pass,
            Reason = reason
        };

        public IReadOnlyList<string> GetFeatureNames() =>
            _provider?.GetFeatureNames() ?? Array.Empty<string>();

        public string GetModelVersion() => _provider?.GetModelVersion() ?? "none";

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _provider?.Dispose();
            _provider = null;
        }
    }
}
