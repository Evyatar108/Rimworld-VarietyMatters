namespace VarietyMatters
{
    using System;
    using System.Collections.Generic;
    using RimWorld;
    using UnityEngine;
    using Verse;

    public class RecentListEx : IExposable
    {
        public List<string> entries = new List<string>();

        public void ExposeData()
        {
            Scribe_Collections.Look<string>(ref this.entries, "entries", LookMode.Value, Array.Empty<object>());
            if (Scribe.mode == LoadSaveMode.PostLoadInit && this.entries == null)
            {
                this.entries = new List<string>();
            }
        }
    }

    // Token: 0x02000013 RID: 19
    public class VarietyCookingRecord : MapComponent
    {
        private const int CleanupInterval = 60000;

        private Dictionary<Pawn, RecentListEx> recentByPawn = new Dictionary<Pawn, RecentListEx>();
        private List<Pawn> scribeKeys;
        private List<RecentListEx> scribeValues;

        // F-027: transient "just-cooked last meal" def-label set per chef. NOT Scribe-persisted; rebuilds
        // on first cook post-load. Used by SortIngredients for a strong immediate-repeat penalty that
        // scales with the variety-pull setting so close-by ingredients don't win purely on distance.
        private Dictionary<Pawn, HashSet<string>> lastMealDefsByPawn = new Dictionary<Pawn, HashSet<string>>();

        public VarietyCookingRecord(Map map) : base(map)
        {
        }

        public (List<string> list, int startIndex, int count) CheckRecentRecipes(Pawn chef)
        {
            if (chef == null) return (null, 0, 0);
            if (!this.recentByPawn.TryGetValue(chef, out RecentListEx listEx)) return (null, 0, 0);
            List<string> list = listEx.entries;
            if (list == null || list.Count == 0) return (null, 0, 0);

            // Use the full recent window. The list is already capped at ModSettings_VarietyMatters.cookingMemoryDepth by
            // UpdateCookingRecord. The prior skill-based sub-slice (take = skillLevel / 2) dated from a single-def-per-meal
            // baseline; once the E' water-fill injects numIngredients distinct defs per meal, each meal consumes ~numIngredients
            // entries and a skill-6 cook only saw the LAST meal's picks, causing two-meal oscillation (A/B/C → D/E/F → A/B/C).
            // The cookingMemoryDepth setting (user-tunable) is the proper knob; skill level scales other cooking traits in vanilla.
            int skillLevel = chef.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? chef.RaceProps.mechFixedSkillLevel;
            int numIngredients = Mathf.Max(1, ModSettings_VarietyMatters.numIngredients);
            // Keep a mild skill-scaled floor so very low-skill / no-skill cooks still rotate over at least one full meal's depth.
            int take = Mathf.Max(numIngredients, skillLevel * numIngredients / 2);
            take = Mathf.Min(take, list.Count);
            int start = Mathf.Max(0, list.Count - take);
            return (list, start, list.Count - start);
        }

        public void UpdateCookingRecord(Pawn chef, List<ThingCount> chosen)
        {
            if (chef == null) return;
            if (!this.recentByPawn.TryGetValue(chef, out RecentListEx listEx) || listEx == null)
            {
                listEx = new RecentListEx();
                this.recentByPawn[chef] = listEx;
            }

            // F-027: rebuild the "last meal" def-label set for this chef. Replaces contents so only
            // the *just-picked* defs get the strong immediate-repeat penalty; older defs only get the
            // softer linear recent-matches penalty.
            if (!this.lastMealDefsByPawn.TryGetValue(chef, out HashSet<string> lastMeal) || lastMeal == null)
            {
                lastMeal = new HashSet<string>();
                this.lastMealDefsByPawn[chef] = lastMeal;
            }
            lastMeal.Clear();

            for (int i = 0; i < chosen.Count; i++)
            {
                string label = chosen[i].Thing.def.label;
                listEx.entries.Add(label);
                lastMeal.Add(label);
            }

            int depth = Mathf.Max(1, ModSettings_VarietyMatters.cookingMemoryDepth);
            if (listEx.entries.Count > depth)
            {
                listEx.entries.RemoveRange(0, listEx.entries.Count - depth);
            }
        }

        public bool IsDefInLastMeal(Pawn chef, string defLabel)
        {
            if (chef == null || defLabel == null) return false;
            return this.lastMealDefsByPawn.TryGetValue(chef, out HashSet<string> lastMeal)
                && lastMeal != null
                && lastMeal.Contains(defLabel);
        }

        public override void MapRemoved()
        {
            this.recentByPawn?.Clear();
            this.lastMealDefsByPawn?.Clear();
            base.MapRemoved();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (Find.TickManager.TicksGame % CleanupInterval != 0) return;
            this.CleanupDeadPawns();
        }

        private void CleanupDeadPawns()
        {
            if (this.recentByPawn == null || this.recentByPawn.Count == 0) return;

            List<Pawn> toRemove = null;
            foreach (var kvp in this.recentByPawn)
            {
                Pawn p = kvp.Key;
                if (p == null || p.Destroyed || p.Map != this.map)
                {
                    if (toRemove == null) toRemove = new List<Pawn>();
                    toRemove.Add(p);
                }
            }
            if (toRemove == null) return;
            for (int i = 0; i < toRemove.Count; i++)
            {
                this.recentByPawn.Remove(toRemove[i]);
                this.lastMealDefsByPawn?.Remove(toRemove[i]);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            List<string> legacyRecentlyCooked = null;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look<string>(ref legacyRecentlyCooked, "RecentlyCooked", LookMode.Value, Array.Empty<object>());
            }

            Scribe_Collections.Look<Pawn, RecentListEx>(
                ref this.recentByPawn,
                "RecentByPawn",
                LookMode.Reference,
                LookMode.Deep,
                ref this.scribeKeys,
                ref this.scribeValues);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.recentByPawn == null)
                {
                    this.recentByPawn = new Dictionary<Pawn, RecentListEx>();
                }
            }
        }
    }
}
