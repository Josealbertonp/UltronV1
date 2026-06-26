namespace UltronV1.Core.Dataset
{
    /// <summary>
    /// Central version registry — every dataset record embeds these values for ML reproducibility.
    /// </summary>
    public static class UltronVersions
    {
        public const string UltronVersion = "1.0.0";
        public const int FeatureSchemaVersion = 1;
        public const int ScoreVersion = 1;
        public const int DatasetRecordSchemaVersion = 1;
        public const string DefaultConfigVersion = "1.0.0";
    }
}
