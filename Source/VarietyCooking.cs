namespace VarietyMatters
{
    using System;
    using System.Collections.Generic;
    using HarmonyLib;
    using RimWorld;
    using UnityEngine;
    using Verse;

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
			if (ModSettings_VarietyMatters.foodTrackingType != New.FoodTrackingType.ByMealNames && ModSettings_VarietyMatters.preferVariety)
			{
				VarietyCooking.Chef = pawn;
			}
		}

		// Token: 0x06000031 RID: 49 RVA: 0x00004194 File Offset: 0x00002394
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
		[HarmonyPrefix]
		private static bool BestVariety_AllowMix(ref bool __result, List<Thing> availableThings, Bill bill, List<ThingCount> chosen, IntVec3 rootCell)
		{
			bool result;
			if (ModSettings_VarietyMatters.foodTrackingType == New.FoodTrackingType.ByMealNames || !ModSettings_VarietyMatters.preferVariety)
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
						flag2 = bill.recipe.specialProducts != null;
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
					// OPTIMIZATION: Cache component lookup for this operation
					var cookingRecord = VarietyCooking.Chef?.MapHeld?.GetComponent<VarietyCookingRecord>();
					if (cookingRecord == null)
					{
						__result = false;
						return false;
					}
					
					chosen.Clear();
					VarietyCooking.SortIngredients(availableThings, rootCell);
					for (int i = 0; i < bill.recipe.ingredients.Count; i++)
					{
						IngredientCount ingredientCount = bill.recipe.ingredients[i];
						float billRecipeIngredientBaseCount = ingredientCount.GetBaseCount();
						for (int j = 0; j < availableThings.Count; j++)
						{
							Thing availableThing = availableThings[j];
							bool availableThingIsValidIngredient = ingredientCount.filter.Allows(availableThing) && (ingredientCount.IsFixedIngredient || bill.ingredientFilter.Allows(availableThing));
							if (availableThingIsValidIngredient)
							{
								float num2 = bill.recipe.IngredientValueGetter.ValuePerUnitOf(availableThing.def);
								int num3 = Mathf.Min(Mathf.CeilToInt(billRecipeIngredientBaseCount / num2), availableThing.stackCount);
								ThingCountUtility.AddToList(chosen, availableThing, num3);
								billRecipeIngredientBaseCount -= (float)num3 * num2;
								bool flag5 = billRecipeIngredientBaseCount <= 0.0001f;
								if (flag5)
								{
									break;
								}
							}
						}
						bool flag6 = billRecipeIngredientBaseCount > 0.0001f;
						if (flag6)
						{
							__result = false;
							return false;
						}
					}
					// OPTIMIZATION: Use cached component reference
					cookingRecord.UpdateCookingRecord(chosen);
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
			if (ModSettings_VarietyMatters.foodTrackingType != New.FoodTrackingType.ByMealNames && ModSettings_VarietyMatters.preferVariety && bill.recipe.workSkill == SkillDefOf.Cooking)
			{
				ThingDef producedThingDef = bill.recipe.ProducedThingDef;
				if (((producedThingDef != null) ? producedThingDef.ingestible : null) != null)
				{
					flag = bill.recipe.specialProducts == null;
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
			if (__result && alreadySorted && ModSettings_VarietyMatters.foodTrackingType != New.FoodTrackingType.ByMealNames && ModSettings_VarietyMatters.preferVariety && bill.recipe.workSkill == SkillDefOf.Cooking)
			{
				ThingDef producedThingDef = bill.recipe.ProducedThingDef;
				if (((producedThingDef != null) ? producedThingDef.ingestible : null) != null)
				{
					flag = bill.recipe.specialProducts == null;
					goto IL_50;
				}
			}
			flag = false;
			IL_50:
			bool flag2 = flag;
			if (flag2)
			{
				// OPTIMIZATION: Safe component lookup with null check
				var cookingRecord = VarietyCooking.Chef?.MapHeld?.GetComponent<VarietyCookingRecord>();
				cookingRecord?.UpdateCookingRecord(chosen);
			}
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00004438 File Offset: 0x00002638
		private static void SortIngredients(List<Thing> availableThings, IntVec3 rootCell)
		{
			var cookingRecord = VarietyCooking.Chef?.MapHeld?.GetComponent<VarietyCookingRecord>();
			List<string> curVarieties = cookingRecord?.CheckRecentRecipes(VarietyCooking.Chef);

			Dictionary<string, int> occurrenceByLabel = null;
			if (curVarieties != null && curVarieties.Count > 0)
			{
				occurrenceByLabel = new Dictionary<string, int>();
				for (int i = 0; i < curVarieties.Count; i++)
				{
					string lbl = curVarieties[i];
					if (occurrenceByLabel.TryGetValue(lbl, out int c))
					{
						occurrenceByLabel[lbl] = c + 1;
					}
					else
					{
						occurrenceByLabel[lbl] = 1;
					}
				}
			}

			bool preferSpoiling = ModSettings_VarietyMatters.preferSpoiling;
			var sortKeys = new Dictionary<Thing, float>(availableThings.Count);
			for (int i = 0; i < availableThings.Count; i++)
			{
				Thing t = availableThings[i];
				float key = (t.Position - rootCell).LengthHorizontal;
				if (occurrenceByLabel != null && occurrenceByLabel.TryGetValue(t.def.label, out int count))
				{
					key += count * 2f;
				}
				if (preferSpoiling)
				{
					CompRottable compRottable = ThingCompUtility.TryGetComp<CompRottable>(t);
					if (compRottable != null)
					{
						key += (1f - compRottable.RotProgressPct) * 3f;
					}
				}
				sortKeys[t] = key;
			}

			availableThings.Sort((t1, t2) => sortKeys[t1].CompareTo(sortKeys[t2]));
		}
	}
}
