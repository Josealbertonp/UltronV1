using System;

namespace UltronML
{
    /// <summary>Safe ML filter — veto only, never creates trades. Errors always PASS.</summary>
    public static class MlFilterEvaluator
    {
        public static MlFilterResult Evaluate(MlHostConfig config, MlPredictionResult prediction)
        {
            double threshold = ResolveThreshold(config, prediction);
            bool failSafe = IsFailSafe(prediction);
            bool wouldBlock = !failSafe && prediction.Confidence < threshold;

            var result = new MlFilterResult
            {
                Prediction = prediction,
                Threshold = threshold,
                WouldBlockInActive = wouldBlock,
                FilterMode = config.Mode.ToString()
            };

            if (failSafe)
            {
                result.AllowEntry = true;
                result.Decision = MlDecision.Pass;
                result.Reason = "FailSafe";
                return result;
            }

            if (config.ForcePassThrough)
            {
                result.AllowEntry = true;
                result.Decision = MlDecision.PassThrough;
                result.Reason = "ForcePassThrough";
                return result;
            }

            if (config.Mode == MlFilterMode.Disabled && !config.EnableShadowMl)
            {
                result.AllowEntry = true;
                result.Decision = MlDecision.PassThrough;
                result.Reason = "Disabled";
                return result;
            }

            if (config.Mode == MlFilterMode.Shadow
                || !config.EnableMlFilter
                || config.Mode != MlFilterMode.Active)
            {
                result.AllowEntry = true;
                result.Decision = MlDecision.PassThrough;
                result.Reason = config.Mode == MlFilterMode.Shadow ? "ShadowMode" : "FilterInactive";
                return result;
            }

            // Active + EnableMlFilter
            if (prediction.Confidence >= threshold)
            {
                result.AllowEntry = true;
                result.Decision = MlDecision.Pass;
                result.Reason = "ConfidenceMet";
            }
            else
            {
                result.AllowEntry = false;
                result.Decision = MlDecision.Block;
                result.Reason = "LowConfidence";
            }

            return result;
        }

        static double ResolveThreshold(MlHostConfig config, MlPredictionResult prediction)
        {
            if (config.ThresholdOverride.HasValue)
            {
                double t = config.ThresholdOverride.Value;
                if (t >= 0 && t <= 1)
                    return t;
            }

            if (prediction.Threshold > 0 && prediction.Threshold <= 1)
                return prediction.Threshold;

            return 0.50;
        }

        static bool IsFailSafe(MlPredictionResult prediction) =>
            !prediction.Active;
    }
}
