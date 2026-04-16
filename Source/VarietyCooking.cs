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
		private static readonly Dictionary<Bill, Pawn> chefByBill = new Dictionary<Bill, Pawn>();

		// Token: 0x06000030 RID: 48 RVA: 0x00004168 File Offset: 0x00002368
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
		[HarmonyPrefix]
		private static void GetChef(Bill bill, Pawn pawn)
		{
			if (bill == null) return;
			if (ModSettings_VarietyMatters.foodTrackingType != New.FoodTrackingType.ByMealNames && ModSettings_VarietyMatters.preferVariety)
			{
				chefByBill[bill] = pawn;
			}
		}

		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
		[HarmonyPostfix]
		private static void ClearChef(Bill bill)
		{
			if (bill != null) chefByBill.Remove(bill);
		}

		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
		[HarmonyFinalizer]
		private static void ClearChefOnException(Bill bill)
		{
			if (bill != null) chefByBill.Remove(bill);
		}

		// Token: 0x06000031 RID: 49 RVA: 0x00004194 File Offset: 0x00002394
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
		[HarmonyPrefix]
		private static bool BestVariety_AllowMix(ref bool __result, List<Thing> availableThings, Bill bill, List<ThingCount> chosen, IntVec3 rootCell)
		{
			if (ModSettings_VarietyMatters.foodTrackingType == New.FoodTrackingType.ByMealNames || !ModSettings_VarietyMatters.preferVariety)
			{
				return true;
			}

			bool gate;
			if (bill.recipe.workSkill == SkillDefOf.Cooking)
			{
				ThingDef producedThingDef = bill.recipe.ProducedThingDef;
				gate = (producedThingDef?.ingestible != null) ? (bill.recipe.specialProducts != null) : true;
			}
			else
			{
				gate = true;
			}
			if (gate) return true;

			chefByBill.TryGetValue(bill, out Pawn chef);
			var cookingRecord = (bill.billStack?.billGiver as Thing)?.Map?.GetComponent<VarietyCookingRecord>();
			if (cookingRecord == null)
			{
				return true;
			}

			chosen.Clear();
			VarietyCooking.SortIngredients(availableThings, rootCell, chef, bill);
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
			__result = true;
			return false;
		}

		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_AllowMix")]
		[HarmonyPostfix]
		private static void UpdateVariety_AllowMix(bool __result, Bill bill, List<ThingCount> chosen)
		{
			if (!__result) return;
			if (ModSettings_VarietyMatters.foodTrackingType == New.FoodTrackingType.ByMealNames || !ModSettings_VarietyMatters.preferVariety) return;
			if (bill.recipe.workSkill != SkillDefOf.Cooking) return;
			ThingDef producedThingDef = bill.recipe.ProducedThingDef;
			if (producedThingDef?.ingestible == null) return;
			if (bill.recipe.specialProducts != null) return;

			chefByBill.TryGetValue(bill, out Pawn chef);
			var cookingRecord = (bill.billStack?.billGiver as Thing)?.Map?.GetComponent<VarietyCookingRecord>();
			cookingRecord?.UpdateCookingRecord(chef, chosen);
		}

		// Token: 0x06000032 RID: 50 RVA: 0x00004350 File Offset: 0x00002550
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_NoMix")]
		[HarmonyPrefix]
		private static void BestVariety_NoMix(List<Thing> availableThings, Bill bill, IntVec3 rootCell, ref bool alreadySorted)
		{
			bool flag = false;
			if (ModSettings_VarietyMatters.foodTrackingType != New.FoodTrackingType.ByMealNames && ModSettings_VarietyMatters.preferVariety && bill.recipe.workSkill == SkillDefOf.Cooking)
			{
				ThingDef producedThingDef = bill.recipe.ProducedThingDef;
				if (producedThingDef?.ingestible != null)
				{
					flag = bill.recipe.specialProducts == null;
				}
			}
			if (flag)
			{
				chefByBill.TryGetValue(bill, out Pawn chef);
				VarietyCooking.SortIngredients(availableThings, rootCell, chef, bill);
				alreadySorted = true;
			}
		}

		// Token: 0x06000033 RID: 51 RVA: 0x000043BC File Offset: 0x000025BC
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet_NoMix")]
		[HarmonyPostfix]
		private static void ChosenVariety_NoMix(bool __result, Bill bill, List<ThingCount> chosen, bool alreadySorted)
		{
			if (!(__result && alreadySorted)) return;
			if (ModSettings_VarietyMatters.foodTrackingType == New.FoodTrackingType.ByMealNames || !ModSettings_VarietyMatters.preferVariety) return;
			if (bill.recipe.workSkill != SkillDefOf.Cooking) return;
			ThingDef producedThingDef = bill.recipe.ProducedThingDef;
			if (producedThingDef?.ingestible == null) return;
			if (bill.recipe.specialProducts != null) return;

			chefByBill.TryGetValue(bill, out Pawn chef);
			var cookingRecord = (bill.billStack?.billGiver as Thing)?.Map?.GetComponent<VarietyCookingRecord>();
			cookingRecord?.UpdateCookingRecord(chef, chosen);
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
		private static void SortIngredients(List<Thing> availableThings, IntVec3 rootCell, Pawn chef, Bill bill)
		{
			var cookingRecord = (bill?.billStack?.billGiver as Thing)?.Map?.GetComponent<VarietyCookingRecord>();
			List<string> curVarieties = (chef != null) ? cookingRecord?.CheckRecentRecipes(chef) : null;

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
