using System;
using System.Collections.Generic;
using System.IO;
using UltronML.Providers;

namespace UltronML
{
    /// <summary>
    /// Loads and validates ML models. RC2 Phase 1: resolves DummyProvider when filter disabled or model missing.
    /// </summary>
    public sealed class MLModelLoader : IDisposable
    {
        readonly MlFilterConfig _config;
        IMLProvider _provider;
        bool _disposed;

        public MLModelLoader(MlFilterConfig? config = null)
        {
            _config = config ?? new MlFilterConfig();
            _provider = ResolveProvider();
        }

        public IMLProvider Provider => _provider;
        public MlFilterConfig Config => _config;

        public MlPredictionResult Predict(MlPredictionInput input)
        {
            if (!_config.EnableMlFilter)
                return MlPredictionResult.PassThrough;

            ValidateInputFeatures(input);
            return _provider.Predict(input);
        }

        IMLProvider ResolveProvider()
        {
            if (!_config.EnableMlFilter)
                return DummyProvider.Instance;

            if (string.IsNullOrWhiteSpace(_config.ModelDirectory) || !Directory.Exists(_config.ModelDirectory))
                return DummyProvider.Instance;

            if (!ValidateModelArtifacts(_config.ModelDirectory, out string? error))
            {
                return DummyProvider.Instance;
            }

            string name = (_config.ProviderName ?? "Dummy").Trim();
            if (name.Equals("Dummy", StringComparison.OrdinalIgnoreCase))
                return DummyProvider.Instance;

            if (name.Equals("ONNX", StringComparison.OrdinalIgnoreCase)
                || name.Equals("Onnx", StringComparison.OrdinalIgnoreCase))
            {
                var onnx = new OnnxProvider();
                try
                {
                    onnx.LoadModel(_config.ModelDirectory);
                    return onnx;
                }
                catch
                {
                    onnx.Dispose();
                    return DummyProvider.Instance;
                }
            }

            return DummyProvider.Instance;
        }

        public static bool ValidateModelArtifacts(string modelDirectory, out string? error)
        {
            error = null;
            if (!Directory.Exists(modelDirectory))
            {
                error = "Model directory not found";
                return false;
            }

            string versionPath = Path.Combine(modelDirectory, "version.json");
            if (!File.Exists(versionPath))
            {
                error = "version.json missing";
                return false;
            }

            return true;
        }

        static void ValidateInputFeatures(MlPredictionInput input)
        {
            if (input.Features == null || input.Features.Count == 0)
                throw new ArgumentException("Features required for ML prediction");
        }

        public IReadOnlyList<string> GetFeatureNames() => _provider.GetFeatureNames();

        public string GetModelVersion() => _provider.GetModelVersion();

        public void Dispose()
        {
            if (_disposed) return;
            _provider.Unload();
            _disposed = true;
        }
    }
}
