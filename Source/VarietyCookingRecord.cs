namespace VarietyMatters
{
    using System;
    using System.Collections.Generic;
    using RimWorld;
    using UnityEngine;
    using Verse;

    // Token: 0x02000013 RID: 19
    public class VarietyCookingRecord : MapComponent
	{
        private List<string> recentlyCooked;

        public VarietyCookingRecord(Map map) : base(map)
		{
			this.recentlyCooked = new List<string>();
		}

		// Token: 0x06000037 RID: 55 RVA: 0x000044B4 File Offset: 0x000026B4
		public List<string> CheckRecentRecipes(Pawn chef)
		{
			bool flag = this.recentlyCooked != null && this.recentlyCooked.Count > 0;
			List<string> result;
			if (flag)
			{
				int skillLevel = chef.skills?.GetSkill(SkillDefOf.Cooking)?.Level ?? chef.RaceProps.mechFixedSkillLevel;

                result = this.recentlyCooked.GetRange(0, Mathf.Min(this.recentlyCooked.Count, skillLevel / 2));
			}
			else
			{
				result = null;
			}
			return result;
		}

		// Token: 0x06000038 RID: 56 RVA: 0x0000451C File Offset: 0x0000271C
		public void UpdateCookingRecord(List<ThingCount> chosen)
		{
			for (int i = 0; i < chosen.Count; i++)
			{
				this.recentlyCooked.Add(chosen[i].Thing.def.label);
			}
			bool flag = this.recentlyCooked.Count > 10;
			if (flag)
			{
				this.recentlyCooked.RemoveRange(0, this.recentlyCooked.Count - 10);
			}
		}

		// Token: 0x06000039 RID: 57 RVA: 0x00004596 File Offset: 0x00002796
		public override void MapRemoved()
		{
			this.recentlyCooked.Clear();
			base.MapRemoved();
		}

		// Token: 0x0600003A RID: 58 RVA: 0x000045AC File Offset: 0x000027AC
		public override void ExposeData()
		{
			Scribe_Collections.Look<string>(ref this.recentlyCooked, "RecentlyCooked", (LookMode)1, Array.Empty<object>());
			base.ExposeData();
		}
	}
}
