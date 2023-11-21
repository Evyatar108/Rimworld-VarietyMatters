using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VarietyMatters
{
	// Token: 0x0200000C RID: 12
	public class Need_FoodVariety : Need
	{
		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000018 RID: 24 RVA: 0x00002F8C File Offset: 0x0000118C
		public MenuCategory CurCategory
		{
			get
			{
				bool disabled = this.Disabled;
				MenuCategory result;
				if (disabled)
				{
					result = MenuCategory.None;
				}
				else
				{
					bool flag = this.CurLevel > 0.99f;
					if (flag)
					{
						result = MenuCategory.Gourmet;
					}
					else
					{
						bool flag2 = this.CurLevel > 0.85f;
						if (flag2)
						{
							result = MenuCategory.Lavish;
						}
						else
						{
							bool flag3 = this.CurLevel > 0.7f;
							if (flag3)
							{
								result = MenuCategory.Fine;
							}
							else
							{
								bool flag4 = this.CurLevel > 0.3f;
								if (flag4)
								{
									result = MenuCategory.Simple;
								}
								else
								{
									bool flag5 = this.CurLevel > 0.15f;
									if (flag5)
									{
										result = MenuCategory.Limited;
									}
									else
									{
										bool flag6 = this.CurLevel > 0f;
										if (flag6)
										{
											result = MenuCategory.Sparse;
										}
										else
										{
											result = MenuCategory.Empty;
										}
									}
								}
							}
						}
					}
				}
				return result;
			}
		}

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000019 RID: 25 RVA: 0x0000303D File Offset: 0x0000123D
		public override int GUIChangeArrow
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x0600001A RID: 26 RVA: 0x00003040 File Offset: 0x00001240
		public override bool ShowOnNeedList
		{
			get
			{
				return !this.Disabled;
			}
		}

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x0600001B RID: 27 RVA: 0x0000304C File Offset: 0x0000124C
		public bool Disabled
		{
			get
			{
				bool doesNotHaveVarietyNeed = this.pawn.Dead || !TraitDef_ModExtension.NeedsVariety(this.pawn) || this.pawn.needs.food == null || this.pawn.needs.mood == null;
				if (doesNotHaveVarietyNeed)
				{
					return true;
				}

				if (!ModSettings_VarietyMatters.slavesHaveVarietyNeed && this.pawn.IsSlave)
				{
					return true;
				}

                if (!ModSettings_VarietyMatters.prisonersHaveVarietyNeed && this.pawn.IsPrisoner)
                {
                    return true;
                }

                bool isRaceWithVariety = ModSettings_VarietyMatters.raceVariety.ContainsKey(this.pawn.def.label) && ModSettings_VarietyMatters.raceVariety[this.pawn.def.label];
				return !isRaceWithVariety;
			}
		}

		// Token: 0x0600001C RID: 28 RVA: 0x000030EC File Offset: 0x000012EC
		public Need_FoodVariety(Pawn pawn) : base(pawn)
		{
			this.threshPercents = new List<float>();
			this.threshPercents.Add(0.15f);
			this.threshPercents.Add(0.3f);
			this.threshPercents.Add(0.7f);
			this.threshPercents.Add(0.85f);
		}

		// Token: 0x0600001D RID: 29 RVA: 0x00003151 File Offset: 0x00001351
		public override void SetInitialLevel()
		{
			this.CurLevel = 0.5f;
		}

		// Token: 0x0600001E RID: 30 RVA: 0x00003160 File Offset: 0x00001360
		public override void NeedInterval()
		{
			bool disabled = this.Disabled;
			if (disabled)
			{
				this.CurLevel = 0.5f;
			}
		}

		// Token: 0x0600001F RID: 31 RVA: 0x00003188 File Offset: 0x00001388
		public override string GetTipString()
		{
			string tipString = base.GetTipString();
			string text = VarietyExpectation.GetVarietyExpectation(this.pawn).ToString();
			string text2 = "0";
			string text3 = "I haven't eaten in ages";
			Pawn_VarietyTracker varietyRecord = VarietyRecord.GetVarietyRecord(this.pawn);
			bool flag = varietyRecord != null && varietyRecord.recentlyConsumed != null;
			if (flag)
			{
				text2 = varietyRecord.recentVarieties.ToString();
				List<string> lastVariety = varietyRecord.lastVariety;
				bool flag2 = lastVariety.Count > 0;
				if (flag2)
				{
					text3 = GenText.CapitalizeFirst(lastVariety[0]);
				}
				bool flag3 = lastVariety.Count > 1;
				if (flag3)
				{
					for (int i = 1; i < lastVariety.Count; i++)
					{
						text3 = text3 + ", " + lastVariety[i];
					}
				}
			}
			return string.Concat(new string[]
			{
				tipString,
				"\n\nVarieties Expected: ",
				text,
				"\nRecent Varieties: ",
				text2,
				"\nLast: ",
				text3
			});
		}
	}
}
