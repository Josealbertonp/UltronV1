using System;
using System.Collections.Generic;
using System.IO;using UltronV1.Core.Engine;
using Xunit;

namespace UltronV1.Tests.Engine
{
    public class StructuredLogTests
    {
        [Fact]
        public void Serialize_EmitsRequiredFields()
        {
            var context = new Dictionary<string, string> { ["onnxPath"] = "C:\\models\\model.onnx" };
            string json = UltronStructuredLog.Serialize(
                new DateTime(2026, 6, 25, 14, 30, 0, DateTimeKind.Utc),
                UltronLogLevel.Warn,
                UltronLogComponents.OnnxInferenceEngine,
                "LoadSession",
                new InvalidOperationException("bad onnx"),
                UltronLogActions.DisableOnnx,
                context);

            Assert.Contains("\"timestampUtc\":\"2026-06-25T14:30:00.0000000Z\"", json);
            Assert.Contains("\"level\":\"Warn\"", json);
            Assert.Contains("\"component\":\"OnnxInferenceEngine\"", json);
            Assert.Contains("\"operation\":\"LoadSession\"", json);
            Assert.Contains("\"exceptionType\":\"InvalidOperationException\"", json);
            Assert.Contains("\"exceptionMessage\":\"bad onnx\"", json);
            Assert.Contains("\"onnxPath\":\"C:\\\\models\\\\model.onnx\"", json);
            Assert.Contains("\"action\":\"disable_onnx\"", json);
        }

        [Fact]
        public void Emit_PrefixesJsonForDownstreamParsing()
        {
            var logger = new Support.ListUltronLogger();
            UltronStructuredLog.Warn(
                logger,
                UltronLogComponents.ManifestLoader,
                "LoadConfig",
                new FileNotFoundException("config.json missing"),
                UltronLogActions.Propagate,
                new Dictionary<string, string> { ["modelDirectory"] = "C:\\models" });

            Assert.Single(logger.WarnMessages);
            Assert.StartsWith(UltronStructuredLog.JsonPrefix, logger.WarnMessages[0]);
            Assert.Contains("\"component\":\"ManifestLoader\"", logger.WarnMessages[0]);
        }
    }
}
