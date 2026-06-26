using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace UltronML
{
    /// <summary>Loads champion model artifacts from UltronML/models/champion/.</summary>
    public sealed class ModelArtifacts
    {
        public string ModelDirectory { get; }
        public string OnnxPath { get; }
        public string Version { get; }
        public IReadOnlyList<string> FeatureNames { get; }
        public double RecommendedThreshold { get; }
        public string CalibrationMethod { get; }
        public Dictionary<string, object> RawMetadata { get; }

        ModelArtifacts(
            string modelDirectory,
            string onnxPath,
            string version,
            IReadOnlyList<string> featureNames,
            double recommendedThreshold,
            string calibrationMethod,
            Dictionary<string, object> rawMetadata)
        {
            ModelDirectory = modelDirectory;
            OnnxPath = onnxPath;
            Version = version;
            FeatureNames = featureNames;
            RecommendedThreshold = recommendedThreshold;
            CalibrationMethod = calibrationMethod;
            RawMetadata = rawMetadata;
        }

        public static bool TryLoad(string modelDirectory, out ModelArtifacts? artifacts, out string? error)
        {
            artifacts = null;
            error = null;

            if (string.IsNullOrWhiteSpace(modelDirectory) || !Directory.Exists(modelDirectory))
            {
                error = "Model directory not found";
                return false;
            }

            string onnxPath = Path.Combine(modelDirectory, "model.onnx");
            if (!File.Exists(onnxPath))
            {
                error = "model.onnx missing";
                return false;
            }

            string version = ReadVersion(modelDirectory);
            var featureNames = ReadFeatureList(modelDirectory);
            double threshold = ReadRecommendedThreshold(modelDirectory);
            string calibrationMethod = ReadCalibrationMethod(modelDirectory);
            var meta = ReadJsonFile(Path.Combine(modelDirectory, "metadata.json"));

            artifacts = new ModelArtifacts(
                modelDirectory,
                onnxPath,
                version,
                featureNames,
                threshold,
                calibrationMethod,
                meta);

            return true;
        }

        static string ReadVersion(string dir)
        {
            try
            {
                string json = File.ReadAllText(Path.Combine(dir, "version.json"));
                return ReadJsonString(json, "version") ?? "unknown";
            }
            catch { /* optional */ }
            return "unknown";
        }

        static IReadOnlyList<string> ReadFeatureList(string dir)
        {
            string path = Path.Combine(dir, "feature_list.json");
            if (!File.Exists(path))
                return Array.Empty<string>();

            try
            {
                return ReadJsonStringArray(File.ReadAllText(path), "features");
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        static double ReadRecommendedThreshold(string dir)
        {
            string path = Path.Combine(dir, "thresholds.json");
            if (!File.Exists(path))
                return 0.50;

            try
            {
                string json = File.ReadAllText(path);
                double? t = ReadNestedDouble(json, "recommended", "threshold");
                if (t.HasValue)
                    return t.Value;
                t = ReadNestedDouble(json, "best_by_profit_factor", "threshold");
                if (t.HasValue)
                    return t.Value;
            }
            catch { /* optional */ }

            return 0.50;
        }

        static string ReadCalibrationMethod(string dir)
        {
            string path = Path.Combine(dir, "calibration.json");
            if (!File.Exists(path))
                return "none";

            try
            {
                return ReadJsonString(File.ReadAllText(path), "method") ?? "none";
            }
            catch { /* optional */ }

            return "none";
        }

        static Dictionary<string, object> ReadJsonFile(string path)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return dict;

            try
            {
                string json = File.ReadAllText(path);
                foreach (Match m in Regex.Matches(json, "\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\""))
                    dict[m.Groups[1].Value] = m.Groups[2].Value;
            }
            catch { /* optional */ }

            return dict;
        }

        static string? ReadJsonString(string json, string key)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"",
                RegexOptions.CultureInvariant);
            return m.Success ? m.Groups[1].Value : null;
        }

        static IReadOnlyList<string> ReadJsonStringArray(string json, string key)
        {
            int keyIdx = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyIdx < 0)
                return Array.Empty<string>();

            int arrStart = json.IndexOf('[', keyIdx);
            int arrEnd = json.IndexOf(']', arrStart);
            if (arrStart < 0 || arrEnd < arrStart)
                return Array.Empty<string>();

            var list = new List<string>();
            foreach (Match m in Regex.Matches(json.Substring(arrStart, arrEnd - arrStart + 1), "\"([^\"]+)\""))
                list.Add(m.Groups[1].Value);
            return list;
        }

        static double? ReadNestedDouble(string json, string parentKey, string childKey)
        {
            int parentIdx = json.IndexOf("\"" + parentKey + "\"", StringComparison.Ordinal);
            if (parentIdx < 0)
                return null;

            int braceStart = json.IndexOf('{', parentIdx);
            if (braceStart < 0)
                return null;

            int depth = 0;
            int braceEnd = -1;
            for (int i = braceStart; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        braceEnd = i;
                        break;
                    }
                }
            }

            if (braceEnd < braceStart)
                return null;

            string block = json.Substring(braceStart, braceEnd - braceStart + 1);
            var m = Regex.Match(block,
                "\"" + Regex.Escape(childKey) + "\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)?)",
                RegexOptions.CultureInvariant);
            if (!m.Success)
                return null;

            return double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
