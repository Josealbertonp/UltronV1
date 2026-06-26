using System;
using System.Collections.Generic;
using System.IO;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Signals;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>
    /// Builds ONNX input vector from feature_list.json order (host layer — no Core changes).
    /// </summary>
    public static class UltronShadowFeatureBuilder
    {
        public static float[] Build(
            BarEvaluationResult result,
            IReadOnlyList<string> featureNames,
            DateTime barTime)
        {
            if (featureNames == null || featureNames.Count == 0)
                return CopyCoreFeatures(result);

            var values = new float[featureNames.Count];
            float[] core = result.Snapshot?.Features?.Values ?? Array.Empty<float>();

            for (int i = 0; i < featureNames.Count; i++)
            {
                string name = featureNames[i];
                values[i] = ResolveFeature(name, result, core, barTime);
            }

            return values;
        }

        static float ResolveFeature(string name, BarEvaluationResult result, float[] core, DateTime barTime)
        {
            if (name.StartsWith("feat_", StringComparison.Ordinal))
            {
                string schemaName = name.Substring(5);
                int idx = Array.IndexOf(FeatureSchema.FeatureNames, schemaName);
                if (idx >= 0 && idx < core.Length)
                    return core[idx];
                return 0f;
            }

            var signal = result.Signal;
            var eval = result.Evaluation;

            switch (name)
            {
                case "score": return signal?.Score ?? 0f;
                case "long_score": return result.Score?.LongScore ?? 0f;
                case "short_score": return result.Score?.ShortScore ?? 0f;
                case "tier": return signal != null ? TierToFloat(signal.Tier) : 0f;
                case "archetype": return ArchetypeToFloat(signal?.Archetype.ToString());
                case "regime": return RegimeToFloat(result.Explanation?.Regime.ToString());
                case "direction": return DirectionToFloat(signal?.Direction ?? TradeDirection.None);
                case "hour": return barTime.Hour;
                case "minute": return barTime.Minute;
                case "day_of_week": return (float)barTime.DayOfWeek;
                case "order_flow_passed": return eval?.OrderFlowConfirmation.Passed == true ? 1f : 0f;
                case "gates_long_allowed": return result.Gates.LongAllowed ? 1f : 0f;
                case "gates_short_allowed": return result.Gates.ShortAllowed ? 1f : 0f;
                default:
                    if (name.StartsWith("gate_", StringComparison.Ordinal))
                        return 0f;
                    return 0f;
            }
        }

        static float[] CopyCoreFeatures(BarEvaluationResult result)
        {
            var src = result.Snapshot?.Features?.Values ?? Array.Empty<float>();
            var copy = new float[FeatureSchema.Count];
            int len = Math.Min(src.Length, FeatureSchema.Count);
            Array.Copy(src, copy, len);
            return copy;
        }

        static float TierToFloat(SignalTier tier) =>
            tier switch
            {
                SignalTier.A => 3f,
                SignalTier.B => 2f,
                SignalTier.C => 1f,
                _ => 0f
            };

        static float ArchetypeToFloat(string? archetype) =>
            string.IsNullOrEmpty(archetype) ? 0f : (float)(archetype.GetHashCode() % 100) / 100f;

        static float RegimeToFloat(string? regime) =>
            string.IsNullOrEmpty(regime) ? 0f : (float)(regime.GetHashCode() % 100) / 100f;

        static float DirectionToFloat(TradeDirection dir) =>
            dir == TradeDirection.Long ? 1f : dir == TradeDirection.Short ? -1f : 0f;
    }
}
