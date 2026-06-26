using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UltronV1.Core.Dataset;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Writes shadow_predictions.jsonl — observational ML telemetry only.</summary>
    public sealed class UltronShadowPredictionSink : IDisposable
    {
        readonly object _lock = new object();
        readonly Dictionary<string, ShadowPendingRecord> _pending = new Dictionary<string, ShadowPendingRecord>();
        StreamWriter? _writer;
        string? _filePath;

        public string? FilePath => _filePath;

        public void Open(string sessionDirectory)
        {
            Close();
            Directory.CreateDirectory(sessionDirectory);
            _filePath = Path.Combine(sessionDirectory, "shadow_predictions.jsonl");
            _writer = new StreamWriter(_filePath, append: false, Encoding.UTF8)
            {
                AutoFlush = true
            };
        }

        public void CaptureEntry(ShadowPendingRecord record)
        {
            if (_writer == null || string.IsNullOrEmpty(record.TradeId))
                return;

            lock (_lock)
            {
                _pending[record.TradeId] = record;
            }
        }

        public void EnrichOutcome(TradeOutcome outcome, string? atmLabel)
        {
            if (_writer == null || string.IsNullOrEmpty(outcome.SignalId))
                return;

            lock (_lock)
            {
                if (!_pending.TryGetValue(outcome.SignalId, out ShadowPendingRecord? pending))
                    return;

                pending.OutcomeRecorded = true;
                pending.IsWin = outcome.IsWin;
                pending.IsLoss = !outcome.IsWin && outcome.RealizedPnLTicks < 0;
                pending.MfeTicks = outcome.MfeTicks;
                pending.MaeTicks = outcome.MaeTicks;
                pending.PnlTicks = outcome.RealizedPnLTicks;
                pending.Atm = atmLabel ?? string.Empty;

                _writer!.WriteLine(Serialize(pending));
                _pending.Remove(outcome.SignalId);
            }
        }

        public void MarkBlocked(string tradeId)
        {
            if (_writer == null || string.IsNullOrEmpty(tradeId))
                return;

            lock (_lock)
            {
                if (!_pending.TryGetValue(tradeId, out ShadowPendingRecord? pending))
                    return;

                pending.EntryAllowed = false;
                pending.Decision = "BLOCK";
                _writer!.WriteLine(Serialize(pending));
                _pending.Remove(tradeId);
            }
        }

        public void FlushPendingWithoutOutcome()
        {
            if (_writer == null)
                return;

            lock (_lock)
            {
                foreach (var kv in _pending)
                    _writer.WriteLine(Serialize(kv.Value));
                _pending.Clear();
            }
        }

        static string Serialize(ShadowPendingRecord r)
        {
            var sb = new StringBuilder(1024);
            sb.Append('{');
            AppendStr(sb, "tradeId", r.TradeId);
            sb.Append(',');
            AppendStr(sb, "timestampUtc", r.TimestampUtc);
            sb.Append(',');
            AppendNum(sb, "score", r.Score);
            sb.Append(',');
            AppendStr(sb, "tier", r.Tier);
            sb.Append(',');
            AppendStr(sb, "direction", r.Direction);
            sb.Append(',');
            AppendNum(sb, "confidence", r.Confidence);
            sb.Append(',');
            AppendNum(sb, "threshold", r.Threshold);
            sb.Append(',');
            AppendStr(sb, "modelVersion", r.ModelVersion);
            sb.Append(',');
            AppendNum(sb, "inferenceTimeMs", r.InferenceTimeMs);
            sb.Append(',');
            AppendStr(sb, "decision", r.Decision);
            sb.Append(',');
            AppendStr(sb, "filterMode", r.FilterMode);
            sb.Append(',');
            AppendStr(sb, "filterReason", r.FilterReason);
            sb.Append(',');
            AppendBool(sb, "entryAllowed", r.EntryAllowed);
            sb.Append(',');
            AppendBool(sb, "wouldBlockInActive", r.WouldBlockInActive);
            sb.Append(',');
            AppendStr(sb, "atm", r.Atm);
            sb.Append(',');
            AppendBool(sb, "outcomeRecorded", r.OutcomeRecorded);
            sb.Append(',');
            AppendBool(sb, "win", r.IsWin);
            sb.Append(',');
            AppendBool(sb, "loss", r.IsLoss);
            sb.Append(',');
            AppendNum(sb, "mfeTicks", r.MfeTicks);
            sb.Append(',');
            AppendNum(sb, "maeTicks", r.MaeTicks);
            sb.Append(',');
            AppendNum(sb, "pnlTicks", r.PnlTicks);
            sb.Append(',');
            AppendFeatures(sb, r.Features);
            sb.Append('}');
            return sb.ToString();
        }

        static void AppendFeatures(StringBuilder sb, float[] features)
        {
            sb.Append("\"features\":[");
            for (int i = 0; i < features.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(features[i].ToString(CultureInfo.InvariantCulture));
            }
            sb.Append(']');
        }

        static void AppendStr(StringBuilder sb, string key, string? value)
        {
            sb.Append('"').Append(key).Append("\":");
            if (value == null)
                sb.Append("null");
            else
                sb.Append('"').Append(Escape(value)).Append('"');
        }

        static void AppendNum(StringBuilder sb, string key, double value)
        {
            sb.Append('"').Append(key).Append("\":")
                .Append(value.ToString(CultureInfo.InvariantCulture));
        }

        static void AppendBool(StringBuilder sb, string key, bool value)
        {
            sb.Append('"').Append(key).Append("\":")
                .Append(value ? "true" : "false");
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        public void Close()
        {
            FlushPendingWithoutOutcome();
            _writer?.Dispose();
            _writer = null;
        }

        public void Dispose() => Close();
    }

    public sealed class ShadowPendingRecord
    {
        public string TradeId { get; set; } = string.Empty;
        public string TimestampUtc { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Tier { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public double Threshold { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public double InferenceTimeMs { get; set; }
        public string Decision { get; set; } = "PASS_THROUGH";
        public string FilterMode { get; set; } = string.Empty;
        public string FilterReason { get; set; } = string.Empty;
        public bool EntryAllowed { get; set; } = true;
        public bool WouldBlockInActive { get; set; }
        public string Atm { get; set; } = string.Empty;
        public float[] Features { get; set; } = Array.Empty<float>();
        public bool OutcomeRecorded { get; set; }
        public bool IsWin { get; set; }
        public bool IsLoss { get; set; }
        public double MfeTicks { get; set; }
        public double MaeTicks { get; set; }
        public double PnlTicks { get; set; }
    }
}
