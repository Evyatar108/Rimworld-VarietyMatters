using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VarietyMatters
{
	// Token: 0x02000014 RID: 20
	public class VarietyExpectation
	{
		// Token: 0x17000007 RID: 7
		// (get) Token: 0x0600003B RID: 59 RVA: 0x000045D0 File Offset: 0x000027D0
		public static float ModVarietyFactor
		{
			get
			{
				bool maxVariety = ModSettings_VarietyMatters.maxVariety;
				float result;
				if (maxVariety)
				{
					result = Math.Max(VarietyExpectation.moddedMeals, VarietyExpectation.moddedIngredients) + 0.2f;
				}
				else
				{
					bool ignoreIngredients = ModSettings_VarietyMatters.ignoreIngredients;
					if (ignoreIngredients)
					{
						result = VarietyExpectation.moddedMeals;
					}
					else
					{
						result = VarietyExpectation.moddedIngredients;
					}
				}
				return result;
			}
		}

		// Token: 0x0600003C RID: 60 RVA: 0x0000461C File Offset: 0x0000281C
		public static int GetVarietyExpectation(Pawn ingester)
		{
			float baseVarietyExpectation = VarietyExpectation.GetBaseVarietyExpectation(ingester);
			float num = VarietyExpectation.ApplyAdjustments(ingester, baseVarietyExpectation);
			num = Mathf.Max(num, (float)Mathf.CeilToInt(baseVarietyExpectation * 0.55f));
			return (int)num;
		}

		// Token: 0x0600003D RID: 61 RVA: 0x00004654 File Offset: 0x00002854
		public static float GetBaseVarietyExpectation(Pawn ingester)
		{
			ExpectationDef expectationDef = ExpectationsUtility.CurrentExpectationFor(ingester);
			float num = (expectationDef == ExpectationDefOf.ExtremelyLow) ? ((float)ModSettings_VarietyMatters.extremelyLowVariety) : ((expectationDef == ExpectationDefOf.VeryLow) ? ((float)ModSettings_VarietyMatters.veryLowVariety) : ((expectationDef == ExpectationDefOf.Low) ? ((float)ModSettings_VarietyMatters.lowVariety) : ((expectationDef == ExpectationDefOf.Moderate) ? ((float)ModSettings_VarietyMatters.moderateVariety) : ((expectationDef == ExpectationDefOf.High) ? ((float)ModSettings_VarietyMatters.highVariety) : ((expectationDef != ExpectationDefOf.SkyHigh) ? ((float)ModSettings_VarietyMatters.nobleVariety) : ((float)ModSettings_VarietyMatters.skyHighVariety))))));
			bool flag = ModsConfig.IdeologyActive && ingester.Ideo != null && !ModSettings_VarietyMatters.ignoreIngredients;
			if (flag)
			{
				List<Precept> preceptsListForReading = ingester.Ideo.PreceptsListForReading;
				for (int i = 0; i < preceptsListForReading.Count; i++)
				{
					bool flag2 = preceptsListForReading[i].def == DefOf_VarietyMatters.FungusEating_Preferred;
					if (flag2)
					{
						num = ((!ModSettings_VarietyMatters.maxVariety) ? ((float)ModSettings_VarietyMatters.extremelyLowVariety) : (num * 0.5f));
					}
				}

				if (ingester.IsSlave)
				{
					num = Math.Max(1f, num / ModSettings_VarietyMatters.divideSlaveVarietyBy);
				}

                if (ingester.IsPrisoner)
                {
                    num = Math.Max(1f, num / ModSettings_VarietyMatters.dividePrisonerVarietyBy);
                }
            }

			return num;
		}

		// Token: 0x0600003E RID: 62 RVA: 0x00004754 File Offset: 0x00002954
		private static float ApplyAdjustments(Pawn ingester, float baseExpectation)
		{
			bool curNeedAdjustments = ModSettings_VarietyMatters.curNeedAdjustments;
			if (curNeedAdjustments)
			{
				Need_FoodVariety need_FoodVariety = ingester.needs.TryGetNeed<Need_FoodVariety>();
				bool flag = need_FoodVariety.CurLevel >= 0.6f;
				if (flag)
				{
					baseExpectation += (need_FoodVariety.CurLevel - 0.5f) / 0.1f * Math.Min(baseExpectation * 0.16f, 1f);
				}
				bool flag2 = need_FoodVariety.CurLevel <= 0.4f;
				if (flag2)
				{
					baseExpectation *= 0.75f + need_FoodVariety.CurLevel / 2f;
				}
			}
			float num = 1f;
			bool foodModAdjustments = ModSettings_VarietyMatters.foodModAdjustments;
			if (foodModAdjustments)
			{
				num += VarietyExpectation.ModVarietyFactor;
			}
			baseExpectation *= Mathf.Min(num, 2f);
			bool flag3 = ModSettings_VarietyMatters.tempAdjustments && ingester.MapHeld != null;
			if (flag3)
			{
				float outdoorTemp = ingester.MapHeld.mapTemperature.OutdoorTemp;
				bool flag4 = outdoorTemp < -10f;
				if (flag4)
				{
					baseExpectation *= 0.75f;
				}
				bool flag5 = outdoorTemp < 0f;
				if (flag5)
				{
					baseExpectation *= 0.8f;
				}
				else
				{
					bool flag6 = outdoorTemp < 10f || outdoorTemp > 44f;
					if (flag6)
					{
						baseExpectation *= 0.9f;
					}
				}
			}
			return baseExpectation;
		}

		// Token: 0x0400002D RID: 45
		public static float moddedMeals;

		// Token: 0x0400002E RID: 46
		public static float moddedIngredients;

		// Token: 0x0400002F RID: 47
		public static float meatTypes;
	}
}
