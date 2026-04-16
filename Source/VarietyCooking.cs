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
					bool recipeAllowsCap = bill.recipe.specialProducts == null && ModSettings_VarietyMatters.numIngredients > 1;
					for (int i = 0; i < bill.recipe.ingredients.Count; i++)
					{
						IngredientCount ingredientCount = bill.recipe.ingredients[i];
						bool applyCapForSlot = recipeAllowsCap && !ingredientCount.IsFixedIngredient;
						if (!VarietyCooking.TryFillIngredientSlotWithCap(availableThings, bill, ingredientCount, chosen, applyCapForSlot))
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

		// Two-pass fill: Pass 1 caps per ThingDef to encourage mixing; Pass 2 drains residual uncapped.
		private static bool TryFillIngredientSlotWithCap(List<Thing> availableThings, Bill bill, IngredientCount ingredientCount, List<ThingCount> chosen, bool applyCap)
		{
			float slotBaseCount = ingredientCount.GetBaseCount();
			float remaining = slotBaseCount;

			if (applyCap)
			{
				HashSet<ThingDef> distinctDefs = null;
				for (int j = 0; j < availableThings.Count; j++)
				{
					Thing t = availableThings[j];
					if (!ingredientCount.filter.Allows(t)) continue;
					if (!ingredientCount.IsFixedIngredient && !bill.ingredientFilter.Allows(t)) continue;
					if (distinctDefs == null) distinctDefs = new HashSet<ThingDef>();
					distinctDefs.Add(t.def);
				}
				int distinctDefCount = distinctDefs != null ? distinctDefs.Count : 0;

				if (distinctDefCount > 1)
				{
					int perDefCap = Mathf.Max(1, Mathf.CeilToInt(slotBaseCount / (float)Mathf.Max(2, distinctDefCount)));
					Dictionary<ThingDef, int> consumedPerDef = new Dictionary<ThingDef, int>();
					for (int j = 0; j < availableThings.Count; j++)
					{
						if (remaining <= 0.0001f) break;
						Thing t = availableThings[j];
						if (!ingredientCount.filter.Allows(t)) continue;
						if (!ingredientCount.IsFixedIngredient && !bill.ingredientFilter.Allows(t)) continue;

						consumedPerDef.TryGetValue(t.def, out int alreadyForDef);
						int defBudget = perDefCap - alreadyForDef;
						if (defBudget <= 0) continue;

						float valuePerUnit = bill.recipe.IngredientValueGetter.ValuePerUnitOf(t.def);
						int needed = Mathf.CeilToInt(remaining / valuePerUnit);
						int take = Mathf.Min(Mathf.Min(needed, t.stackCount), defBudget);
						if (take <= 0) continue;

						ThingCountUtility.AddToList(chosen, t, take);
						consumedPerDef[t.def] = alreadyForDef + take;
						remaining -= (float)take * valuePerUnit;
					}
				}
			}

			if (remaining <= 0.0001f) return true;

			for (int j = 0; j < availableThings.Count; j++)
			{
				if (remaining <= 0.0001f) break;
				Thing t = availableThings[j];
				if (!ingredientCount.filter.Allows(t)) continue;
				if (!ingredientCount.IsFixedIngredient && !bill.ingredientFilter.Allows(t)) continue;

				int alreadyFromThing = 0;
				for (int k = 0; k < chosen.Count; k++)
				{
					if (chosen[k].Thing == t) { alreadyFromThing = chosen[k].Count; break; }
				}
				int stackLeft = t.stackCount - alreadyFromThing;
				if (stackLeft <= 0) continue;

				float valuePerUnit = bill.recipe.IngredientValueGetter.ValuePerUnitOf(t.def);
				int needed = Mathf.CeilToInt(remaining / valuePerUnit);
				int take = Mathf.Min(needed, stackLeft);
				if (take <= 0) continue;

				ThingCountUtility.AddToList(chosen, t, take);
				remaining -= (float)take * valuePerUnit;
			}

			return remaining <= 0.0001f;
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
