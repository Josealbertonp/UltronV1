namespace UltronML
{
    /// <summary>Backward-compatible alias for Phase 4A.</summary>
    public sealed class ShadowMlConfig
    {
        public bool EnableShadowMl { get; set; } = false;
        public string ModelDirectory { get; set; } = string.Empty;
        public bool EnableMlFilter { get; set; } = false;

        public MlHostConfig ToHostConfig() => new MlHostConfig
        {
            Mode = EnableShadowMl ? MlFilterMode.Shadow : MlFilterMode.Disabled,
            EnableShadowMl = EnableShadowMl,
            EnableMlFilter = EnableMlFilter,
            ModelDirectory = ModelDirectory
        };

        public static ShadowMlConfig FromHost(MlHostConfig host) => new ShadowMlConfig
        {
            EnableShadowMl = host.EnableShadowMl || host.Mode == MlFilterMode.Shadow,
            EnableMlFilter = host.EnableMlFilter,
            ModelDirectory = host.ModelDirectory
        };
    }
}
