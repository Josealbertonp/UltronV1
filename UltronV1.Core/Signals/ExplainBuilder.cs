using System.Collections.Generic;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Gates;
using UltronV1.Core.Scoring;

namespace UltronV1.Core.Signals
{
    internal static class ExplainBuilder
    {
        public static void PopulateGateRejections(GateEvaluationResult gates, SignalExplanation explain)
        {
            explain.GateRejections.Clear();
            foreach (var check in gates.Checks)
            {
                if (check.LongPass && check.ShortPass)
                    continue;

                string reason = string.IsNullOrEmpty(check.Detail) ? gates.BlockReason : check.Detail;
                explain.GateRejections.Add(new GateRejection(
                    check.Name,
                    reason,
                    !check.LongPass,
                    !check.ShortPass));
            }
        }

        public static void PopulateContributions(
            ScoreBreakdown score,
            SignalExplanation explain,
            TradeDirection side)
        {
            explain.LongContributions.Clear();
            explain.ShortContributions.Clear();

            foreach (var line in score.LongLines)
                explain.LongContributions.Add(line);
            foreach (var line in score.ShortLines)
                explain.ShortContributions.Add(line);
        }

        public static MarketRegime ClassifyRegime(BarSnapshot snap, UltronConfig cfg)
        {
            if (snap.EfficiencyRatio < cfg.ErChopThreshold || snap.ChopFlips > cfg.ChopMaxFlips)
                return MarketRegime.Chop;
            if (snap.Indicators.Adx >= cfg.AdxMin)
                return MarketRegime.Trending;
            if (snap.Indicators.Atr > 0 && snap.Rvol < cfg.MinRvol * 0.5)
                return MarketRegime.Quiet;
            return MarketRegime.Unknown;
        }

        public static SignalTier ClassifyTier(int score, bool orderFlowConfirmed, UltronConfig cfg)
        {
            if (!orderFlowConfirmed)
                return score >= cfg.ScoreMinEntry ? SignalTier.C : SignalTier.None;

            if (score >= cfg.TierAThreshold)
                return SignalTier.A;
            if (score >= cfg.TierBThreshold)
                return SignalTier.B;
            if (score >= cfg.ScoreMinEntry)
                return SignalTier.C;
            return SignalTier.None;
        }
    }
}
