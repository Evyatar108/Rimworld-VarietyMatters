using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VarietyMatters
{
	// Token: 0x02000012 RID: 18
	[HarmonyPatch]
	public class VarietyCooking : WorkGiver_DoBill
	{
		// Token: 0x17000006 RID: 6
		// (get) Token: 0x0600002E RID: 46 RVA: 0x00004158 File Offset: 0x00002358
		// (set) Token: 0x0600002F RID: 47 RVA: 0x0000415F File Offset: 0x0000235F
		private static Pawn Chef { get; set; }

		// Token: 0x06000030 RID: 48 RVA: 0x00004168 File Offset: 0x00002368
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
		[HarmonyPrefix]
		private static void GetChef(Pawn pawn)
		{
			bool flag = !ModSettings_VarietyMatters.ignoreIngredients && ModSettings_VarietyMatters.preferVariety;
			if (flag)
			{
				VarietyCooking.Chef = pawn;
			}
		}

		// Token: 0x06000031 RID: 49 RVA: 0x00004194 File Offset: 0x00002394
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
		[HarmonyPrefix]
		private static bool BestVariety_AllowMix(ref bool __result, List<Thing> availableThings, Bill bill, List<ThingCount> chosen, IntVec3 rootCell)
		{
            bool flag = ModSettings_VarietyMatters.ignoreIngredients || !ModSettings_VarietyMatters.preferVariety;
			bool result;
			if (flag)
			{
				result = true;
			}
			else
			{
				bool flag2;
				if (bill.recipe.workSkill == SkillDefOf.Cooking)
				{
					ThingDef producedThingDef = bill.recipe.ProducedThingDef;
					if (((producedThingDef != null) ? producedThingDef.ingestible : null) != null)
					{
						flag2 = (bill.recipe.specialProducts != null);
						goto IL_5B;
					}
				}
				flag2 = true;
				IL_5B:
				bool flag3 = flag2;
				if (flag3)
				{
					result = true;
				}
				else
				{
					chosen.Clear();
					VarietyCooking.SortIngredients(availableThings, rootCell);
					for (int i = 0; i < bill.recipe.ingredients.Count; i++)
					{
						IngredientCount ingredientCount = bill.recipe.ingredients[i];
						float num = ingredientCount.GetBaseCount();
						for (int j = 0; j < availableThings.Count; j++)
						{
							Thing thing = availableThings[j];
							bool flag4 = ingredientCount.filter.Allows(thing) && (ingredientCount.IsFixedIngredient || bill.ingredientFilter.Allows(thing));
							if (flag4)
							{
								float num2 = bill.recipe.IngredientValueGetter.ValuePerUnitOf(thing.def);
								int num3 = Mathf.Min(Mathf.CeilToInt(num / num2), thing.stackCount);
								ThingCountUtility.AddToList(chosen, thing, num3);
								num -= (float)num3 * num2;
								bool flag5 = num <= 0.0001f;
								if (flag5)
								{
									break;
								}
							}
						}
						bool flag6 = num > 0.0001f;
						if (flag6)
						{
							__result = false;
							return false;
						}
					}
					VarietyCooking.Chef.MapHeld.GetComponent<VarietyCookingRecord>().UpdateCookingRecord(chosen);
					__result = true;
					result = false;
				}
			}
			return result;
		}

		// Token: 0x06000032 RID: 50 RVA: 0x00004350 File Offset: 0x00002550
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_NoMix")]
		[HarmonyPrefix]
		private static void BestVariety_NoMix(List<Thing> availableThings, Bill bill, IntVec3 rootCell, ref bool alreadySorted)
		{
			bool flag;
			if (!ModSettings_VarietyMatters.ignoreIngredients && ModSettings_VarietyMatters.preferVariety && bill.recipe.workSkill == SkillDefOf.Cooking)
			{
				ThingDef producedThingDef = bill.recipe.ProducedThingDef;
				if (((producedThingDef != null) ? producedThingDef.ingestible : null) != null)
				{
					flag = (bill.recipe.specialProducts == null);
					goto IL_4B;
				}
			}
			flag = false;
			IL_4B:
			bool flag2 = flag;
			if (flag2)
			{
				VarietyCooking.SortIngredients(availableThings, rootCell);
				alreadySorted = true;
			}
		}

		// Token: 0x06000033 RID: 51 RVA: 0x000043BC File Offset: 0x000025BC
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_NoMix")]
		[HarmonyPostfix]
		private static void ChosenVariety_NoMix(bool __result, Bill bill, List<ThingCount> chosen, bool alreadySorted)
		{
			bool flag;
			if (__result && alreadySorted && !ModSettings_VarietyMatters.ignoreIngredients && ModSettings_VarietyMatters.preferVariety && bill.recipe.workSkill == SkillDefOf.Cooking)
			{
				ThingDef producedThingDef = bill.recipe.ProducedThingDef;
				if (((producedThingDef != null) ? producedThingDef.ingestible : null) != null)
				{
					flag = (bill.recipe.specialProducts == null);
					goto IL_50;
				}
			}
			flag = false;
			IL_50:
			bool flag2 = flag;
			if (flag2)
			{
				VarietyCooking.Chef.MapHeld.GetComponent<VarietyCookingRecord>().UpdateCookingRecord(chosen);
			}
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00004438 File Offset: 0x00002638
		private static void SortIngredients(List<Thing> availableThings, IntVec3 rootCell)
		{
			List<string> curVarieties = VarietyCooking.Chef.MapHeld.GetComponent<VarietyCookingRecord>().CheckRecentRecipes(VarietyCooking.Chef);
			Comparison<Thing> comparison = delegate(Thing t1, Thing t2)
			{
				float num = (t1.Position - rootCell).LengthHorizontal;
				float num2 = (t2.Position - rootCell).LengthHorizontal;
				bool flag = curVarieties != null && t1.def.label != t2.def.label;
				if (flag)
				{
					for (int i = 0; i < curVarieties.Count; i++)
					{
						bool flag2 = curVarieties[i] == t1.def.label;
						if (flag2)
						{
							num += 1f;
						}
						else
						{
							bool flag3 = curVarieties[i] == t2.def.label;
							if (flag3)
							{
								num2 += 1f;
							}
						}
					}
				}
				bool preferSpoiling = ModSettings_VarietyMatters.preferSpoiling;
				if (preferSpoiling)
				{
					CompRottable compRottable = ThingCompUtility.TryGetComp<CompRottable>(t1);
					CompRottable compRottable2 = ThingCompUtility.TryGetComp<CompRottable>(t1);
					bool flag4 = compRottable != null;
					if (flag4)
					{
						num += (1f - compRottable.RotProgressPct) * 10f;
					}
					bool flag5 = compRottable2 != null;
					if (flag5)
					{
						num2 += (1f - compRottable2.RotProgressPct) * 10f;
					}
				}
				return num.CompareTo(num2);
			};
			availableThings.Sort(comparison);
		}
	}
}
