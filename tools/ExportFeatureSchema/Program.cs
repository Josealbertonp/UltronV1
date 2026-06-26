using System;
using System.IO;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;

namespace ExportFeatureSchema
{
    static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--score-profile-hash")
            {
                Console.WriteLine(UltronV1.Core.Scoring.ScoreProfileHash.Compute(UltronConfig.Default));
                return 0;
            }

            string root = args.Length > 0 && args[0] != "--score-profile-hash"
                ? args[0]
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

            if (!Directory.Exists(Path.Combine(root, "UltronV1.Core")))
            {
                Console.Error.WriteLine("UltronV1 root not found: " + root);
                Console.Error.WriteLine("Usage: ExportFeatureSchema [path-to-UltronV1]");
                return 1;
            }

            FeatureSchemaExporter.ExportAll(root);
            Console.WriteLine("Exported feature schema from FeatureSchema.cs to:");
            Console.WriteLine("  UltronV1.Core/Features/" + FeatureSchemaExporter.FullSchemaFileName);
            Console.WriteLine("  schema/" + FeatureSchemaExporter.FullSchemaFileName);
            Console.WriteLine("  schema/" + FeatureSchemaExporter.FeatureNamesFileName);
            return 0;
        }
    }
}
