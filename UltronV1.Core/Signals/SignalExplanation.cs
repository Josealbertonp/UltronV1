using System.Collections.Generic;
using System.Text;
using UltronV1.Core.OrderFlow;

namespace UltronV1.Core.Signals
{
    public readonly struct ScoreContribution
    {
        public string Name { get; }
        public int Points { get; }
        public bool Applied { get; }

        public ScoreContribution(string name, int points, bool applied)
        {
            Name = name;
            Points = points;
            Applied = applied;
        }

        public string FormatLine()
        {
            if (!Applied && Points >= 0)
                return null!;
            int display = Applied ? Points : 0;
            if (display == 0)
                return null!;
            return display > 0 ? $"{Name} +{display}" : $"{Name} {display}";
        }
    }

    public readonly struct GateRejection
    {
        public string GateName { get; }
        public string Reason { get; }
        public bool BlocksLong { get; }
        public bool BlocksShort { get; }

        public GateRejection(string gateName, string reason, bool blocksLong, bool blocksShort)
        {
            GateName = gateName;
            Reason = reason;
            BlocksLong = blocksLong;
            BlocksShort = blocksShort;
        }
    }

    /// <summary>
    /// Full audit trail for accepted or rejected evaluations — Explain Mode.
    /// </summary>
    public sealed class SignalExplanation
    {
        public bool Accepted { get; internal set; }
        public RejectionStage RejectionStage { get; internal set; }
        public string RejectionSummary { get; internal set; } = string.Empty;
        public TradeDirection EvaluatedDirection { get; internal set; }
        public int LongScore { get; internal set; }
        public int ShortScore { get; internal set; }
        public SignalTier Tier { get; internal set; }
        public MarketRegime Regime { get; internal set; }
        public OrderFlowConfirmationResult OrderFlowConfirmation { get; internal set; }
        public Ml.MlAdvisoryResult MlAdvisory { get; internal set; } = Ml.MlAdvisoryResult.Inactive;

        public List<ScoreContribution> LongContributions { get; } = new List<ScoreContribution>(24);
        public List<ScoreContribution> ShortContributions { get; } = new List<ScoreContribution>(24);
        public List<GateRejection> GateRejections { get; } = new List<GateRejection>(8);

        public string FormatScoreLines(TradeDirection side)
        {
            var list = side == TradeDirection.Short ? ShortContributions : LongContributions;
            var sb = new StringBuilder();
            foreach (var c in list)
            {
                string line = c.FormatLine();
                if (line == null) continue;
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(line);
            }
            return sb.Length > 0 ? sb.ToString() : "(no factors)";
        }

        public string FormatGateRejections()
        {
            if (GateRejections.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var g in GateRejections)
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(g.GateName).Append(": ").Append(g.Reason);
            }
            return sb.ToString();
        }

        public string FormatExplainBlock()
        {
            var sb = new StringBuilder();
            if (Accepted)
            {
                sb.Append("ACCEPT ").Append(EvaluatedDirection)
                    .Append(" Tier=").Append(Tier)
                    .Append(" Score=").Append(EvaluatedDirection == TradeDirection.Long ? LongScore : ShortScore)
                    .AppendLine();
                sb.Append("  ").Append(FormatScoreLines(EvaluatedDirection));
                if (MlAdvisory.Active)
                    sb.AppendLine().Append("  ML: ").Append(MlAdvisory.Summary);
            }
            else
            {
                sb.Append("REJECT [").Append(RejectionStage).Append("] ")
                    .Append(RejectionSummary).AppendLine();
                string gates = FormatGateRejections();
                if (!string.IsNullOrEmpty(gates))
                    sb.Append("  Gates: ").Append(gates).AppendLine();
                if (RejectionStage == RejectionStage.Score)
                    sb.Append("  Scores: L=").Append(LongScore).Append(" S=").Append(ShortScore).AppendLine();
                if (RejectionStage == RejectionStage.OrderFlow)
                    sb.Append("  OF: ").Append(OrderFlowConfirmation.Reason)
                        .Append(" — ").Append(OrderFlowConfirmation.Detail).AppendLine();
            }
            return sb.ToString();
        }
    }
}
