namespace UltronML
{
    /// <summary>
    /// RC2 ML confidence filter configuration. Default off preserves RC1 behavior.
    /// Lives in UltronML — not wired into UltronV1.Core in Phase 1.
    /// </summary>
    public sealed class MlFilterConfig
    {
        public bool EnableMlFilter { get; set; } = false;
        public double MinConfidence { get; set; } = 0.50;
        public string ModelDirectory { get; set; } = string.Empty;
        public string ProviderName { get; set; } = "Dummy";

        public bool IsActive => EnableMlFilter;
    }
}
