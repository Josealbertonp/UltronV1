namespace UltronV1.Core.Engine
{
    /// <summary>Standard action tokens for structured Ultron logs (Validation Framework / log pipelines).</summary>
    public static class UltronLogActions
    {
        public const string FallbackHeuristic = "fallback_heuristic";
        public const string DisableOnnx = "disable_onnx";
        public const string Propagate = "propagate";
        public const string MlInactive = "ml_inactive";
        public const string SkipOutcome = "skip_outcome";
        public const string UseEmptyWeights = "use_empty_weights";
    }
}
