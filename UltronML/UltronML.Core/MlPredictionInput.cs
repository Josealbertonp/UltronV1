using System.Collections.Generic;

namespace UltronML
{
    /// <summary>Feature vector + signal context for ML confidence prediction (inference).</summary>
    public sealed class MlPredictionInput
    {
        public IReadOnlyList<float> Features { get; set; } = new float[0];
        public int Score { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public int FeatureSchemaVersion { get; set; }
    }
}
