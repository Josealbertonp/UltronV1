namespace UltronV1.Core.Ml
{
    /// <summary>
    /// Inference thresholds loaded from model config.json.
    /// </summary>
    public sealed class MlInferenceConfig
    {
        public string ModelId { get; set; } = string.Empty;
        public int FeatureSchemaVersion { get; set; } = 1;
        public bool AdvisoryOnly { get; set; } = true;
        public bool NeverVeto { get; set; } = true;
        public int MaxBoostPoints { get; set; } = 8;
        public int MaxPenaltyPoints { get; set; } = -6;
        public double BoostThreshold { get; set; } = 0.62;
        public double PenaltyThreshold { get; set; } = 0.45;
    }
}
