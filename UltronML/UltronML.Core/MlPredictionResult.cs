namespace UltronML
{
    public sealed class MlPredictionResult
    {
        public static MlPredictionResult PassThrough { get; } = new MlPredictionResult
        {
            Active = false,
            Confidence = 1.0,
            ProviderName = "Dummy",
            ModelVersion = "none"
        };

        public bool Active { get; set; }
        public double Confidence { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string ModelVersion { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public double LatencyMs { get; set; }
        public double Threshold { get; set; } = 0.50;
        public string Decision { get; set; } = MlDecision.PassThrough;

        public bool PassesThreshold(double minConfidence) => !Active || Confidence >= minConfidence;
    }
}
