using System;
using System.IO;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Ml;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;
using Xunit;

namespace UltronV1.Tests.Ml
{
    public class MlAdvisorTests
    {
        [Fact]
        public void NullAdvisor_ReturnsInactive()
        {
            var advisor = NullMlAdvisor.Instance;
            var snap = new BarSnapshot { Features = new FeatureVector() };
            var eval = new EvaluationResult();
            var result = advisor.Advise(snap, eval, UltronConfig.Default);

            Assert.False(result.Active);
            Assert.Equal(0, result.ScoreDelta);
        }

        [Fact]
        public void SignalEvaluator_MlNeverVetoesAcceptedSignal()
        {
            var stub = new StubMlAdvisor { ScoreDelta = -20, WinProbability = 0.1, Active = true };
            var eval = new SignalEvaluator(stub);
            var snap = BuildStrongLongSnapshot();
            var cfg = UltronConfig.Default;

            var result = eval.Evaluate(snap, cfg);

            Assert.NotNull(result.Signal);
            Assert.True(result.Explanation.Accepted);
            Assert.True(result.Explanation.MlAdvisory.Active);
            Assert.Equal(-20, result.Explanation.MlAdvisory.ScoreDelta);
        }

        [Fact]
        public void ManifestLoader_ParsesConfigJson()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ultron_ml_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "config.json"), @"{
                    ""modelId"": ""test_model"",
                    ""featureSchemaVersion"": 1,
                    ""maxBoostPoints"": 10,
                    ""maxPenaltyPoints"": -8,
                    ""boostThreshold"": 0.7,
                    ""penaltyThreshold"": 0.4,
                    ""advisoryOnly"": true,
                    ""neverVeto"": true
                }");
                File.WriteAllText(Path.Combine(dir, "feature_importance.json"), @"{
                    ""features"": [
                        {""feature"": ""Delta"", ""index"": 0, ""importance"": 100.0, ""importancePct"": 0.5},
                        {""feature"": ""Rvol"", ""index"": 8, ""importance"": 50.0, ""importancePct"": 0.25}
                    ]
                }");

                var bundle = ManifestLoader.Load(dir);
                Assert.Equal("test_model", bundle.Config.ModelId);
                Assert.Equal(10, bundle.Config.MaxBoostPoints);
                Assert.Equal(2, bundle.FeatureWeights.Count);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        static BarSnapshot BuildStrongLongSnapshot()
        {
            var of = new OrderFlowState(
                500, 200, 300, 50, 1.5,
                true, TickSide.Buy, 5,
                true, TickSide.Sell, 3,
                80100, 80090);

            var vec = new FeatureVector();
            for (int i = 0; i < FeatureSchema.Count; i++)
                vec.Values[i] = 1f;

            return new BarSnapshot
            {
                Close = 20010,
                High = 20011,
                Low = 20009,
                BarVolume = 200,
                EfficiencyRatio = 0.5,
                EfficiencyRatioPrev = 0.4,
                ChopFlips = 0,
                Rvol = 1.5,
                PullbackLong = true,
                ContinuationLong = true,
                Features = vec,
                Indicators = new IndicatorSnapshot(
                    20000, 19995, 28, 25, 58, 6,
                    20000, 20, 20010, 19990, 20008, 19992,
                    80, 20000, 19999.5, true),
                OrderFlow = of
            };
        }

        sealed class StubMlAdvisor : IMlAdvisor
        {
            public bool Active { get; set; }
            public int ScoreDelta { get; set; }
            public double WinProbability { get; set; }
            public bool IsEnabled => true;

            public MlAdvisoryResult Advise(BarSnapshot snapshot, EvaluationResult evaluation, UltronConfig config)
            {
                return new MlAdvisoryResult
                {
                    Active = Active,
                    ScoreDelta = ScoreDelta,
                    WinProbability = WinProbability,
                    Summary = $"stub P={WinProbability:F2} delta={ScoreDelta}"
                };
            }
        }
    }
}
