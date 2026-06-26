using System;

namespace UltronV1.Core.Dataset
{
    public sealed class DatasetManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public string Instrument { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
        public string UltronVersion { get; set; } = UltronVersions.UltronVersion;
        public int FeatureSchemaVersion { get; set; } = UltronVersions.FeatureSchemaVersion;
        public int ScoreVersion { get; set; } = UltronVersions.ScoreVersion;
        public string ScoreProfileHash { get; set; } = string.Empty;
        public string ConfigVersion { get; set; } = UltronVersions.DefaultConfigVersion;
        public int RecordSchemaVersion { get; set; } = UltronVersions.DatasetRecordSchemaVersion;
        public int OpportunityCount { get; set; }
        public int AcceptedCount { get; set; }
        public int OutcomeCount { get; set; }
        public string OpportunitiesFile { get; set; } = "opportunities.jsonl";
        public string OutcomesFile { get; set; } = "outcomes.jsonl";
    }
}
