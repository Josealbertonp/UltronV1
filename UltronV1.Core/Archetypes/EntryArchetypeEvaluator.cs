using UltronV1.Core.Domain;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Archetypes
{
    public readonly struct ArchetypeEvaluation
    {
        public EntryArchetype LongArchetype { get; }
        public EntryArchetype ShortArchetype { get; }
        public bool LongHasTrigger { get; }
        public bool ShortHasTrigger { get; }

        public ArchetypeEvaluation(
            EntryArchetype longArchetype,
            EntryArchetype shortArchetype,
            bool longHasTrigger,
            bool shortHasTrigger)
        {
            LongArchetype = longArchetype;
            ShortArchetype = shortArchetype;
            LongHasTrigger = longHasTrigger;
            ShortHasTrigger = shortHasTrigger;
        }
    }

    /// <summary>
    /// Identifies entry archetype per side. In SCORE mode triggers contribute to score, not hard veto.
    /// </summary>
    public sealed class EntryArchetypeEvaluator
    {
        public ArchetypeEvaluation Evaluate(BarSnapshot snap, bool enableFreshBreakout)
        {
            bool freshLong = enableFreshBreakout && snap.FreshBreakoutLong;
            bool freshShort = enableFreshBreakout && snap.FreshBreakoutShort;

            bool longTrigger = snap.PullbackLong || snap.RecentCrossLong || snap.ContinuationLong || freshLong;
            bool shortTrigger = snap.PullbackShort || snap.RecentCrossShort || snap.ContinuationShort || freshShort;

            EntryArchetype longArch = EntryArchetype.None;
            if (freshLong) longArch = EntryArchetype.Breakout;
            else if (snap.PullbackLong) longArch = EntryArchetype.Pullback;
            else if (snap.ContinuationLong) longArch = EntryArchetype.Continuation;
            else if (snap.RecentCrossLong) longArch = EntryArchetype.Pullback;

            EntryArchetype shortArch = EntryArchetype.None;
            if (freshShort) shortArch = EntryArchetype.Breakout;
            else if (snap.PullbackShort) shortArch = EntryArchetype.Pullback;
            else if (snap.ContinuationShort) shortArch = EntryArchetype.Continuation;
            else if (snap.RecentCrossShort) shortArch = EntryArchetype.Pullback;

            return new ArchetypeEvaluation(longArch, shortArch, longTrigger, shortTrigger);
        }
    }
}
