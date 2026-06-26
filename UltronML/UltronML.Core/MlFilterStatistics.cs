using System;
using System.Collections.Generic;
using System.Globalization;

namespace UltronML
{
    /// <summary>Runtime filter statistics — host layer only.</summary>
    public sealed class MlFilterStatistics
    {
        public int TradesPassed { get; private set; }
        public int TradesBlocked { get; private set; }
        public int FailSafePasses { get; private set; }

        readonly List<double> _passedPnl = new List<double>();
        readonly List<double> _blockedHypotheticalPnl = new List<double>();
        readonly List<bool> _blockedWouldWin = new List<bool>();
        int _trueBlocks;
        int _falseBlocks;

        public void RecordFilterDecision(MlFilterResult result, bool? wouldHaveWon = null, double? hypotheticalPnl = null)
        {
            if (result.AllowEntry)
            {
                TradesPassed++;
                if (result.Reason == "FailSafe")
                    FailSafePasses++;
            }
            else
            {
                TradesBlocked++;
                if (wouldHaveWon.HasValue)
                {
                    if (wouldHaveWon.Value)
                        _falseBlocks++;
                    else
                        _trueBlocks++;
                    _blockedWouldWin.Add(wouldHaveWon.Value);
                }
                if (hypotheticalPnl.HasValue)
                    _blockedHypotheticalPnl.Add(hypotheticalPnl.Value);
            }
        }

        public void RecordPassedOutcome(double pnlTicks) => _passedPnl.Add(pnlTicks);

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["tradesPassed"] = TradesPassed,
                ["tradesBlocked"] = TradesBlocked,
                ["failSafePasses"] = FailSafePasses,
                ["passedWinRate"] = Round(SafeWinRate(_passedPnl)),
                ["passedProfitFactor"] = Round(ProfitFactor(_passedPnl)),
                ["passedExpectancy"] = Round(Expectancy(_passedPnl)),
                ["blockedTrueBlocks"] = _trueBlocks,
                ["blockedFalseBlocks"] = _falseBlocks,
                ["filterPrecision"] = Round(Precision(_trueBlocks, _falseBlocks)),
                ["filterRecall"] = Round(Recall(_trueBlocks, _blockedWouldWin.Count)),
                ["passedTrades"] = _passedPnl.Count
            };
        }

        static double SafeWinRate(List<double> pnl)
        {
            if (pnl.Count == 0) return 0;
            int wins = 0;
            foreach (double p in pnl)
                if (p > 0) wins++;
            return (double)wins / pnl.Count;
        }

        static double ProfitFactor(List<double> pnl)
        {
            double gp = 0, gl = 0;
            foreach (double p in pnl)
            {
                if (p > 0) gp += p;
                else gl += Math.Abs(p);
            }
            return gl > 0 ? gp / gl : (gp > 0 ? double.PositiveInfinity : 0);
        }

        static double Expectancy(List<double> pnl)
        {
            if (pnl.Count == 0) return 0;
            double sum = 0;
            foreach (double p in pnl) sum += p;
            return sum / pnl.Count;
        }

        static double Precision(int trueBlocks, int falseBlocks)
        {
            int total = trueBlocks + falseBlocks;
            return total > 0 ? (double)trueBlocks / total : 0;
        }

        static double Recall(int trueBlocks, int totalLossesBlocked)
        {
            return totalLossesBlocked > 0 ? (double)trueBlocks / totalLossesBlocked : 0;
        }

        static object Round(double v) =>
            double.IsInfinity(v) ? "inf" : Math.Round(v, 4).ToString(CultureInfo.InvariantCulture);
    }
}
