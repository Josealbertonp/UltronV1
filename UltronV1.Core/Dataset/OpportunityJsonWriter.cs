using System;
using System.Globalization;
using System.IO;
using System.Text;
using UltronV1.Core.Features;

namespace UltronV1.Core.Dataset
{
    /// <summary>
    /// Zero-dependency JSON Lines serializer for OpportunityRecord and TradeOutcome.
    /// </summary>
    internal static class OpportunityJsonWriter
    {
        public static string SerializeOpportunity(OpportunityRecord r)
        {
            var sb = new StringBuilder(2048);
            sb.Append('{');

            AppendStr(sb, "signalId", r.SignalId);
            AppendStr(sb, "sessionId", r.SessionId);
            AppendStr(sb, "timestampUtc", r.TimestampUtc.ToString("o", CultureInfo.InvariantCulture));
            AppendInt(sb, "barIndex", r.BarIndex);
            AppendStr(sb, "instrument", r.Instrument);
            AppendNum(sb, "close", r.Close);

            sb.Append(",\"versions\":{");
            AppendStrInline(sb, "ultron", r.UltronVersion);
            AppendIntInline(sb, "featureSchema", r.FeatureSchemaVersion);
            AppendIntInline(sb, "score", r.ScoreVersion);
            AppendStrInline(sb, "config", r.ConfigVersion);
            AppendStrInline(sb, "scoreProfileHash", r.ScoreProfileHash);
            AppendIntInline(sb, "recordSchema", r.RecordSchemaVersion, last: true);
            sb.Append('}');

            AppendStr(sb, "archetype", r.Archetype.ToString());
            AppendStr(sb, "direction", r.Direction.ToString());
            AppendInt(sb, "score", r.Score);
            AppendInt(sb, "longScore", r.LongScore);
            AppendInt(sb, "shortScore", r.ShortScore);
            AppendStr(sb, "tier", r.Tier.ToString());
            AppendStr(sb, "regime", r.Regime.ToString());
            AppendStr(sb, "decision", r.Decision.ToString());
            AppendStr(sb, "rejectionStage", r.RejectionStage.ToString());
            AppendStr(sb, "rejectionReason", r.RejectionReason);

            sb.Append(",\"gates\":{");
            AppendBoolInline(sb, "longAllowed", r.GatesLongAllowed);
            AppendBoolInline(sb, "shortAllowed", r.GatesShortAllowed);
            AppendStrInline(sb, "blockReason", r.GatesBlockReason);
            sb.Append(",\"checks\":[");
            for (int i = 0; i < r.GateChecks.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var g = r.GateChecks[i];
                sb.Append('{');
                AppendStrInline(sb, "name", g.Name);
                AppendBoolInline(sb, "longPass", g.LongPass);
                AppendBoolInline(sb, "shortPass", g.ShortPass);
                AppendStrInline(sb, "detail", g.Detail, last: true);
                sb.Append('}');
            }
            sb.Append("]}");

            sb.Append(",\"orderFlow\":{");
            AppendBoolInline(sb, "passed", r.OrderFlowPassed);
            AppendStrInline(sb, "reason", r.OrderFlowReason);
            AppendStrInline(sb, "detail", r.OrderFlowDetail, last: true);
            sb.Append('}');

            sb.Append(",\"features\":[");
            for (int i = 0; i < r.Features.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(r.Features[i].ToString(CultureInfo.InvariantCulture));
            }
            sb.Append(']');

            sb.Append(",\"featureNames\":[");
            AppendFeatureNames(sb);
            sb.Append(']');

            sb.Append(",\"scoreBreakdown\":[");
            for (int i = 0; i < r.ScoreBreakdown.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var s = r.ScoreBreakdown[i];
                sb.Append('{');
                AppendStrInline(sb, "name", s.Name);
                AppendIntInline(sb, "points", s.Points);
                AppendBoolInline(sb, "applied", s.Applied, last: true);
                sb.Append('}');
            }
            sb.Append(']');

            AppendStr(sb, "explainText", r.ExplainText);

            sb.Append(",\"shadow\":{");
            AppendBoolInline(sb, "rvolLowWouldBlock", r.Shadow.RvolLowWouldBlock);
            AppendNumInline(sb, "rvol", r.Shadow.RvolAtEvaluation);
            sb.Append(",\"flags\":[");
            for (int i = 0; i < r.Shadow.Flags.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Escape(r.Shadow.Flags[i])).Append('"');
            }
            sb.Append("]}");

            AppendBool(sb, "outcomeLinked", r.OutcomeLinked, last: true);
            sb.Append('}');
            return sb.ToString();
        }

        public static string SerializeOutcome(TradeOutcome o)
        {
            var sb = new StringBuilder(768);
            sb.Append('{');
            AppendStr(sb, "signalId", o.SignalId);
            AppendStr(sb, "direction", o.Direction.ToString());
            AppendStr(sb, "entryAtUtc", o.EntryAtUtc.ToString("o", CultureInfo.InvariantCulture));
            AppendStr(sb, "closedAtUtc", o.ClosedAtUtc.ToString("o", CultureInfo.InvariantCulture));
            AppendNum(sb, "entryPrice", o.EntryPrice);
            AppendNum(sb, "exitPrice", o.ExitPrice);
            AppendNum(sb, "stopPrice", o.StopPrice);
            AppendNum(sb, "targetPrice", o.TargetPrice);
            AppendBool(sb, "isWin", o.IsWin);
            AppendNum(sb, "mfeTicks", o.MfeTicks);
            AppendNum(sb, "maeTicks", o.MaeTicks);
            AppendNum(sb, "rMultiple", o.RMultiple);
            AppendNum(sb, "realizedPnLTicks", o.RealizedPnLTicks);
            AppendNum(sb, "realizedPnLUsd", o.RealizedPnLUsd);
            AppendInt(sb, "barsInTrade", o.BarsInTrade);
            AppendInt(sb, "secondsInTrade", o.SecondsInTrade);
            AppendStr(sb, "exitReason", o.ExitReason);
            AppendStr(sb, "executionMode", o.ExecutionMode);
            AppendInt(sb, "label", o.Label, last: true);
            sb.Append('}');
            return sb.ToString();
        }

        public static string SerializeManifest(DatasetManifest m)
        {
            var sb = new StringBuilder(1024);
            sb.Append('{');
            AppendStr(sb, "sessionId", m.SessionId);
            AppendStr(sb, "instrument", m.Instrument);
            AppendStr(sb, "startedAtUtc", m.StartedAtUtc.ToString("o", CultureInfo.InvariantCulture));
            AppendStr(sb, "ultronVersion", m.UltronVersion);
            AppendInt(sb, "featureSchemaVersion", m.FeatureSchemaVersion);
            AppendInt(sb, "scoreVersion", m.ScoreVersion);
            AppendStr(sb, "scoreProfileHash", m.ScoreProfileHash);
            AppendStr(sb, "configVersion", m.ConfigVersion);
            AppendInt(sb, "recordSchemaVersion", m.RecordSchemaVersion);
            AppendInt(sb, "opportunityCount", m.OpportunityCount);
            AppendInt(sb, "acceptedCount", m.AcceptedCount);
            AppendInt(sb, "outcomeCount", m.OutcomeCount);
            AppendStr(sb, "opportunitiesFile", m.OpportunitiesFile);
            AppendStr(sb, "outcomesFile", m.OutcomesFile, last: true);
            sb.Append('}');
            return sb.ToString();
        }

        static void AppendFeatureNames(StringBuilder sb)
        {
            string[] names = FeatureSchema.FeatureNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Escape(names[i])).Append('"');
            }
        }

        static void AppendStr(StringBuilder sb, string key, string value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');
            if (!last) sb.Append(',');
        }

        static void AppendStrInline(StringBuilder sb, string key, string value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":\"").Append(Escape(value)).Append('"');
            if (!last) sb.Append(',');
        }

        static void AppendInt(StringBuilder sb, string key, int value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":").Append(value);
            if (!last) sb.Append(',');
        }

        static void AppendIntInline(StringBuilder sb, string key, int value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":").Append(value);
            if (!last) sb.Append(',');
        }

        static void AppendBool(StringBuilder sb, string key, bool value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");
            if (!last) sb.Append(',');
        }

        static void AppendBoolInline(StringBuilder sb, string key, bool value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");
            if (!last) sb.Append(',');
        }

        static void AppendNum(StringBuilder sb, string key, double value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
            if (!last) sb.Append(',');
        }

        static void AppendNumInline(StringBuilder sb, string key, double value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
            if (!last) sb.Append(',');
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
