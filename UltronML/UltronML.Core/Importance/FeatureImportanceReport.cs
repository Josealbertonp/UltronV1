using System;
using System.Collections.Generic;

namespace UltronML.Importance
{
    /// <summary>Feature importance container — calculation deferred to training phase.</summary>
    public sealed class FeatureImportanceReport
    {
        public string ModelVersion { get; set; } = "none";
        public string Method { get; set; } = "pending";
        public string Status { get; set; } = "placeholder";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public List<FeatureImportanceEntry> Entries { get; set; } = new List<FeatureImportanceEntry>();
    }

    public sealed class FeatureImportanceEntry
    {
        public string Feature { get; set; } = string.Empty;
        public double Importance { get; set; }
        public int Rank { get; set; }
        public string Method { get; set; } = "pending";
    }
}
