namespace UltronML
{
    /// <summary>RC2 Phase 4B — ML filter operating mode.</summary>
    public enum MlFilterMode
    {
        /// <summary>No ML inference — pure Ultron (RC1).</summary>
        Disabled = 0,

        /// <summary>Inference + logging only — never blocks.</summary>
        Shadow = 1,

        /// <summary>ML may veto entries when EnableMlFilter is true.</summary>
        Active = 2
    }
}
