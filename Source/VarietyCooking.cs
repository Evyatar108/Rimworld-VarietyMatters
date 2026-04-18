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

		// Reused scratch state. RimWorld cooking runs on the main thread only — no locking needed.
		// Assumption: no other mod recursively re-enters BestVariety_AllowMix or SortIngredients mid-execution via Harmony stacking. If that assumption breaks, nested calls would clobber each other's scratch state. Accepted risk for the GC savings; revisit if users report interactions with specific mods (e.g. RimThreaded, PickUpAndHaul).
		private static readonly Dictionary<Thing, int> _takenByThingScratch = new Dictionary<Thing, int>();
		private static readonly Dictionary<Thing, float> _sortKeysScratch = new Dictionary<Thing, float>();
		private static readonly Dictionary<string, int> _sortOccurrenceScratch = new Dictionary<string, int>();
		private static readonly Dictionary<ThingDef, float> _jitteredCapPerDefScratch = new Dictionary<ThingDef, float>();
		private static readonly Dictionary<ThingDef, float> _consumedPerDefScratch = new Dictionary<ThingDef, float>();
		private static readonly HashSet<ThingDef> _distinctDefsScratch = new HashSet<ThingDef>();
		private static readonly Comparison<Thing> _sortKeyComparison = (t1, t2) => _sortKeysScratch[t1].CompareTo(_sortKeysScratch[t2]);

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
			// Clear scratch up front regardless of early-return path. Holding stale Thing refs until the next eligible AllowMix call is harmless functionally, but releasing them now is free and keeps the invariant simple: scratch is empty after every call.
			_takenByThingScratch.Clear();
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
			// _takenByThingScratch was cleared at the top of this method; it accumulates across all slots so a Thing reserved for slot 1 isn't re-allocated for slot 2.
			bool recipeAllowsCap = bill.recipe.specialProducts == null;
			int totalSlots = bill.recipe.ingredients.Count;
			// Note: when numIngredients < totalSlots, each slot still gets a minimum budget of 1 (else the slot couldn't pick anything), so the meal may end up with up to totalSlots distinct defs — exceeding the user's numIngredients setting. Accepted because 0-budget slots are not meaningful.
			int totalBudget = Mathf.Max(1, ModSettings_VarietyMatters.numIngredients);
			int budgetRemainder = totalBudget % totalSlots;
			int budgetBase = totalBudget / totalSlots;
			for (int i = 0; i < totalSlots; i++)
			{
				IngredientCount ingredientCount = bill.recipe.ingredients[i];
				bool applyCapForSlot = recipeAllowsCap && !ingredientCount.IsFixedIngredient;
				// Distribute numIngredients across recipe slots so the total distinct defs hauled per meal stays ≤ numIngredients. First `budgetRemainder` slots get +1 to spend the remainder. Minimum 1 per slot so each slot still balances when numIngredients < totalSlots.
				int slotMaxDefs = Mathf.Max(1, budgetBase + (i < budgetRemainder ? 1 : 0));
				if (!VarietyCooking.TryFillIngredientSlotWithCap(availableThings, bill, ingredientCount, chosen, applyCapForSlot, slotMaxDefs))
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

		// RC6: NoMix bills are structurally single-ingredient — vanilla picks one ThingDef per slot,
		// so the per-slot cap (RC1-RC5) cannot apply and mixing cannot emerge at meal level.
		// We still pre-sort availableThings by variety+distance so vanilla's per-slot pick favors
		// less-recently-used ingredients across meals. Auto-toggling AllowMix on user's behalf was
		// rejected: AllowMix is a user-owned bill flag (recipe behavior, production intent).
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

		// Two-pass fill: Pass 1 caps per ThingDef to encourage mixing; Pass 2 drains residual restricted to top-K (falls back to unrestricted only if top-K can't satisfy the slot).
		private static bool TryFillIngredientSlotWithCap(List<Thing> availableThings, Bill bill, IngredientCount ingredientCount, List<ThingCount> chosen, bool applyCap, int slotMaxDefs)
		{
			// Per-slot scratch: cleared at method entry so each slot computes its own top-K/caps/consumed tallies without cross-slot bleed. _takenByThingScratch is cleared by the caller (BestVariety_AllowMix) once per bill so cross-slot Thing accounting survives.
			_distinctDefsScratch.Clear();
			_jitteredCapPerDefScratch.Clear();
			_consumedPerDefScratch.Clear();

			float slotBaseCount = ingredientCount.GetBaseCount();
			float remaining = slotBaseCount;
			bool hasDistinctDefs = false;

			if (applyCap)
			{
				// Collect distinct defs in sort order, capped at slotMaxDefs (the slot's share of numIngredients distributed across recipe slots). Top-K by sort order preserves variety/distance/spoiling priority.
				int maxDefs = Mathf.Max(1, slotMaxDefs);
				for (int j = 0; j < availableThings.Count; j++)
				{
					Thing t = availableThings[j];
					if (!ingredientCount.filter.Allows(t)) continue;
					if (!ingredientCount.IsFixedIngredient && !bill.ingredientFilter.Allows(t)) continue;
					hasDistinctDefs = true;
					if (_distinctDefsScratch.Contains(t.def)) continue;
					if (_distinctDefsScratch.Count >= maxDefs) break;
					_distinctDefsScratch.Add(t.def);
				}
				int distinctDefCount = _distinctDefsScratch.Count;

				// Skip Pass 1 for single-def slots: the formula would cap at slotBaseCount/2 and Pass 2 would fill the rest — equivalent to just running Pass 2 directly.
				if (distinctDefCount > 1)
				{
					// Jitter per-def cap with ±30% weights, then normalize so the sum equals slotBaseCount. This gives varied distributions across meals (e.g. 4+6, 5+5, 6+4) without under-allocating Pass 1 (which would leak residual to Pass 2 and concentrate it on the first-sorted def).
					float weightSum = 0f;
					foreach (ThingDef def in _distinctDefsScratch)
					{
						float w = Rand.Range(0.7f, 1.3f);
						_jitteredCapPerDefScratch[def] = w;
						weightSum += w;
					}
					float normFactor = slotBaseCount / weightSum;
					foreach (ThingDef def in _distinctDefsScratch)
					{
						_jitteredCapPerDefScratch[def] *= normFactor;
					}
					for (int j = 0; j < availableThings.Count; j++)
					{
						if (remaining <= 0.0001f) break;
						Thing t = availableThings[j];
						if (!ingredientCount.filter.Allows(t)) continue;
						if (!ingredientCount.IsFixedIngredient && !bill.ingredientFilter.Allows(t)) continue;
						// Skip defs outside the top-K (maxDefs) selected above — those fall through to Pass 2 if needed.
						if (!_jitteredCapPerDefScratch.TryGetValue(t.def, out float capForDef)) continue;

						_consumedPerDefScratch.TryGetValue(t.def, out float alreadyForDefBase);
						float defBudgetBase = capForDef - alreadyForDefBase;
						if (defBudgetBase <= 0.0001f) continue;

						float valuePerUnit = bill.recipe.IngredientValueGetter.ValuePerUnitOf(t.def);
						int needed = Mathf.CeilToInt(remaining / valuePerUnit);
						_takenByThingScratch.TryGetValue(t, out int alreadyFromThing);
						int stackLeft = t.stackCount - alreadyFromThing;
						// Ceil (not floor) so the sum of per-def item caps never undershoots the slot — prevents Pass 1 from systematically leaking residue to Pass 2 when normalized float caps have fractional item values (e.g. 4 defs × 2.5 items each would floor to 2+2+2+2=8, leaving 2 items for Pass 2 to concentrate on the first-sorted def). Over-rounding is safe: `take = min(needed, ...)` clamps by actual remaining so total never exceeds the slot.
						int defBudgetItems = Mathf.CeilToInt(defBudgetBase / valuePerUnit);
						int take = Mathf.Min(Mathf.Min(needed, stackLeft), defBudgetItems);
						if (take <= 0) continue;

						ThingCountUtility.AddToList(chosen, t, take);
						_takenByThingScratch[t] = alreadyFromThing + take;
						_consumedPerDefScratch[t.def] = alreadyForDefBase + (float)take * valuePerUnit;
						remaining -= (float)take * valuePerUnit;
					}
				}
			}

			if (remaining <= 0.0001f) return true;

			// Pass 2a: restricted to the top-K defs from Pass 1 so the numIngredients cap survives stack-limited scenarios. Only falls back to unrestricted (Pass 2b) if the top-K genuinely cannot fill the slot (all top-K stacks exhausted).
			for (int j = 0; j < availableThings.Count; j++)
			{
				if (remaining <= 0.0001f) break;
				Thing t = availableThings[j];
				if (!ingredientCount.filter.Allows(t)) continue;
				if (!ingredientCount.IsFixedIngredient && !bill.ingredientFilter.Allows(t)) continue;
				if (hasDistinctDefs && !_distinctDefsScratch.Contains(t.def)) continue;

				_takenByThingScratch.TryGetValue(t, out int alreadyFromThing2a);
				int stackLeft = t.stackCount - alreadyFromThing2a;
				if (stackLeft <= 0) continue;

				float valuePerUnit = bill.recipe.IngredientValueGetter.ValuePerUnitOf(t.def);
				int needed = Mathf.CeilToInt(remaining / valuePerUnit);
				int take = Mathf.Min(needed, stackLeft);
				if (take <= 0) continue;

				ThingCountUtility.AddToList(chosen, t, take);
				_takenByThingScratch[t] = alreadyFromThing2a + take;
				remaining -= (float)take * valuePerUnit;
			}

			if (remaining <= 0.0001f) return true;

			// Pass 2b: relax the numIngredients cap only if strictly necessary to fill the slot (rare — top-K entirely exhausted). Without this, a stack-limited scenario would fail the bill and the cook would skip the recipe.
			if (hasDistinctDefs)
			{
				for (int j = 0; j < availableThings.Count; j++)
				{
					if (remaining <= 0.0001f) break;
					Thing t = availableThings[j];
					if (!ingredientCount.filter.Allows(t)) continue;
					if (!ingredientCount.IsFixedIngredient && !bill.ingredientFilter.Allows(t)) continue;
					if (_distinctDefsScratch.Contains(t.def)) continue;

					_takenByThingScratch.TryGetValue(t, out int alreadyFromThing2b);
					int stackLeft = t.stackCount - alreadyFromThing2b;
					if (stackLeft <= 0) continue;

					float valuePerUnit = bill.recipe.IngredientValueGetter.ValuePerUnitOf(t.def);
					int needed = Mathf.CeilToInt(remaining / valuePerUnit);
					int take = Mathf.Min(needed, stackLeft);
					if (take <= 0) continue;

					ThingCountUtility.AddToList(chosen, t, take);
					_takenByThingScratch[t] = alreadyFromThing2b + take;
					remaining -= (float)take * valuePerUnit;
				}
			}

			return remaining <= 0.0001f;
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00004438 File Offset: 0x00002638
		private static void SortIngredients(List<Thing> availableThings, IntVec3 rootCell, Pawn chef, Bill bill)
		{
			var cookingRecord = (bill?.billStack?.billGiver as Thing)?.Map?.GetComponent<VarietyCookingRecord>();
			(List<string> list, int startIndex, int count) recent = default;
			if (cookingRecord != null) recent = cookingRecord.CheckRecentRecipes(chef);

			_sortOccurrenceScratch.Clear();
			bool hasVarietyPenalty = recent.list != null && recent.count > 0;
			if (hasVarietyPenalty)
			{
				int recentEnd = recent.startIndex + recent.count;
				for (int i = recent.startIndex; i < recentEnd; i++)
				{
					string lbl = recent.list[i];
					_sortOccurrenceScratch.TryGetValue(lbl, out int c);
					_sortOccurrenceScratch[lbl] = c + 1;
				}
			}

			bool preferSpoiling = ModSettings_VarietyMatters.preferSpoiling;
			_sortKeysScratch.Clear();
			for (int i = 0; i < availableThings.Count; i++)
			{
				Thing t = availableThings[i];
				float key = (t.Position - rootCell).LengthHorizontal;
				if (hasVarietyPenalty && _sortOccurrenceScratch.TryGetValue(t.def.label, out int count))
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
				_sortKeysScratch[t] = key;
			}

			availableThings.Sort(_sortKeyComparison);
		}
	}
}
