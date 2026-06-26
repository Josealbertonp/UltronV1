using System.Globalization;
using System.Text;
using UltronV1.Core.Dataset;
using UltronV1.Core.Engine;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Scoring
{
    /// <summary>
    /// Defines the scoring-profile field set hashed into ScoreProfileHash.
    /// Values mirror UltronConfig defaults — metadata only; does not replace runtime config.
    /// </summary>
    public static class ScoreProfileV1
    {
        public const int SchemaVersion = 1;

        /// <summary>Hardcoded in ConfluenceScorer — included in hash for full score traceability.</summary>
        public const int W_ArchetypeTrigger = 5;

        public static string BuildCanonical(UltronConfig config)
        {
            var sb = new StringBuilder(640);

            void Line(string key, string value)
            {
                sb.Append(key).Append('=').Append(value).Append('\n');
            }

            Line("scoreProfileSchema", SchemaVersion.ToString(CultureInfo.InvariantCulture));
            Line("scoreVersion", UltronVersions.ScoreVersion.ToString(CultureInfo.InvariantCulture));

            Line("ScoreMinEntry", config.ScoreMinEntry.ToString(CultureInfo.InvariantCulture));
            Line("FreshBreakoutMinScore", config.FreshBreakoutMinScore.ToString(CultureInfo.InvariantCulture));
            Line("TierAThreshold", config.TierAThreshold.ToString(CultureInfo.InvariantCulture));
            Line("TierBThreshold", config.TierBThreshold.ToString(CultureInfo.InvariantCulture));
            Line("MinSignalTier", ((int)config.MinSignalTier).ToString(CultureInfo.InvariantCulture));

            Line("EnableFreshBreakout", config.EnableFreshBreakout ? "1" : "0");
            Line("AdxMin", config.AdxMin.ToString("G17", CultureInfo.InvariantCulture));
            Line("RsiOb", config.RsiOb.ToString("G17", CultureInfo.InvariantCulture));
            Line("RsiOs", config.RsiOs.ToString("G17", CultureInfo.InvariantCulture));
            Line("EmaSpreadMinTicks", config.EmaSpreadMinTicks.ToString("G17", CultureInfo.InvariantCulture));
            Line("MinRvol", config.MinRvol.ToString("G17", CultureInfo.InvariantCulture));

            Line("W_AdxStrength", config.W_AdxStrength.ToString(CultureInfo.InvariantCulture));
            Line("W_ArchetypeTrigger", W_ArchetypeTrigger.ToString(CultureInfo.InvariantCulture));
            Line("W_AtrExpansion", config.W_AtrExpansion.ToString(CultureInfo.InvariantCulture));
            Line("W_Absorption", config.W_Absorption.ToString(CultureInfo.InvariantCulture));
            Line("W_BidAskPressure", config.W_BidAskPressure.ToString(CultureInfo.InvariantCulture));
            Line("W_DeltaAlign", config.W_DeltaAlign.ToString(CultureInfo.InvariantCulture));
            Line("W_EmaCrossRecent", config.W_EmaCrossRecent.ToString(CultureInfo.InvariantCulture));
            Line("W_EmaSpread", config.W_EmaSpread.ToString(CultureInfo.InvariantCulture));
            Line("W_FreshBreakout", config.W_FreshBreakout.ToString(CultureInfo.InvariantCulture));
            Line("W_MtfTrend", config.W_MtfTrend.ToString(CultureInfo.InvariantCulture));
            Line("W_Pullback", config.W_Pullback.ToString(CultureInfo.InvariantCulture));
            Line("W_RsiChopPenalty", config.W_RsiChopPenalty.ToString(CultureInfo.InvariantCulture));
            Line("W_RsiMomentum", config.W_RsiMomentum.ToString(CultureInfo.InvariantCulture));
            Line("W_Rvol", config.W_Rvol.ToString(CultureInfo.InvariantCulture));
            Line("W_SweepAlign", config.W_SweepAlign.ToString(CultureInfo.InvariantCulture));
            Line("W_Trend", config.W_Trend.ToString(CultureInfo.InvariantCulture));
            Line("W_Volume", config.W_Volume.ToString(CultureInfo.InvariantCulture));
            Line("W_VwapAlign", config.W_VwapAlign.ToString(CultureInfo.InvariantCulture));

            return sb.ToString();
        }
    }
}
