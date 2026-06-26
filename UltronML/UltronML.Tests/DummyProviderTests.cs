using UltronML;
using UltronML.Providers;
using Xunit;

namespace UltronML.Tests
{
    public class DummyProviderTests
    {
        [Fact]
        public void Predict_ReturnsConfidenceOne()
        {
            var result = DummyProvider.Instance.Predict(new MlPredictionInput
            {
                Features = new float[] { 1f, 2f, 3f },
                Score = 71,
                Tier = "A"
            });
            Assert.Equal(1.0, result.Confidence);
        }

        [Fact]
        public void Loader_FilterDisabled_ReturnsPassThrough()
        {
            using var loader = new MLModelLoader(new MlFilterConfig { EnableMlFilter = false });
            var result = loader.Predict(new MlPredictionInput { Features = new float[] { 1f } });
            Assert.False(result.Active);
            Assert.Equal(1.0, result.Confidence);
        }

        [Fact]
        public void Loader_FilterEnabled_UsesDummyConfidenceOne()
        {
            using var loader = new MLModelLoader(new MlFilterConfig
            {
                EnableMlFilter = true,
                ProviderName = "Dummy",
                ModelDirectory = ""
            });
            var result = loader.Predict(new MlPredictionInput
            {
                Features = new float[] { 1f, 2f },
                Score = 72,
                Tier = "A"
            });
            Assert.True(result.Active);
            Assert.Equal(1.0, result.Confidence);
        }
    }
}
