using System.Collections.Generic;

namespace UltronML.Providers
{
    /// <summary>
    /// RC1-compatible provider — always returns Confidence = 1.0 (no filtering).
    /// </summary>
    public sealed class DummyProvider : IMLProvider
    {
        public static DummyProvider Instance { get; } = new DummyProvider();

        DummyProvider() { }

        public string ProviderName => "Dummy";
        public bool IsLoaded => true;

        public void LoadModel(string modelDirectory) { }

        public MlPredictionResult Predict(MlPredictionInput input)
        {
            return new MlPredictionResult
            {
                Active = true,
                Confidence = 1.0,
                ProviderName = ProviderName,
                ModelVersion = "dummy-rc2",
                Reason = "DummyProvider — pass-through"
            };
        }

        public void Unload() { }

        public string GetModelVersion() => "dummy-rc2";

        public IReadOnlyList<string> GetFeatureNames() => new string[0];
    }
}
