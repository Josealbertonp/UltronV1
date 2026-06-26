using System;
using System.IO;
using System.Text;
using global::UltronV1.Core.Domain;
using global::UltronV1.Core.Engine;
using global::UltronV1.Core.Signals;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>
    /// Read-only pipeline funnel + rejection counters for UltronV1Paper (one sample per bar).
    /// Does not alter evaluation — observes <see cref="BarEvaluationResult"/> only.
    /// </summary>
    public sealed class UltronPipelineDiagnostics
    {
        const int SummaryIntervalBars = 10;

        public int BarsProcessed { get; private set; }
        public int Candidates { get; private set; }
        public int Accepted { get; private set; }
        public int SignalsGenerated { get; private set; }
        public int SignalsPassedScore { get; private set; }
        public int SignalsPassedHardGates { get; private set; }
        public int SignalsPassedOrderFlow { get; private set; }
        public int EntriesSubmitted { get; private set; }

        public int RejectedTrendAcceleration { get; private set; }
        public int RejectedER { get; private set; }
        public int RejectedADX { get; private set; }
        public int RejectedEMA { get; private set; }
        public int RejectedRVOL { get; private set; }
        public int RejectedNoSweep { get; private set; }
        public int RejectedNoSweepNoDelta { get; private set; }
        public int RejectedChop { get; private set; }
        public int RejectedWyckoff { get; private set; }
        public int RejectedSession { get; private set; }
        public int RejectedCooldown { get; private set; }
        public int RejectedAlreadyInPosition { get; private set; }
        public int RejectedAnyOtherGate { get; private set; }

        public string LastReject { get; private set; } = string.Empty;

        public string? LogFilePath { get; private set; }

        StreamWriter? _logWriter;

        public void OpenLog(string directory, string fileStem)
        {
            CloseLog();
            Directory.CreateDirectory(directory);
            LogFilePath = Path.Combine(directory, fileStem + ".log");
            _logWriter = new StreamWriter(LogFilePath, append: false, Encoding.UTF8)
            {
                AutoFlush = true
            };
            _logWriter.WriteLine("# UltronV1Paper pipeline diagnostics — {0:O}", DateTime.Now);
            _logWriter.WriteLine();
        }

        public void CloseLog()
        {
            _logWriter?.Dispose();
            _logWriter = null;
        }

        public void RecordBar(
            BarEvaluationResult result,
            UltronConfig cfg,
            SignalTier minTierForEntry,
            bool tradeCycleOpen)
        {
            BarsProcessed++;

            var gates = result.Gates;
            var score = result.Score;
            var snap = result.Snapshot;
            var ex = result.Explanation;

            bool passedHardGates = gates.LongAllowed || gates.ShortAllowed;
            if (passedHardGates)
                SignalsPassedHardGates++;

            int longMin = snap.FreshBreakoutLong ? cfg.FreshBreakoutMinScore : cfg.ScoreMinEntry;
            int shortMin = snap.FreshBreakoutShort ? cfg.FreshBreakoutMinScore : cfg.ScoreMinEntry;
            bool longElig = gates.LongAllowed && score.LongScore >= longMin;
            bool shortElig = gates.ShortAllowed && score.ShortScore >= shortMin;
            bool passedScore = longElig || shortElig;

            if (passedScore)
            {
                SignalsPassedScore++;
                Candidates++;
            }

            if (result.Signal != null)
            {
                SignalsGenerated++;
                if (cfg.RequireOrderFlowConfirmation)
                    SignalsPassedOrderFlow++;
                else
                    SignalsPassedOrderFlow++;
                PrintSummary();
            }

            if (ex.Accepted && result.Signal != null)
                Accepted++;

            string? rejectKey = ClassifyLastReject(result, cfg, minTierForEntry, tradeCycleOpen);
            if (rejectKey != null)
            {
                LastReject = rejectKey;
                IncrementReject(rejectKey);
            }

            if (BarsProcessed % SummaryIntervalBars == 0)
                PrintSummary();
        }

        public void RecordEntrySubmitted()
        {
            EntriesSubmitted++;
            PrintSummary();
        }

        public void PrintSummary()
        {
            if (_logWriter == null)
                return;

            var sb = new StringBuilder();
            sb.AppendLine("--- " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ---");
            sb.AppendLine("==========================");
            sb.AppendLine("ULTRON DIAGNOSTICS");
            sb.AppendLine("==========================");
            sb.AppendLine();
            sb.AppendFormat("Bars Processed: {0}", BarsProcessed).AppendLine();
            sb.AppendLine();
            sb.AppendFormat("Signals Generated: {0}", SignalsGenerated).AppendLine();
            sb.AppendFormat("Passed Score: {0}", SignalsPassedScore).AppendLine();
            sb.AppendFormat("Passed Hard Gates: {0}", SignalsPassedHardGates).AppendLine();
            sb.AppendFormat("Passed OrderFlow: {0}", SignalsPassedOrderFlow).AppendLine();
            sb.AppendLine();
            sb.AppendFormat("Candidates: {0}", Candidates).AppendLine();
            sb.AppendFormat("Accepted (evaluator): {0}", Accepted).AppendLine();
            sb.AppendFormat("Entries Submitted: {0}", EntriesSubmitted).AppendLine();
            sb.AppendLine();
            sb.AppendLine("Rejected:");
            sb.AppendFormat("TrendAcceleration: {0}", RejectedTrendAcceleration).AppendLine();
            sb.AppendFormat("ER: {0}", RejectedER).AppendLine();
            sb.AppendFormat("ADX: {0}", RejectedADX).AppendLine();
            sb.AppendFormat("EMA: {0}", RejectedEMA).AppendLine();
            sb.AppendFormat("RVOL: {0}", RejectedRVOL).AppendLine();
            sb.AppendFormat("NoSweep: {0}", RejectedNoSweep).AppendLine();
            sb.AppendFormat("NoSweepNoDelta: {0}", RejectedNoSweepNoDelta).AppendLine();
            sb.AppendFormat("Chop: {0}", RejectedChop).AppendLine();
            sb.AppendFormat("Wyckoff: {0}", RejectedWyckoff).AppendLine();
            sb.AppendFormat("Cooldown: {0}", RejectedCooldown).AppendLine();
            sb.AppendFormat("AlreadyInPosition: {0}", RejectedAlreadyInPosition).AppendLine();
            sb.AppendFormat("Session: {0}", RejectedSession).AppendLine();
            sb.AppendFormat("AnyOtherGate: {0}", RejectedAnyOtherGate).AppendLine();
            if (!string.IsNullOrEmpty(LastReject))
                sb.AppendFormat("LastReject: {0}", LastReject).AppendLine();
            sb.AppendLine("==========================");
            _logWriter.Write(sb.ToString());
            _logWriter.WriteLine();
        }

        static string? ClassifyLastReject(
            BarEvaluationResult result,
            UltronConfig cfg,
            SignalTier minTierForEntry,
            bool tradeCycleOpen)
        {
            var ex = result.Explanation;
            var signal = result.Signal;

            if (ex.Accepted && signal != null)
            {
                if (tradeCycleOpen)
                    return "AlreadyInPosition";
                if (signal.Tier < minTierForEntry)
                    return "AnyOtherGate";
                return null;
            }

            switch (ex.RejectionStage)
            {
                case RejectionStage.HardGate:
                    return MapHardGateReject(ex);
                case RejectionStage.OrderFlow:
                    return MapOrderFlowReject(ex.RejectionSummary);
                case RejectionStage.Score:
                    return MapScoreReject(ex.RejectionSummary);
                default:
                    return "AnyOtherGate";
            }
        }

        static string MapHardGateReject(SignalExplanation ex)
        {
            string summary = ex.RejectionSummary ?? string.Empty;

            if (Contains(summary, "TrendAcceleration"))
                return "TrendAcceleration";
            if (Contains(summary, "ER=") || GateFailed(ex, "ER"))
                return "ER";
            if (Contains(summary, "ADX=") || GateFailed(ex, "ADX"))
                return "ADX";
            if (Contains(summary, "EMA") || GateFailed(ex, "EMA"))
                return "EMA";
            if (Contains(summary, "ChopFlips") || Contains(summary, "Chop") || GateFailed(ex, "ChopFilter"))
                return "Chop";
            if (Contains(summary, "Wyckoff") || GateFailed(ex, "Wyckoff"))
                return "Wyckoff";
            if (Contains(summary, "RVOL") || Contains(summary, "Rvol"))
                return "RVOL";
            if (Contains(summary, "Session"))
                return "Session";
            if (Contains(summary, "Cooldown"))
                return "Cooldown";

            return "AnyOtherGate";
        }

        static string MapOrderFlowReject(string summary)
        {
            if (string.IsNullOrEmpty(summary))
                return "AnyOtherGate";

            switch (summary)
            {
                case "NoSweep":
                    return "NoSweep";
                case "NoSweepNoDelta":
                    return "NoSweepNoDelta";
                default:
                    if (Contains(summary, "RVOL") || Contains(summary, "Rvol"))
                        return "RVOL";
                    return "AnyOtherGate";
            }
        }

        static string MapScoreReject(string summary)
        {
            if (string.IsNullOrEmpty(summary))
                return "AnyOtherGate";

            if (Contains(summary, "RVOL") || Contains(summary, "Rvol"))
                return "RVOL";
            if (Contains(summary, "Session"))
                return "Session";
            if (Contains(summary, "Cooldown"))
                return "Cooldown";

            return "AnyOtherGate";
        }

        static bool GateFailed(SignalExplanation ex, string gateName)
        {
            foreach (var g in ex.GateRejections)
            {
                if (g.GateName == gateName && (g.BlocksLong || g.BlocksShort))
                    return true;
            }
            return false;
        }

        static bool Contains(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        void IncrementReject(string key)
        {
            switch (key)
            {
                case "TrendAcceleration": RejectedTrendAcceleration++; break;
                case "ER": RejectedER++; break;
                case "ADX": RejectedADX++; break;
                case "EMA": RejectedEMA++; break;
                case "RVOL": RejectedRVOL++; break;
                case "NoSweep": RejectedNoSweep++; break;
                case "NoSweepNoDelta": RejectedNoSweepNoDelta++; break;
                case "Chop": RejectedChop++; break;
                case "Wyckoff": RejectedWyckoff++; break;
                case "Session": RejectedSession++; break;
                case "Cooldown": RejectedCooldown++; break;
                case "AlreadyInPosition": RejectedAlreadyInPosition++; break;
                default: RejectedAnyOtherGate++; break;
            }
        }
    }
}
