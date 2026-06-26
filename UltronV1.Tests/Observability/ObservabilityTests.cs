using System;
using System.IO;
using UltronV1.Core.Dataset;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Ml;
using UltronV1.Core.Signals;
using UltronV1.Tests.Support;
using Xunit;

namespace UltronV1.Tests.Observability
{
    public class ObservabilityTests
    {
        [Fact]
        public void OnnxInferenceEngine_MissingOnnxFile_LogsDisableFallback()
        {
            string dir = CreateModelDir(includeOnnx: false);
            try
            {
                var logger = new ListUltronLogger();
                var bundle = ManifestLoader.Load(dir, logger);
                var engine = new OnnxInferenceEngine(bundle, logger);

                Assert.False(engine.UsesOnnx);
                Assert.NotEmpty(logger.WarnMessages);
                Assert.Contains("\"component\":\"OnnxInferenceEngine\"", logger.WarnMessages[0]);
                Assert.Contains("\"action\":\"disable_onnx\"", logger.WarnMessages[0]);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void OnnxInferenceEngine_CorruptOnnxFile_LogsDisableFallback()
        {
            string dir = CreateModelDir(includeOnnx: true, corruptOnnx: true);
            try
            {
                var logger = new ListUltronLogger();
                var bundle = ManifestLoader.Load(dir, logger);
                var engine = new OnnxInferenceEngine(bundle, logger);

                Assert.False(engine.UsesOnnx);
                Assert.NotEmpty(logger.WarnMessages);
                Assert.Contains("\"operation\":\"LoadSession\"", logger.WarnMessages[0]);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void OnnxInferenceEngine_CorruptOnnx_StillReturnsHeuristicPrediction()
        {
            string dir = CreateModelDir(includeOnnx: true, corruptOnnx: true);
            try
            {
                var logger = new ListUltronLogger();
                var bundle = ManifestLoader.Load(dir, logger);
                using var engine = new OnnxInferenceEngine(bundle, logger);

                var features = new float[FeatureSchema.Count];
                for (int i = 0; i < features.Length; i++)
                    features[i] = 1f;

                double prob = engine.PredictWinProbability(features);

                Assert.InRange(prob, 0.0, 1.0);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void ManifestLoader_MissingConfig_LogsErrorBeforeThrow()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ultron_obs_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var logger = new ListUltronLogger();

            try
            {
                var ex = Assert.Throws<FileNotFoundException>(() => ManifestLoader.Load(dir, logger));
                Assert.NotEmpty(logger.ErrorMessages);
                Assert.Contains("\"component\":\"ManifestLoader\"", logger.ErrorMessages[0]);
                Assert.Contains("\"action\":\"propagate\"", logger.ErrorMessages[0]);
                Assert.Contains("config.json", ex.Message);
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void JsonLinesDatasetSink_InvalidRootPath_LogsErrorAndPropagates()
        {
            var logger = new ListUltronLogger();
            var sink = new JsonLinesDatasetSink("<<invalid>>", logger);
            var manifest = new DatasetManifest { SessionId = "sess1", Instrument = "MNQ 09-26" };

            Assert.Throws<IOException>(() => sink.OpenSession(manifest));
            Assert.NotEmpty(logger.ErrorMessages);
            Assert.Contains("\"component\":\"JsonLinesDatasetSink\"", logger.ErrorMessages[0]);
            Assert.Contains("\"operation\":\"OpenSession\"", logger.ErrorMessages[0]);
        }

        [Fact]
        public void MlAdvisor_SchemaMismatch_LogsWarnAndReturnsInactive()
        {
            string dir = CreateModelDir(includeOnnx: false, schemaVersion: 99);
            try
            {
                var logger = new ListUltronLogger();
                using var advisor = new MlAdvisor(dir, logger);
                var cfg = UltronConfig.Default;
                cfg.EnableMlAdvisor = true;

                var result = advisor.Advise(
                    new BarSnapshot { Features = new FeatureVector() },
                    new EvaluationResult(),
                    cfg);

                Assert.False(result.Active);
                Assert.Contains(logger.WarnMessages, m => m.Contains("\"component\":\"FeatureNormalizer\""));
                Assert.Contains(logger.WarnMessages, m => m.Contains("\"action\":\"ml_inactive\""));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void DatasetEngine_EnrichOutcome_UnknownSignal_LogsSkipOutcome()
        {
            var logger = new ListUltronLogger();
            var dataset = new DatasetEngine(new InMemoryDatasetSink(), logger);
            dataset.StartSession("MNQ 09-26", UltronConfig.Default);

            bool linked = dataset.EnrichOutcome(new TradeOutcome { SignalId = "missing-signal" });

            Assert.False(linked);
            Assert.Contains(logger.WarnMessages, m => m.Contains("\"component\":\"DatasetEngine\""));
            Assert.Contains(logger.WarnMessages, m => m.Contains("\"action\":\"skip_outcome\""));
        }

        static string CreateModelDir(bool includeOnnx, bool corruptOnnx = false, int schemaVersion = 1)
        {
            string dir = Path.Combine(Path.GetTempPath(), "ultron_obs_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            File.WriteAllText(Path.Combine(dir, "config.json"), $@"{{
                ""modelId"": ""obs_test"",
                ""featureSchemaVersion"": {schemaVersion},
                ""maxBoostPoints"": 8,
                ""maxPenaltyPoints"": -6,
                ""boostThreshold"": 0.62,
                ""penaltyThreshold"": 0.45,
                ""advisoryOnly"": true,
                ""neverVeto"": true
            }}");

            File.WriteAllText(Path.Combine(dir, "feature_importance.json"), @"{
                ""features"": [
                    {""feature"": ""Delta"", ""index"": 0, ""importance"": 100.0, ""importancePct"": 0.5}
                ]
            }");

            if (includeOnnx)
                File.WriteAllText(Path.Combine(dir, "model.onnx"), corruptOnnx ? "not-valid-onnx" : string.Empty);

            return dir;
        }
    }
}
