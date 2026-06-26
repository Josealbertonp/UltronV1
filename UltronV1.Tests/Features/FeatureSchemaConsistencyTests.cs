using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UltronV1.Core.Features;
using Xunit;

namespace UltronV1.Tests.Features
{
    public class FeatureSchemaConsistencyTests
    {
        static readonly string UltronV1Root = FindUltronV1Root();

        [Fact]
        public void FeatureNames_CountIs40_AndUnique()
        {
            Assert.Equal(40, FeatureSchema.Count);
            Assert.Equal(FeatureSchema.Count, FeatureSchema.FeatureNames.Length);

            var unique = new HashSet<string>(FeatureSchema.FeatureNames, StringComparer.Ordinal);
            Assert.Equal(FeatureSchema.Count, unique.Count);
            Assert.All(FeatureSchema.FeatureNames, n => Assert.False(string.IsNullOrWhiteSpace(n)));
        }

        [Fact]
        public void FeatureVector_UsesFeatureSchemaCount()
        {
            var vector = new FeatureVector();
            Assert.Equal(FeatureSchema.Count, vector.Values.Length);
        }

        [Fact]
        public void CommittedFullSchemaJson_MatchesFeatureSchema()
        {
            string path = Path.Combine(UltronV1Root, "UltronV1.Core", "Features", FeatureSchemaExporter.FullSchemaFileName);
            Assert.True(File.Exists(path), "Missing committed schema JSON: " + path);

            var json = SimpleSchemaJson.ReadFeatures(path);
            Assert.Equal(FeatureSchema.Version, json.SchemaVersion);
            Assert.Equal(FeatureSchema.Count, json.FeatureCount);
            Assert.Equal(FeatureSchema.FeatureNames, json.Features);
        }

        [Fact]
        public void CommittedFeatureNamesJson_MatchesFeatureSchema()
        {
            string path = Path.Combine(UltronV1Root, "schema", FeatureSchemaExporter.FeatureNamesFileName);
            Assert.True(File.Exists(path), "Missing committed feature_names.json: " + path);

            var json = SimpleSchemaJson.ReadNames(path);
            Assert.Equal(FeatureSchema.Version, json.SchemaVersion);
            Assert.Equal(FeatureSchema.Count, json.FeatureCount);
            Assert.Equal(FeatureSchema.FeatureNames, json.Names);
        }

        [Fact]
        public void SharedSchemaJson_MatchesCoreSchemaJson()
        {
            string corePath = Path.Combine(UltronV1Root, "UltronV1.Core", "Features", FeatureSchemaExporter.FullSchemaFileName);
            string sharedPath = Path.Combine(UltronV1Root, "schema", FeatureSchemaExporter.FullSchemaFileName);

            var core = SimpleSchemaJson.ReadFeatures(corePath);
            var shared = SimpleSchemaJson.ReadFeatures(sharedPath);

            Assert.Equal(core.Features, shared.Features);
            Assert.Equal(core.FeatureCount, shared.FeatureCount);
            Assert.Equal(core.SchemaVersion, shared.SchemaVersion);
        }

        [Fact]
        public void Exporter_RegeneratesIdenticalJson()
        {
            string expectedFull = FeatureSchemaExporter.BuildFullSchemaJson();
            string expectedNames = FeatureSchemaExporter.BuildFeatureNamesJson();

            string tempRoot = Path.Combine(Path.GetTempPath(), "ultron-schema-" + Guid.NewGuid().ToString("N"));
            try
            {
                FeatureSchemaExporter.ExportAll(tempRoot);

                string fullPath = Path.Combine(tempRoot, "UltronV1.Core", "Features", FeatureSchemaExporter.FullSchemaFileName);
                string namesPath = Path.Combine(tempRoot, "schema", FeatureSchemaExporter.FeatureNamesFileName);

                Assert.Equal(NormalizeNewlines(expectedFull), NormalizeNewlines(File.ReadAllText(fullPath)));
                Assert.Equal(NormalizeNewlines(expectedNames), NormalizeNewlines(File.ReadAllText(namesPath)));
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        [Fact]
        public void PythonSources_DoNotHardcodeFeatureNameLists()
        {
            string[] roots =
            {
                Path.Combine(UltronV1Root, "UltronV1.Training"),
                Path.Combine(UltronV1Root, "UltronV1.Validation"),
            };

            var pattern = new Regex(
                @"FEATURE_NAMES\s*=\s*\[",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            foreach (string root in roots)
            {
                foreach (string file in Directory.EnumerateFiles(root, "*.py", SearchOption.AllDirectories))
                {
                    if (file.Contains("__pycache__", StringComparison.Ordinal))
                        continue;

                    string content = File.ReadAllText(file);
                    Assert.False(
                        pattern.IsMatch(content),
                        "Hardcoded FEATURE_NAMES list found in " + file + " — load from schema/*.json via ultron_schema.py");
                }
            }
        }

        [Fact]
        public void OnnxInputOrder_AlignsWithFeatureSchemaIndices()
        {
            // ONNX tensor column i == FeatureSchema index i == Python f_{name} column order.
            for (int i = 0; i < FeatureSchema.Count; i++)
                Assert.Equal(FeatureSchema.FeatureNames[i], FeatureSchema.FeatureNames[i]);

            Assert.Equal("Delta", FeatureSchema.FeatureNames[FeatureSchema.Delta]);
            Assert.Equal("TickCount", FeatureSchema.FeatureNames[FeatureSchema.TickCount]);
        }

        static string FindUltronV1Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "UltronV1.Core")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            throw new InvalidOperationException("UltronV1 root not found from test output directory.");
        }

        static string NormalizeNewlines(string text)
        {
            return text.Replace("\r\n", "\n").TrimEnd('\n') + "\n";
        }

        sealed class SimpleSchemaJson
        {
            internal int SchemaVersion { get; private set; }
            internal int FeatureCount { get; private set; }
            internal string[] Features { get; private set; } = Array.Empty<string>();
            internal string[] Names { get; private set; } = Array.Empty<string>();

            internal static SimpleSchemaJson ReadFeatures(string path)
            {
                string json = File.ReadAllText(path);
                return new SimpleSchemaJson
                {
                    SchemaVersion = ReadInt(json, "schemaVersion"),
                    FeatureCount = ReadInt(json, "featureCount"),
                    Features = ReadStringArray(json, "features"),
                };
            }

            internal static SimpleSchemaJson ReadNames(string path)
            {
                string json = File.ReadAllText(path);
                return new SimpleSchemaJson
                {
                    SchemaVersion = ReadInt(json, "schemaVersion"),
                    FeatureCount = ReadInt(json, "featureCount"),
                    Names = ReadStringArray(json, "names"),
                };
            }

            static int ReadInt(string json, string key)
            {
                var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);
                Assert.True(match.Success, "Missing " + key);
                return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            static string[] ReadStringArray(string json, string key)
            {
                var match = Regex.Match(
                    json,
                    "\"" + key + "\"\\s*:\\s*\\[(.*?)\\]",
                    RegexOptions.CultureInvariant | RegexOptions.Singleline);
                Assert.True(match.Success, "Missing array " + key);

                var names = Regex.Matches(match.Groups[1].Value, "\"([^\"]+)\"", RegexOptions.CultureInvariant)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .ToArray();
                return names;
            }
        }
    }
}
