using System.Collections.Generic;

namespace UltronV1.Core.Ml
{
    /// <summary>
    /// ML advisory output — boost/penalty only, never veto.
    /// </summary>
    public sealed class MlAdvisoryResult
    {
        public static MlAdvisoryResult Inactive { get; } = new MlAdvisoryResult();

        public bool Active { get; internal set; }
        public double WinProbability { get; internal set; }
        public int ScoreDelta { get; internal set; }
        public string Summary { get; internal set; } = string.Empty;
        public string ModelId { get; internal set; } = string.Empty;
        public List<MlFeatureContribution> TopContributions { get; } = new List<MlFeatureContribution>(5);

        public bool IsBoost => ScoreDelta > 0;
        public bool IsPenalty => ScoreDelta < 0;
    }

    public readonly struct MlFeatureContribution
    {
        public string FeatureName { get; }
        public float Value { get; }
        public double Weight { get; }

        public MlFeatureContribution(string featureName, float value, double weight)
        {
            FeatureName = featureName;
            Value = value;
            Weight = weight;
        }

        public double SignedImpact => Value * Weight;
    }
}
