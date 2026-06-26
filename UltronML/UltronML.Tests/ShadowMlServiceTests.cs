using UltronML;
using Xunit;

namespace UltronML.Tests
{
    public class ShadowMlServiceTests
    {
        [Fact]
        public void Disabled_ReturnsPassThrough()
        {
            using var svc = new ShadowMlService(new ShadowMlConfig { EnableShadowMl = false });
            var result = svc.Predict(new MlPredictionInput { Features = new float[] { 1f } });
            Assert.Equal(1.0, result.Confidence);
            Assert.Equal(MlDecision.Pass, result.Decision);
        }

        [Fact]
        public void MissingModel_ReturnsConfidenceOne()
        {
            using var svc = new ShadowMlService(new ShadowMlConfig
            {
                EnableShadowMl = true,
                ModelDirectory = @"C:\nonexistent\champion"
            });
            var result = svc.Predict(new MlPredictionInput { Features = new float[] { 1f } });
            Assert.Equal(1.0, result.Confidence);
            Assert.Equal(MlDecision.Pass, result.Decision);
        }

        [Fact]
        public void Loader_FilterDisabled_StillPassThrough()
        {
            using var loader = new MLModelLoader(new MlFilterConfig { EnableMlFilter = false });
            var result = loader.Predict(new MlPredictionInput { Features = new float[] { 1f } });
            Assert.False(result.Active);
            Assert.Equal(1.0, result.Confidence);
        }
    }
}
