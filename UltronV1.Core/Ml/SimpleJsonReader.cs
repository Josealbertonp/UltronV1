using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace UltronV1.Core.Ml
{
    /// <summary>
    /// Minimal JSON field extractor — zero external dependencies (netstandard2.0).
    /// </summary>
    internal static class SimpleJsonReader
    {
        public static string? ReadString(string json, string key)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        public static bool TryReadDouble(string json, string key, out double value)
        {
            var m = Regex.Match(
                json,
                "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)",
                RegexOptions.CultureInvariant);
            if (!m.Success)
            {
                value = 0;
                return false;
            }
            return double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryReadInt(string json, string key, out int value)
        {
            if (TryReadDouble(json, key, out double d))
            {
                value = (int)Math.Round(d);
                return true;
            }
            value = 0;
            return false;
        }

        public static bool TryReadBool(string json, string key, out bool value)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                value = false;
                return false;
            }
            value = string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
            return true;
        }
    }
}
