using UltronML;
using Xunit;

namespace UltronML.Tests
{
    public class MlFilterEvaluatorTests
    {
        [Fact]
        public void Active_BlocksLowConfidence()
        {
            var config = new MlHostConfig
            {
                Mode = MlFilterMode.Active,
                EnableMlFilter = true,
                ThresholdOverride = 0.70
            };
            var prediction = new MlPredictionResult
            {
                Active = true,
                Confidence = 0.55,
                Threshold = 0.70
            };

            var result = MlFilterEvaluator.Evaluate(config, prediction);
            Assert.False(result.AllowEntry);
            Assert.Equal(MlDecision.Block, result.Decision);
            Assert.Equal("LowConfidence", result.Reason);
        }

        [Fact]
        public void Active_PassesHighConfidence()
        {
            var config = new MlHostConfig
            {
                Mode = MlFilterMode.Active,
                EnableMlFilter = true,
                ThresholdOverride = 0.70
            };
            var prediction = new MlPredictionResult
            {
                Active = true,
                Confidence = 0.81,
                Threshold = 0.70
            };

            var result = MlFilterEvaluator.Evaluate(config, prediction);
            Assert.True(result.AllowEntry);
            Assert.Equal(MlDecision.Pass, result.Decision);
        }

        [Fact]
        public void Shadow_NeverBlocks()
        {
            var config = new MlHostConfig { Mode = MlFilterMode.Shadow, EnableMlFilter = true };
            var prediction = new MlPredictionResult { Active = true, Confidence = 0.10 };

            var result = MlFilterEvaluator.Evaluate(config, prediction);
            Assert.True(result.AllowEntry);
            Assert.Equal(MlDecision.PassThrough, result.Decision);
            Assert.True(result.WouldBlockInActive);
        }

        [Fact]
        public void FailSafe_AlwaysPasses()
        {
            var config = new MlHostConfig
            {
                Mode = MlFilterMode.Active,
                EnableMlFilter = true
            };
            var prediction = new MlPredictionResult
            {
                Active = false,
                Confidence = 1.0,
                Reason = "ProviderNotLoaded"
            };

            var result = MlFilterEvaluator.Evaluate(config, prediction);
            Assert.True(result.AllowEntry);
            Assert.Equal("FailSafe", result.Reason);
        }

        [Fact]
        public void ForcePassThrough_OverridesActive()
        {
            var config = new MlHostConfig
            {
                Mode = MlFilterMode.Active,
                EnableMlFilter = true,
                ForcePassThrough = true,
                ThresholdOverride = 0.99
            };
            var prediction = new MlPredictionResult { Active = true, Confidence = 0.10 };

            var result = MlFilterEvaluator.Evaluate(config, prediction);
            Assert.True(result.AllowEntry);
            Assert.Equal("ForcePassThrough", result.Reason);
        }
    }
}
