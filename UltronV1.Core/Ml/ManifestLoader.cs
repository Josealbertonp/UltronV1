using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;

namespace UltronV1.Core.Ml
{
    /// <summary>
    /// Loads model artifacts: config.json, feature_importance.json, training_manifest.json.
    /// </summary>
    public sealed class ModelManifestBundle
    {
        public MlInferenceConfig Config { get; internal set; } = new MlInferenceConfig();
        public string ModelDirectory { get; internal set; } = string.Empty;
        public string OnnxPath { get; internal set; } = string.Empty;
        public int FeatureSchemaVersion { get; internal set; } = FeatureSchema.Version;
        public List<(string Name, double Weight)> FeatureWeights { get; } = new List<(string, double)>(40);
    }

    public static class ManifestLoader
    {
        public static ModelManifestBundle Load(string modelDirectory, IUltronLogger? logger = null)
        {
            var log = logger ?? NullUltronLogger.Instance;

            if (string.IsNullOrWhiteSpace(modelDirectory))
            {
                var ex = new ArgumentException("Model directory is required", nameof(modelDirectory));
                UltronStructuredLog.Error(
                    log,
                    UltronLogComponents.ManifestLoader,
                    "Load",
                    ex,
                    UltronLogActions.Propagate,
                    SingleContext("modelDirectory", modelDirectory ?? string.Empty));
                throw ex;
            }

            string dir = Path.GetFullPath(modelDirectory);
            if (!Directory.Exists(dir))
            {
                var ex = new DirectoryNotFoundException($"ML model directory not found: {dir}");
                UltronStructuredLog.Error(
                    log,
                    UltronLogComponents.ManifestLoader,
                    "Load",
                    ex,
                    UltronLogActions.Propagate,
                    SingleContext("modelDirectory", dir));
                throw ex;
            }

            string configPath = Path.Combine(dir, "config.json");
            string importancePath = Path.Combine(dir, "feature_importance.json");
            string onnxPath = Path.Combine(dir, "model.onnx");

            if (!File.Exists(configPath))
            {
                var ex = new FileNotFoundException("config.json missing in model directory", configPath);
                UltronStructuredLog.Error(
                    log,
                    UltronLogComponents.ManifestLoader,
                    "LoadConfig",
                    ex,
                    UltronLogActions.Propagate,
                    Context("modelDirectory", dir, "configPath", configPath));
                throw ex;
            }

            string configJson;
            try
            {
                configJson = File.ReadAllText(configPath);
            }
            catch (Exception ex)
            {
                UltronStructuredLog.Error(
                    log,
                    UltronLogComponents.ManifestLoader,
                    "ReadConfig",
                    ex,
                    UltronLogActions.Propagate,
                    Context("modelDirectory", dir, "configPath", configPath));
                throw;
            }

            var bundle = new ModelManifestBundle
            {
                ModelDirectory = dir,
                OnnxPath = onnxPath,
                Config = ParseConfig(configJson)
            };

            if (File.Exists(importancePath))
                LoadFeatureImportance(importancePath, bundle, log);

            return bundle;
        }

        static MlInferenceConfig ParseConfig(string json)
        {
            var cfg = new MlInferenceConfig
            {
                ModelId = SimpleJsonReader.ReadString(json, "modelId") ?? string.Empty
            };

            if (SimpleJsonReader.TryReadInt(json, "featureSchemaVersion", out int schema))
                cfg.FeatureSchemaVersion = schema;
            if (SimpleJsonReader.TryReadBool(json, "advisoryOnly", out bool adv))
                cfg.AdvisoryOnly = adv;
            if (SimpleJsonReader.TryReadBool(json, "neverVeto", out bool neverVeto))
                cfg.NeverVeto = neverVeto;
            if (SimpleJsonReader.TryReadInt(json, "maxBoostPoints", out int boost))
                cfg.MaxBoostPoints = boost;
            if (SimpleJsonReader.TryReadInt(json, "maxPenaltyPoints", out int penalty))
                cfg.MaxPenaltyPoints = penalty;
            if (SimpleJsonReader.TryReadDouble(json, "boostThreshold", out double boostTh))
                cfg.BoostThreshold = boostTh;
            if (SimpleJsonReader.TryReadDouble(json, "penaltyThreshold", out double penTh))
                cfg.PenaltyThreshold = penTh;

            return cfg;
        }

        static void LoadFeatureImportance(string path, ModelManifestBundle bundle, IUltronLogger log)
        {
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                UltronStructuredLog.Warn(
                    log,
                    UltronLogComponents.ManifestLoader,
                    "ReadFeatureImportance",
                    ex,
                    UltronLogActions.UseEmptyWeights,
                    Context("modelDirectory", bundle.ModelDirectory, "importancePath", path));
                return;
            }

            var matches = Regex.Matches(
                json,
                "\"feature\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"index\"\\s*:\\s*(\\d+)\\s*,\\s*\"importance\"\\s*:\\s*([0-9.eE+-]+)",
                RegexOptions.CultureInvariant);

            foreach (Match m in matches)
            {
                string name = m.Groups[1].Value;
                if (double.TryParse(m.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double w))
                    bundle.FeatureWeights.Add((name, w));
            }

            if (matches.Count > 0 && bundle.FeatureWeights.Count == 0)
            {
                UltronStructuredLog.Warn(
                    log,
                    UltronLogComponents.ManifestLoader,
                    "ParseFeatureImportance",
                    null,
                    UltronLogActions.UseEmptyWeights,
                    Context("modelDirectory", bundle.ModelDirectory, "importancePath", path));
            }
        }

        static Dictionary<string, string> SingleContext(string key, string value)
        {
            return new Dictionary<string, string>(1) { [key] = value ?? string.Empty };
        }

        static Dictionary<string, string> Context(string k1, string v1, string k2, string v2)
        {
            return new Dictionary<string, string>(2)
            {
                [k1] = v1 ?? string.Empty,
                [k2] = v2 ?? string.Empty
            };
        }
    }
}
