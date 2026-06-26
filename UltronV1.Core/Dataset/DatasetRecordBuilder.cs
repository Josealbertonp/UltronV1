using System;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Gates;
using UltronV1.Core.Scoring;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Dataset
{
    public static class DatasetRecordBuilder
    {
        public static OpportunityRecord Build(
            BarEvaluationResult result,
            string sessionId,
            string instrument,
            UltronConfig config,
            ShadowRecordEntry shadow)
        {
            BarSnapshot snap = result.Snapshot;
            EvaluationResult eval = result.Evaluation;
            SignalExplanation explain = eval.Explanation;
            EvaluationDatasetSnapshot fields = eval.DatasetFields;

            var record = new OpportunityRecord
            {
                SignalId = BuildSignalId(sessionId, snap.BarIndex, snap.BarTime, fields.Direction),
                SessionId = sessionId,
                TimestampUtc = snap.BarTime.ToUniversalTime(),
                BarIndex = snap.BarIndex,
                Instrument = instrument,
                Close = snap.Close,
                UltronVersion = UltronVersions.UltronVersion,
                FeatureSchemaVersion = UltronVersions.FeatureSchemaVersion,
                ScoreVersion = UltronVersions.ScoreVersion,
                ScoreProfileHash = ScoreProfileHash.Compute(config),
                ConfigVersion = config.ConfigVersion,
                RecordSchemaVersion = UltronVersions.DatasetRecordSchemaVersion,
                Archetype = fields.Archetype,
                Direction = fields.Direction,
                Score = fields.PrimaryScore,
                LongScore = eval.Score.LongScore,
                ShortScore = eval.Score.ShortScore,
                Tier = explain.Tier,
                Regime = explain.Regime,
                Decision = explain.Accepted ? DatasetDecision.Accepted : DatasetDecision.Rejected,
                RejectionStage = explain.RejectionStage,
                RejectionReason = explain.RejectionSummary ?? string.Empty,
                GatesLongAllowed = eval.Gates.LongAllowed,
                GatesShortAllowed = eval.Gates.ShortAllowed,
                GatesBlockReason = eval.Gates.BlockReason ?? string.Empty,
                OrderFlowPassed = eval.OrderFlowConfirmation.Passed,
                OrderFlowReason = eval.OrderFlowConfirmation.Reason ?? string.Empty,
                OrderFlowDetail = eval.OrderFlowConfirmation.Detail ?? string.Empty,
                Features = CopyFeatures(snap.Features),
                ExplainText = explain.FormatExplainBlock().Trim(),
                Shadow = shadow
            };

            PopulateGates(record, eval.Gates);
            PopulateScoreBreakdown(record, explain, fields.ScoreBreakdownSide);

            return record;
        }

        public static string BuildSignalId(string sessionId, int barIndex, DateTime barTime, TradeDirection dir)
        {
            string side = dir == TradeDirection.Short ? "S" : dir == TradeDirection.Long ? "L" : "N";
            return string.Format("{0}-{1:yyyyMMddHHmmss}-{2}-{3}",
                sessionId, barTime.ToUniversalTime(), barIndex, side);
        }

        static float[] CopyFeatures(FeatureVector vector)
        {
            var src = vector.Values;
            var copy = new float[FeatureSchema.Count];
            int len = Math.Min(src.Length, FeatureSchema.Count);
            Array.Copy(src, copy, len);
            return copy;
        }

        static void PopulateGates(OpportunityRecord record, GateEvaluationResult gates)
        {
            foreach (var check in gates.Checks)
            {
                record.GateChecks.Add(new GateRecordEntry(
                    check.Name,
                    check.LongPass,
                    check.ShortPass,
                    check.Detail ?? string.Empty));
            }
        }

        static void PopulateScoreBreakdown(OpportunityRecord record, SignalExplanation explain, TradeDirection side)
        {
            if (side == TradeDirection.None)
                return;

            var contributions = side == TradeDirection.Short
                ? explain.ShortContributions
                : explain.LongContributions;

            foreach (var c in contributions)
            {
                record.ScoreBreakdown.Add(new ScoreBreakdownEntry(c.Name, c.Points, c.Applied));
            }
        }
    }
}
