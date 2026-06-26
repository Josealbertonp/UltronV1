namespace UltronML
{
    /// <summary>Result of ML filter evaluation at entry gate (host layer).</summary>
    public sealed class MlFilterResult
    {
        public bool AllowEntry { get; set; } = true;
        public string Decision { get; set; } = MlDecision.PassThrough;
        public string Reason { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public MlPredictionResult Prediction { get; set; } = MlPredictionResult.PassThrough;

        /// <summary>What Active mode would have done (for shadow comparison).</summary>
        public bool WouldBlockInActive { get; set; }
        public string FilterMode { get; set; } = "Disabled";
    }
}
