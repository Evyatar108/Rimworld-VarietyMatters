namespace VarietyMatters.New
{
    using RimWorld;

    // Settings-driven mood + duration for VM_AteFavourite. Vanilla Thought_Memory reads
    // both from the def (XML constants); subclassing lets players tune mood amount and
    // duration at runtime without mod reload. Existing in-pawn memories keep their original
    // values — only newly-fired thoughts read current settings. That means a setting change
    // takes effect on the next favourite-meal trigger, not retroactively.
    public class Thought_Memory_VMAteFavourite : Thought_Memory
    {
        public override int DurationTicks
        {
            get
            {
                int hours = ModSettings_VarietyMatters.favouriteMoodDurationHours;
                if (hours < 1) hours = 1;
                return hours * 2500;  // 2500 ticks = 1 in-game hour
            }
        }

        public override float MoodOffset()
        {
            return ModSettings_VarietyMatters.favouriteMoodBonus;
        }
    }
}
