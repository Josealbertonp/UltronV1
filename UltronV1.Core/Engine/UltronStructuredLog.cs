using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UltronV1.Core.Engine
{
    /// <summary>
    /// Structured JSON log lines for Ultron observability (RC1 C6).
    /// Emitted via IUltronLogger — one JSON object per line, stable field names for downstream parsers.
    /// </summary>
    public static class UltronStructuredLog
    {
        public const string JsonPrefix = "ULTRON_LOG ";

        public static void Info(
            IUltronLogger logger,
            string component,
            string operation,
            string action,
            IReadOnlyDictionary<string, string>? context = null)
        {
            Emit(logger, UltronLogLevel.Info, component, operation, null, action, context);
        }

        public static void Warn(
            IUltronLogger logger,
            string component,
            string operation,
            Exception? exception,
            string action,
            IReadOnlyDictionary<string, string>? context = null)
        {
            Emit(logger, UltronLogLevel.Warn, component, operation, exception, action, context);
        }

        public static void Error(
            IUltronLogger logger,
            string component,
            string operation,
            Exception exception,
            string action,
            IReadOnlyDictionary<string, string>? context = null)
        {
            Emit(logger, UltronLogLevel.Error, component, operation, exception, action, context);
        }

        public static void Emit(
            IUltronLogger logger,
            UltronLogLevel level,
            string component,
            string operation,
            Exception? exception,
            string action,
            IReadOnlyDictionary<string, string>? context)
        {
            if (logger == null)
                return;

            string json = Serialize(
                DateTime.UtcNow,
                level,
                component,
                operation,
                exception,
                action,
                context);

            switch (level)
            {
                case UltronLogLevel.Info:
                    logger.Info(JsonPrefix + json);
                    break;
                case UltronLogLevel.Warn:
                    logger.Warn(JsonPrefix + json);
                    break;
                default:
                    logger.Error(JsonPrefix + json);
                    break;
            }
        }

        public static string Serialize(
            DateTime timestampUtc,
            UltronLogLevel level,
            string component,
            string operation,
            Exception? exception,
            string action,
            IReadOnlyDictionary<string, string>? context)
        {
            var sb = new StringBuilder(512);
            sb.Append('{');
            AppendStr(sb, "timestampUtc", timestampUtc.ToString("o", CultureInfo.InvariantCulture));
            AppendStr(sb, "level", level.ToString());
            AppendStr(sb, "component", component ?? string.Empty);
            AppendStr(sb, "operation", operation ?? string.Empty);
            AppendStr(sb, "exceptionType", exception?.GetType().Name ?? string.Empty);
            AppendStr(sb, "exceptionMessage", exception?.Message ?? string.Empty);
            AppendContext(sb, context);
            AppendStr(sb, "action", action ?? string.Empty, last: true);
            sb.Append('}');
            return sb.ToString();
        }

        static void AppendContext(StringBuilder sb, IReadOnlyDictionary<string, string>? context)
        {
            sb.Append(",\"context\":{");
            if (context != null && context.Count > 0)
            {
                bool first = true;
                foreach (var pair in context)
                {
                    if (!first)
                        sb.Append(',');
                    first = false;
                    AppendStrInline(sb, pair.Key, pair.Value ?? string.Empty);
                }
            }
            sb.Append("},");
        }

        static void AppendStr(StringBuilder sb, string key, string value, bool last = false)
        {
            sb.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value)).Append('"');
            if (!last)
                sb.Append(',');
        }

        static void AppendStrInline(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value)).Append('"');
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
