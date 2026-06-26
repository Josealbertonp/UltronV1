using System;
using System.Collections.Generic;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Dataset
{
    public enum DatasetDecision : byte
    {
        Rejected = 0,
        Accepted = 1
    }

    /// <summary>
    /// Immutable opportunity record — single source of truth for ML, replay, audit and stats.
    /// </summary>
    public sealed class OpportunityRecord
    {
        public string SignalId { get; internal set; } = string.Empty;
        public string SessionId { get; internal set; } = string.Empty;
        public DateTime TimestampUtc { get; internal set; }
        public int BarIndex { get; internal set; }
        public string Instrument { get; internal set; } = string.Empty;
        public double Close { get; internal set; }

        // Versioning (required on every record)
        public string UltronVersion { get; internal set; } = UltronVersions.UltronVersion;
        public int FeatureSchemaVersion { get; internal set; } = UltronVersions.FeatureSchemaVersion;
        public int ScoreVersion { get; internal set; } = UltronVersions.ScoreVersion;
        public string ScoreProfileHash { get; internal set; } = string.Empty;
        public string ConfigVersion { get; internal set; } = UltronVersions.DefaultConfigVersion;
        public int RecordSchemaVersion { get; internal set; } = UltronVersions.DatasetRecordSchemaVersion;

        public EntryArchetype Archetype { get; internal set; }
        public TradeDirection Direction { get; internal set; }
        public int Score { get; internal set; }
        public int LongScore { get; internal set; }
        public int ShortScore { get; internal set; }
        public SignalTier Tier { get; internal set; }
        public MarketRegime Regime { get; internal set; }

        public DatasetDecision Decision { get; internal set; }
        public RejectionStage RejectionStage { get; internal set; }
        public string RejectionReason { get; internal set; } = string.Empty;

        public bool GatesLongAllowed { get; internal set; }
        public bool GatesShortAllowed { get; internal set; }
        public string GatesBlockReason { get; internal set; } = string.Empty;
        public List<GateRecordEntry> GateChecks { get; } = new List<GateRecordEntry>(8);

        public bool OrderFlowPassed { get; internal set; }
        public string OrderFlowReason { get; internal set; } = string.Empty;
        public string OrderFlowDetail { get; internal set; } = string.Empty;

        public float[] Features { get; internal set; } = Array.Empty<float>();
        public List<ScoreBreakdownEntry> ScoreBreakdown { get; } = new List<ScoreBreakdownEntry>(24);
        public string ExplainText { get; internal set; } = string.Empty;

        public ShadowRecordEntry Shadow { get; internal set; } = new ShadowRecordEntry();

        /// <summary>True when trade outcome has been linked (outcomes.jsonl).</summary>
        public bool OutcomeLinked { get; internal set; }
    }

    public readonly struct GateRecordEntry
    {
        public string Name { get; }
        public bool LongPass { get; }
        public bool ShortPass { get; }
        public string Detail { get; }

        public GateRecordEntry(string name, bool longPass, bool shortPass, string detail)
        {
            Name = name;
            LongPass = longPass;
            ShortPass = shortPass;
            Detail = detail;
        }
    }

    public readonly struct ScoreBreakdownEntry
    {
        public string Name { get; }
        public int Points { get; }
        public bool Applied { get; }

        public ScoreBreakdownEntry(string name, int points, bool applied)
        {
            Name = name;
            Points = points;
            Applied = applied;
        }
    }

    public sealed class ShadowRecordEntry
    {
        public bool RvolLowWouldBlock { get; internal set; }
        public double RvolAtEvaluation { get; internal set; }
        public List<string> Flags { get; } = new List<string>(4);
    }
}
