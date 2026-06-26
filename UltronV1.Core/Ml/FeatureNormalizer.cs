using System;
using UltronV1.Core.Features;

namespace UltronV1.Core.Ml
{
    /// <summary>
    /// Prepares feature vector for ONNX input — validates schema version and count.
    /// </summary>
    public static class FeatureNormalizer
    {
        public static bool TryBuildInput(FeatureVector features, int expectedSchemaVersion, out float[] input, out string? error)
        {
            input = Array.Empty<float>();
            if (features == null)
            {
                error = "features null";
                return false;
            }

            if (expectedSchemaVersion != FeatureSchema.Version)
            {
                error = $"feature schema mismatch: model={expectedSchemaVersion} engine={FeatureSchema.Version}";
                return false;
            }

            var values = features.Values;
            if (values.Length != FeatureSchema.Count)
            {
                error = $"feature count mismatch: expected {FeatureSchema.Count}, got {values.Length}";
                return false;
            }

            input = new float[FeatureSchema.Count];
            Array.Copy(values, input, FeatureSchema.Count);
            error = null;
            return true;
        }
    }
}
