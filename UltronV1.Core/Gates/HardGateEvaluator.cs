using UltronV1.Core.Domain;
using UltronV1.Core.Engine;

namespace UltronV1.Core.Gates
{
    /// <summary>
    /// Hard gates — NightRanger-aligned vetoes. Score mode: direction anchor + anti-chop always enforced.
    /// </summary>
    public sealed class HardGateEvaluator
    {
        public GateEvaluationResult Evaluate(BarSnapshot snap, UltronConfig cfg)
        {
            var result = new GateEvaluationResult();
            bool longOk = true;
            bool shortOk = true;
            string reason = string.Empty;

            double px = snap.Close;
            double emaF = snap.Indicators.EmaFast;
            double adx = snap.Indicators.Adx;

            // GATE 1 — EMA direction anchor (always)
            if (px <= emaF) { longOk = false; reason = "EMA(px<=emaF)"; }
            if (px >= emaF) { shortOk = false; reason = "EMA(px>=emaF)"; }
            if (snap.FreshBreakoutLong && px > emaF) longOk = true;
            if (snap.FreshBreakoutShort && px < emaF) shortOk = true;
            result.Checks.Add(new GateCheckResult("EMA", longOk, shortOk, $"px={px:F2} emaF={emaF:F2}"));

            if (!longOk && !shortOk)
                return Finish(result, false, false, reason);

            // GATE 2 — ER chop (pre-signal)
            if (cfg.EnableErFilter && snap.EfficiencyRatio < cfg.ErChopThreshold)
            {
                longOk = false;
                shortOk = false;
                reason = $"ER={snap.EfficiencyRatio:F3}<{cfg.ErChopThreshold}";
                result.Checks.Add(new GateCheckResult("ER", longOk, shortOk, reason));
                return Finish(result, longOk, shortOk, reason);
            }
            result.Checks.Add(new GateCheckResult("ER", longOk, shortOk, $"ER={snap.EfficiencyRatio:F3}"));

            // GATE 3 — ADX minimum (always, fresh breakout bypass)
            if (adx < cfg.AdxMin)
            {
                if (!(snap.FreshBreakoutLong && longOk)) longOk = false;
                if (!(snap.FreshBreakoutShort && shortOk)) shortOk = false;
                reason = $"ADX={adx:F1}<{cfg.AdxMin}";
            }
            result.Checks.Add(new GateCheckResult("ADX", longOk, shortOk, reason));

            if (!longOk && !shortOk)
                return Finish(result, longOk, shortOk, reason);

            // GATE 4 — Trend acceleration
            if (cfg.RequireTrendAcceleration)
            {
                bool adxRising = (adx - snap.Indicators.AdxBarsAgo) >= cfg.AccelMinAdxDelta;
                bool erRising = (snap.EfficiencyRatio - snap.EfficiencyRatioPrev) >= cfg.AccelMinErDelta;
                if (!(adxRising && erRising))
                {
                    longOk = false;
                    shortOk = false;
                    reason = "TrendAcceleration";
                }
                result.Checks.Add(new GateCheckResult("TrendAcceleration", longOk, shortOk, reason));
                if (!longOk && !shortOk)
                    return Finish(result, longOk, shortOk, reason);
            }

            // GATE 5 — Chop filter
            if (cfg.RequireChopFilter && snap.ChopFlips > cfg.ChopMaxFlips)
            {
                longOk = false;
                shortOk = false;
                reason = $"ChopFlips={snap.ChopFlips}>{cfg.ChopMaxFlips}";
                result.Checks.Add(new GateCheckResult("ChopFilter", longOk, shortOk, reason));
                return Finish(result, longOk, shortOk, reason);
            }
            result.Checks.Add(new GateCheckResult("ChopFilter", longOk, shortOk, $"flips={snap.ChopFlips}"));

            // GATE 6 — Wyckoff anti-sweep
            if (cfg.RequireWyckoffFilter)
            {
                if (longOk && snap.WyckoffBlockLong) { longOk = false; reason = "WyckoffLong"; }
                if (shortOk && snap.WyckoffBlockShort) { shortOk = false; reason = "WyckoffShort"; }
                result.Checks.Add(new GateCheckResult("Wyckoff", longOk, shortOk, reason));
                if (!longOk && !shortOk)
                    return Finish(result, longOk, shortOk, reason);
            }

            return Finish(result, longOk, shortOk, reason);
        }

        static GateEvaluationResult Finish(GateEvaluationResult result, bool longOk, bool shortOk, string reason)
        {
            result.LongAllowed = longOk;
            result.ShortAllowed = shortOk;
            result.BlockReason = reason;
            return result;
        }
    }
}
