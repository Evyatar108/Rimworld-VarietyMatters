using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VarietyMatters
{
	// Token: 0x0200000D RID: 13
	[HarmonyPatch]
	public static class Patch_FoodUtility
	{
		// Token: 0x06000020 RID: 32 RVA: 0x00003298 File Offset: 0x00001498
		[HarmonyPatch(typeof(FoodUtility), "FoodOptimality")]
		[HarmonyPostfix]
		private static void Postfix_FoodOptimality(ref float __result, Pawn eater, Thing foodSource, bool takingToInventory)
		{
			bool flag;
			if (!takingToInventory)
			{
				Pawn_VarietyTracker varietyRecord = VarietyRecord.GetVarietyRecord(eater);
				if (((varietyRecord != null) ? varietyRecord.recentlyConsumed : null) != null && ThingCompUtility.TryGetComp<CompVariety>(foodSource) != null && eater.needs.TryGetNeed<Need_FoodVariety>() != null && !eater.needs.TryGetNeed<Need_FoodVariety>().Disabled && !FoodUtility.Starving(eater) && !eater.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning, true) && (!ModSettings_VarietyMatters.sickPawns || !HealthAIUtility.ShouldSeekMedicalRest(eater)))
				{
					flag = FoodUtility.WillGiveNegativeThoughts(foodSource, eater);
					goto IL_80;
				}
			}
			flag = true;
			IL_80:
			bool flag2 = flag;
			if (!flag2)
			{
				List<string> recentlyConsumed = VarietyRecord.GetVarietyRecord(eater).recentlyConsumed;
				CompIngredients compIngredients = ThingCompUtility.TryGetComp<CompIngredients>(foodSource);
				bool maxVariety = ModSettings_VarietyMatters.maxVariety;
				if (maxVariety)
				{
					bool flag3 = !recentlyConsumed.Contains(foodSource.def.label) || ((int)foodSource.def.ingestible.preferability == 5 && !recentlyConsumed.Contains("Raw" + foodSource.def.label));
					if (flag3)
					{
						__result += 60f;
					}
					else
					{
						bool flag4 = compIngredients == null;
						if (!flag4)
						{
							for (int i = 0; i < compIngredients.ingredients.Count; i++)
							{
								bool flag5 = !recentlyConsumed.Contains(compIngredients.ingredients[i].label);
								if (flag5)
								{
									__result += 60f;
									break;
								}
							}
						}
					}
				}
				else
				{
					bool flag6 = !ModSettings_VarietyMatters.ignoreIngredients;
					if (flag6)
					{
						bool flag7 = compIngredients != null;
						if (flag7)
						{
							for (int j = 0; j < compIngredients.ingredients.Count; j++)
							{
								bool flag8 = !recentlyConsumed.Contains(compIngredients.ingredients[j].label);
								if (flag8)
								{
									__result += 60f;
									break;
								}
							}
						}
						else
						{
							bool flag9 = !recentlyConsumed.Contains(foodSource.def.label) || ((int)foodSource.def.ingestible.preferability == 5 && !recentlyConsumed.Contains("Raw" + foodSource.def.label));
							if (flag9)
							{
								__result += 60f;
							}
						}
					}
					else
					{
						bool flag10 = !recentlyConsumed.Contains(foodSource.def.label) || ((int)foodSource.def.ingestible.preferability == 5 && !recentlyConsumed.Contains("Raw" + foodSource.def.label));
						if (flag10)
						{
							__result += 60f;
						}
					}
				}
			}
		}
	}
}
