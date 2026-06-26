namespace UltronML
{
    /// <summary>Unified ML host configuration — Shadow + Safe Filter (Phase 4B).</summary>
    public sealed class MlHostConfig
    {
        public MlFilterMode Mode { get; set; } = MlFilterMode.Shadow;

        /// <summary>Legacy flag — when true, enables ML in Shadow observe mode.</summary>
        public bool EnableShadowMl { get; set; } = false;

        /// <summary>When true with Mode=Active, ML may block low-confidence entries.</summary>
        public bool EnableMlFilter { get; set; } = false;

        /// <summary>Emergency rollback — all trades pass even in Active mode.</summary>
        public bool ForcePassThrough { get; set; } = false;

        public string ModelDirectory { get; set; } = string.Empty;

        /// <summary>null = auto from thresholds.json; otherwise manual override.</summary>
        public double? ThresholdOverride { get; set; }

        public bool ShouldRunInference =>
            Mode != MlFilterMode.Disabled || EnableShadowMl;

        public bool CanBlock =>
            Mode == MlFilterMode.Active
            && EnableMlFilter
            && !ForcePassThrough;
    }
}
