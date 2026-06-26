using System.Collections.Generic;

namespace UltronML
{
    /// <summary>
    /// ML inference provider — RC2 Phase 1: DummyProvider only.
    /// Future: XGBoostProvider, LightGBMProvider, OnnxProvider.
    /// </summary>
    public interface IMLProvider
    {
        string ProviderName { get; }
        bool IsLoaded { get; }
        void LoadModel(string modelDirectory);
        MlPredictionResult Predict(MlPredictionInput input);
        void Unload();
        string GetModelVersion();
        IReadOnlyList<string> GetFeatureNames();
    }
}
