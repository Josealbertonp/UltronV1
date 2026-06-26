using System.Collections.Generic;
using UltronV1.Core.Archetypes;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Gates;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Scoring
{
    public sealed class ScoreBreakdown
    {
        public int LongScore { get; internal set; }
        public int ShortScore { get; internal set; }
        public Dictionary<string, int> LongFactors { get; } = new Dictionary<string, int>(24);
        public Dictionary<string, int> ShortFactors { get; } = new Dictionary<string, int>(24);
        public List<ScoreContribution> LongLines { get; } = new List<ScoreContribution>(24);
        public List<ScoreContribution> ShortLines { get; } = new List<ScoreContribution>(24);
    }

    /// <summary>
    /// Confluence scoring from BarSnapshot features — NR weights + order-flow elevation.
    /// </summary>
    public sealed class ConfluenceScorer
    {
        public ScoreBreakdown Score(
            BarSnapshot snap,
            GateEvaluationResult gates,
            ArchetypeEvaluation archetypes,
            UltronConfig cfg)
        {
            var bd = new ScoreBreakdown();
            var ind = snap.Indicators;
            var of = snap.OrderFlow;
            double px = snap.Close;
            double tick = MnqContractSpec.TickSize;

            bd.LongScore = ScoreSide(true, snap, gates.LongAllowed, archetypes, cfg, bd.LongFactors, bd.LongLines, px, tick, ind, of);
            bd.ShortScore = ScoreSide(false, snap, gates.ShortAllowed, archetypes, cfg, bd.ShortFactors, bd.ShortLines, px, tick, ind, of);
            return bd;
        }

        static int ScoreSide(
            bool isLong,
            BarSnapshot snap,
            bool sideAllowed,
            ArchetypeEvaluation arch,
            UltronConfig cfg,
            Dictionary<string, int> factors,
            List<ScoreContribution> lines,
            double px,
            double tick,
            IndicatorSnapshot ind,
            OrderFlowState of)
        {
            if (!sideAllowed)
                return 0;

            int total = 0;

            void Add(string name, int weight, bool apply)
            {
                lines.Add(new ScoreContribution(name, weight, apply));
                if (!apply) return;
                total += weight;
                factors[name] = weight;
            }

            bool trend = isLong ? snap.ContinuationLong : snap.ContinuationShort;
            Add("Trend", cfg.W_Trend, trend);
            Add("Pullback", cfg.W_Pullback, isLong ? snap.PullbackLong : snap.PullbackShort);
            Add("ADX", cfg.W_AdxStrength, ind.Adx >= cfg.AdxMin + 5);
            Add("RSI", cfg.W_RsiMomentum, isLong
                ? ind.Rsi > 50 && ind.Rsi <= cfg.RsiOb
                : ind.Rsi < 50 && ind.Rsi >= cfg.RsiOs);
            Add("VWAP", cfg.W_VwapAlign, ind.Vwap > 0 && ind.VwapBarCount >= 10 && (isLong ? px > ind.Vwap : px < ind.Vwap));

            double spreadTk = System.Math.Abs(ind.EmaFast - ind.EmaSlow) / tick;
            Add("EMASpread", cfg.W_EmaSpread, spreadTk >= cfg.EmaSpreadMinTicks * 1.5);
            Add("Volume", cfg.W_Volume, ind.VolSma <= 0 || snap.BarVolume >= ind.VolSma * 1.2);
            Add("ATR", cfg.W_AtrExpansion, ind.Atr > 0 && (ind.Atr / tick) > 20);

            bool fresh = isLong ? snap.FreshBreakoutLong : snap.FreshBreakoutShort;
            Add("Breakout", cfg.W_FreshBreakout, fresh && cfg.EnableFreshBreakout);
            Add("EMACross", cfg.W_EmaCrossRecent, isLong ? snap.RecentCrossLong : snap.RecentCrossShort);
            Add("MTF", cfg.W_MtfTrend, isLong ? ind.EmaMtf > ind.EmaMtfPrev : ind.EmaMtf < ind.EmaMtfPrev);

            Add("Delta", cfg.W_DeltaAlign, isLong ? of.Delta > 0 : of.Delta < 0);
            Add("Sweep", cfg.W_SweepAlign, of.SweepDetected && (
                isLong ? of.SweepDirection == TickSide.Buy : of.SweepDirection == TickSide.Sell));
            Add("Absorption", cfg.W_Absorption, of.AbsorptionDetected);
            Add("RVOL", cfg.W_Rvol, snap.Rvol >= cfg.MinRvol);
            Add("BidAsk", cfg.W_BidAskPressure, isLong
                ? of.BidAskPressureRatio > 1.0
                : of.BidAskPressureRatio < 1.0);

            bool hasTrigger = isLong ? arch.LongHasTrigger : arch.ShortHasTrigger;
            Add("Archetype", 5, hasTrigger);

            bool rsiChop = ind.Rsi > 40 && ind.Rsi < 60;
            Add("RSIChop", cfg.W_RsiChopPenalty, rsiChop);

            return total;
        }
    }
}
