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

        public VarietyCookingRecord(Map map) : base(map)
        {
        }

        public List<string> CheckRecentRecipes(Pawn chef)
        {
            if (chef == null) return null;
            if (!this.recentByPawn.TryGetValue(chef, out RecentListEx listEx)) return null;
            List<string> list = listEx.entries;
            if (list == null || list.Count == 0) return null;

            int skillLevel = chef.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? chef.RaceProps.mechFixedSkillLevel;
            int take = Mathf.Max(1, skillLevel / 2);
            int start = Mathf.Max(0, list.Count - take);
            return list.GetRange(start, list.Count - start);
        }

        public void UpdateCookingRecord(Pawn chef, List<ThingCount> chosen)
        {
            if (chef == null) return;
            if (!this.recentByPawn.TryGetValue(chef, out RecentListEx listEx) || listEx == null)
            {
                listEx = new RecentListEx();
                this.recentByPawn[chef] = listEx;
            }

            for (int i = 0; i < chosen.Count; i++)
            {
                listEx.entries.Add(chosen[i].Thing.def.label);
            }

            int depth = Mathf.Max(1, ModSettings_VarietyMatters.cookingMemoryDepth);
            if (listEx.entries.Count > depth)
            {
                listEx.entries.RemoveRange(0, listEx.entries.Count - depth);
            }
        }

        public override void MapRemoved()
        {
            this.recentByPawn?.Clear();
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
