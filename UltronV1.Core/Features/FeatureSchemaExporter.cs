using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace UltronV1.Core.Features
{
    /// <summary>
    /// Exports FeatureSchema to JSON artifacts consumed by Dataset Engine, Python Training,
    /// Validation Framework, and ML manifests. Run via tools/ExportFeatureSchema after editing FeatureSchema.cs.
    /// </summary>
    public static class FeatureSchemaExporter
    {
        public const string FullSchemaFileName = "ultron_mnq_feature_schema_v1.json";
        public const string FeatureNamesFileName = "feature_names.json";

        public static string BuildFullSchemaJson(string instrument = "MNQ 09-26")
        {
            var sb = new StringBuilder(2048);
            sb.Append('{').AppendLine();
            sb.Append("  \"schemaVersion\": ").Append(FeatureSchema.Version).Append(',').AppendLine();
            sb.Append("  \"instrument\": \"").Append(Escape(instrument)).Append("\",").AppendLine();
            sb.Append("  \"featureCount\": ").Append(FeatureSchema.Count).Append(',').AppendLine();
            sb.AppendLine("  \"features\": [");
            AppendNameArray(sb, "    ");
            sb.AppendLine();
            sb.Append("  ]").AppendLine();
            sb.Append('}');
            return sb.ToString();
        }

        public static string BuildFeatureNamesJson()
        {
            var sb = new StringBuilder(2048);
            sb.Append('{').AppendLine();
            sb.Append("  \"schemaVersion\": ").Append(FeatureSchema.Version).Append(',').AppendLine();
            sb.Append("  \"featureCount\": ").Append(FeatureSchema.Count).Append(',').AppendLine();
            sb.AppendLine("  \"names\": [");
            AppendNameArray(sb, "    ");
            sb.AppendLine();
            sb.Append("  ]").AppendLine();
            sb.Append('}');
            return sb.ToString();
        }

        public static void ExportAll(string ultronV1Root)
        {
            if (string.IsNullOrWhiteSpace(ultronV1Root))
                throw new ArgumentException("UltronV1 root path is required", nameof(ultronV1Root));

            string root = Path.GetFullPath(ultronV1Root);
            string fullSchemaJson = BuildFullSchemaJson();
            string namesJson = BuildFeatureNamesJson();

            WriteUtf8NoBom(Path.Combine(root, "UltronV1.Core", "Features", FullSchemaFileName), fullSchemaJson);
            WriteUtf8NoBom(Path.Combine(root, "schema", FullSchemaFileName), fullSchemaJson);
            WriteUtf8NoBom(Path.Combine(root, "schema", FeatureNamesFileName), namesJson);
        }

        static void AppendNameArray(StringBuilder sb, string indent)
        {
            string[] names = FeatureSchema.FeatureNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0)
                    sb.Append(',').AppendLine();
                sb.Append(indent).Append('"').Append(Escape(names[i])).Append('"');
            }
        }

        static void WriteUtf8NoBom(string path, string content)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, content + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
