using UltronV1.Core.Ml;
using UltronV1.Core.Archetypes;
using UltronV1.Core.Dataset;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Gates;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Scoring;

namespace UltronV1.Core.Signals
{
    public sealed class EvaluationResult
    {
        public UltronSignal? Signal { get; internal set; }
        public SignalExplanation Explanation { get; internal set; } = new SignalExplanation();
        public GateEvaluationResult Gates { get; internal set; } = new GateEvaluationResult();
        public ScoreBreakdown Score { get; internal set; } = new ScoreBreakdown();
        public ArchetypeEvaluation Archetypes { get; internal set; }
        public OrderFlowConfirmationResult OrderFlowConfirmation { get; internal set; }
        public EvaluationDatasetSnapshot DatasetFields { get; internal set; } = new EvaluationDatasetSnapshot();
    }

    /// <summary>
    /// Combines gates + archetypes + score + OF confirmation into signal with full explanation.
    /// </summary>
    public sealed class SignalEvaluator
    {
        readonly HardGateEvaluator _gates = new HardGateEvaluator();
        readonly EntryArchetypeEvaluator _archetypes = new EntryArchetypeEvaluator();
        readonly ConfluenceScorer _scorer = new ConfluenceScorer();
        readonly OrderFlowConfirmationEvaluator _orderFlow = new OrderFlowConfirmationEvaluator();
        readonly IMlAdvisor _mlAdvisor;

        public SignalEvaluator(IMlAdvisor? mlAdvisor = null)
        {
            _mlAdvisor = mlAdvisor ?? NullMlAdvisor.Instance;
        }

        public EvaluationResult Evaluate(BarSnapshot snap, UltronConfig cfg)
        {
            var result = new EvaluationResult();
            var explain = result.Explanation;

            result.Gates = _gates.Evaluate(snap, cfg);
            result.Archetypes = _archetypes.Evaluate(snap, cfg.EnableFreshBreakout);
            result.Score = _scorer.Score(snap, result.Gates, result.Archetypes, cfg);

            explain.LongScore = result.Score.LongScore;
            explain.ShortScore = result.Score.ShortScore;
            explain.Regime = ExplainBuilder.ClassifyRegime(snap, cfg);
            ExplainBuilder.PopulateGateRejections(result.Gates, explain);

            if (!result.Gates.LongAllowed && !result.Gates.ShortAllowed)
            {
                explain.Accepted = false;
                explain.RejectionStage = RejectionStage.HardGate;
                explain.RejectionSummary = result.Gates.BlockReason;
                AssignDatasetFields(result, TradeDirection.None, EntryArchetype.None, 0, TradeDirection.None);
                return result;
            }

            int longMin = snap.FreshBreakoutLong ? cfg.FreshBreakoutMinScore : cfg.ScoreMinEntry;
            int shortMin = snap.FreshBreakoutShort ? cfg.FreshBreakoutMinScore : cfg.ScoreMinEntry;

            bool longElig = result.Gates.LongAllowed && result.Score.LongScore >= longMin;
            bool shortElig = result.Gates.ShortAllowed && result.Score.ShortScore >= shortMin;

            if (!longElig && !shortElig)
            {
                explain.Accepted = false;
                explain.RejectionStage = RejectionStage.Score;
                explain.RejectionSummary = $"score insuf L={result.Score.LongScore}<{longMin} S={result.Score.ShortScore}<{shortMin}";
                ExplainBuilder.PopulateContributions(result.Score, explain, TradeDirection.Long);
                AssignDatasetFields(result, TradeDirection.None, EntryArchetype.None, 0, TradeDirection.Long);
                return result;
            }

            bool chooseLong = longElig && (!shortElig || result.Score.LongScore >= result.Score.ShortScore);
            TradeDirection dir = chooseLong ? TradeDirection.Long : TradeDirection.Short;
            int finalScore = chooseLong ? result.Score.LongScore : result.Score.ShortScore;
            EntryArchetype arch = chooseLong ? result.Archetypes.LongArchetype : result.Archetypes.ShortArchetype;

            ExplainBuilder.PopulateContributions(result.Score, explain, dir);
            explain.EvaluatedDirection = dir;

            if (cfg.RequireOrderFlowConfirmation)
            {
                result.OrderFlowConfirmation = _orderFlow.Evaluate(snap, dir, arch);
                explain.OrderFlowConfirmation = result.OrderFlowConfirmation;

                if (!result.OrderFlowConfirmation.Passed)
                {
                    explain.Accepted = false;
                    explain.RejectionStage = RejectionStage.OrderFlow;
                    explain.RejectionSummary = result.OrderFlowConfirmation.Reason;
                    explain.Tier = ExplainBuilder.ClassifyTier(finalScore, false, cfg);
                    AssignDatasetFields(result, dir, arch, finalScore, dir);
                    return result;
                }
            }
            else
            {
                result.OrderFlowConfirmation = OrderFlowConfirmationResult.Pass("disabled");
                explain.OrderFlowConfirmation = result.OrderFlowConfirmation;
            }

            SignalTier tier = ExplainBuilder.ClassifyTier(finalScore, true, cfg);
            explain.Tier = tier;
            explain.Accepted = tier >= cfg.MinSignalTier;

            if (!explain.Accepted)
            {
                explain.RejectionStage = RejectionStage.Score;
                explain.RejectionSummary = $"tier {tier} below min {cfg.MinSignalTier}";
                AssignDatasetFields(result, dir, arch, finalScore, dir);
                return result;
            }

            result.Signal = new UltronSignal
            {
                Direction = dir,
                Archetype = arch,
                Score = finalScore,
                Tier = tier,
                IsValid = true,
                Reason = $"{finalScore}pts {dir} [{arch}] Tier={tier}"
            };

            explain.Accepted = true;
            explain.RejectionStage = RejectionStage.None;

            ApplyMlAdvisory(snap, result, cfg, explain);
            AssignDatasetFields(
                result,
                result.Signal!.Direction,
                result.Signal.Archetype,
                result.Signal.Score,
                result.Signal.Direction);
            return result;
        }

        static void AssignDatasetFields(
            EvaluationResult result,
            TradeDirection direction,
            EntryArchetype archetype,
            int primaryScore,
            TradeDirection scoreBreakdownSide)
        {
            result.DatasetFields.Direction = direction;
            result.DatasetFields.Archetype = archetype;
            result.DatasetFields.PrimaryScore = primaryScore;
            result.DatasetFields.ScoreBreakdownSide = scoreBreakdownSide;
        }

        void ApplyMlAdvisory(BarSnapshot snap, EvaluationResult result, UltronConfig cfg, SignalExplanation explain)
        {
            MlAdvisoryResult ml = _mlAdvisor.Advise(snap, result, cfg);
            explain.MlAdvisory = ml;

            if (!ml.Active || result.Signal == null)
                return;

            // Advisory only: adjust score display/ranking, never invalidate signal
            int adjusted = result.Signal.Score + ml.ScoreDelta;
            if (adjusted < 0) adjusted = 0;
            if (adjusted > 100) adjusted = 100;
            result.Signal.Score = adjusted;
            result.Signal.Reason = $"{result.Signal.Reason} | {ml.Summary}";
        }
    }
}
