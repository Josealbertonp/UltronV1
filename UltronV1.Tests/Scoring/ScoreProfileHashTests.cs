using UltronV1.Core.Dataset;
using UltronV1.Core.Engine;
using UltronV1.Core.Scoring;
using Xunit;

namespace UltronV1.Tests.Scoring
{
    public class ScoreProfileHashTests
    {
        [Fact]
        public void Compute_DefaultConfig_IsDeterministic()
        {
            string a = ScoreProfileHash.Compute(UltronConfig.Default);
            string b = ScoreProfileHash.Compute(UltronConfig.Default);

            Assert.Equal(a, b);
            Assert.Equal(64, a.Length);
            Assert.Matches("^[0-9a-f]{64}$", a);
        }

        [Fact]
        public void Compute_DefaultConfig_MatchesGoldenHash()
        {
            string hash = ScoreProfileHash.Compute(UltronConfig.Default);

            Assert.Equal(
                "1f618f97e367988ce2c76c844c9a492b5c3a9f5000a506f8624969091914d217",
                hash);
        }

        [Fact]
        public void Compute_WeightChange_ChangesHash()
        {
            var baseline = UltronConfig.Default;
            var modified = UltronConfig.Default;
            modified.W_Trend = baseline.W_Trend + 1;

            Assert.NotEqual(
                ScoreProfileHash.Compute(baseline),
                ScoreProfileHash.Compute(modified));
        }

        [Fact]
        public void Compute_ConfigVersionChange_DoesNotChangeHash()
        {
            var a = UltronConfig.Default;
            var b = UltronConfig.Default;
            b.ConfigVersion = "9.9.9-manual";

            Assert.Equal(ScoreProfileHash.Compute(a), ScoreProfileHash.Compute(b));
        }

        [Fact]
        public void Compute_MlAndDatasetFlags_DoNotChangeHash()
        {
            var a = UltronConfig.Default;
            var b = UltronConfig.Default;
            b.EnableMlAdvisor = true;
            b.EnableDatasetEngine = true;
            b.EnableOutcomeCollection = true;
            b.MlModelDirectory = "C:\\models\\test";

            Assert.Equal(ScoreProfileHash.Compute(a), ScoreProfileHash.Compute(b));
        }

        [Fact]
        public void BuildCanonical_UsesFixedFieldOrder()
        {
            string canonical = ScoreProfileV1.BuildCanonical(UltronConfig.Default);

            Assert.StartsWith("scoreProfileSchema=1\n", canonical);
            Assert.Contains("W_Trend=18\n", canonical);
            Assert.Contains("W_ArchetypeTrigger=5\n", canonical);
            Assert.Contains("ScoreMinEntry=55\n", canonical);
            Assert.DoesNotContain("EnableMlAdvisor", canonical);
            Assert.DoesNotContain("ConfigVersion", canonical);
        }
    }
}
