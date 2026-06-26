using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Dataset
{
    /// <summary>
    /// Evaluates shadow filters (never blocks operational path) for dataset / promotion research.
    /// </summary>
    public sealed class ShadowPipeline
    {
        public ShadowRecordEntry Evaluate(BarSnapshot snap, UltronConfig cfg)
        {
            var shadow = new ShadowRecordEntry
            {
                RvolAtEvaluation = snap.Rvol
            };

            if (snap.Rvol > 0 && snap.Rvol <= cfg.MinRvol)
            {
                shadow.RvolLowWouldBlock = true;
                shadow.Flags.Add($"RVOL<={cfg.MinRvol:F2}");
            }

            if (snap.EfficiencyRatio < cfg.ErChopThreshold)
                shadow.Flags.Add("ER_chop_shadow");

            if (snap.ChopFlips > cfg.ChopMaxFlips)
                shadow.Flags.Add("Chop_shadow");

            return shadow;
        }
    }
}
