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
		// Assumption: no other mod recursively re-enters BestVariety_AllowMix, CountEligibleDefs, or SortIngredients mid-execution via Harmony stacking. If that assumption breaks, nested calls would clobber each other's scratch state. Accepted risk for the GC savings; revisit if users report interactions with specific mods (e.g. RimThreaded, PickUpAndHaul).
		private static readonly Dictionary<Thing, int> _takenByThingScratch = new Dictionary<Thing, int>();
		private static readonly Dictionary<Thing, float> _sortKeysScratch = new Dictionary<Thing, float>();
		private static readonly Dictionary<ThingDef, int> _penaltyPerDefScratch = new Dictionary<ThingDef, int>();
		private static readonly Dictionary<ThingDef, float> _jitteredCapPerDefScratch = new Dictionary<ThingDef, float>();
		private static readonly Dictionary<ThingDef, float> _consumedPerDefScratch = new Dictionary<ThingDef, float>();
		private static readonly HashSet<ThingDef> _distinctDefsScratch = new HashSet<ThingDef>();
		// E' adaptive water-fill scratch. Cleared at top of BestVariety_AllowMix Phase A; Lists are parallel-indexed to bill.recipe.ingredients.
		private static readonly List<int> _eligibleDistinctScratch = new List<int>();
		private static readonly List<int> _assignedScratch = new List<int>();
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
			// E' adaptive water-fill: pre-scan distinct eligible defs per variable slot, then distribute effectiveBudget = max(numIngredients, variableSlotCount) across slots, giving each at least 1 and water-filling remainder with reservoir-sampled random tie-break. Fixed slots (IsFixedIngredient==true) pass slotMaxDefs=0 — TryFillIngredientSlotWithCap sees applyCap=false and ignores the value. Soft cap: modded recipes with more variable slots than numIngredients never starve (budget grows to match slot count).
			// specialProducts recipes already short-circuit the prefix at the gate branch above, so every bill reaching this loop has recipe.specialProducts == null.
			int totalSlots = bill.recipe.ingredients.Count;
			int numIngredients = Mathf.Max(1, ModSettings_VarietyMatters.numIngredients);
			_eligibleDistinctScratch.Clear();
			_assignedScratch.Clear();

			// Phase A: pre-scan each slot. Must complete before any fill call — _distinctDefsScratch is shared with TryFillIngredientSlotWithCap and is clobbered on entry there.
			int variableSlotCount = 0;
			for (int i = 0; i < totalSlots; i++)
			{
				IngredientCount ic = bill.recipe.ingredients[i];
				int eligible = 0;
				int assigned = 0;
				if (!ic.IsFixedIngredient)
				{
					variableSlotCount++;
					eligible = VarietyCooking.CountEligibleDefs(availableThings, ic, bill);
					assigned = eligible > 0 ? 1 : 0;
				}
				_eligibleDistinctScratch.Add(eligible);
				_assignedScratch.Add(assigned);
			}

			int effectiveBudget = Mathf.Max(numIngredients, variableSlotCount);
			int sumAssigned = 0;
			for (int i = 0; i < _assignedScratch.Count; i++) sumAssigned += _assignedScratch[i];
			int remaining = effectiveBudget - sumAssigned;

			// Reservoir-sample tie-break (single-pass, zero-alloc): on each while-iteration, count candidates where assigned<eligible and pick uniformly via Rand.Range(0, k)==0 replacement rule.
			while (remaining > 0)
			{
				int picked = -1;
				int candidateK = 0;
				for (int i = 0; i < _assignedScratch.Count; i++)
				{
					if (_assignedScratch[i] < _eligibleDistinctScratch[i])
					{
						candidateK++;
						if (Rand.Range(0, candidateK) == 0) picked = i;
					}
				}
				if (picked < 0) break;
				_assignedScratch[picked] = _assignedScratch[picked] + 1;
				remaining--;
			}

			// Phase B: fill. slotMaxDefs==0 for (a) fixed slots (applyCap=false ignores it) and (b) variable slots whose pre-scan found eligible==0; in case (b) TryFillIngredientSlotWithCap raises 0→1 at :183 but finds nothing matching the filters, slot fails, bill fails — matches vanilla behavior for unmakeable filtered bills.
			for (int i = 0; i < totalSlots; i++)
			{
				IngredientCount ingredientCount = bill.recipe.ingredients[i];
				bool applyCapForSlot = !ingredientCount.IsFixedIngredient;
				int slotMaxDefs = _assignedScratch[i];
				if (!VarietyCooking.TryFillIngredientSlotWithCap(availableThings, bill, ingredientCount, chosen, applyCapForSlot, slotMaxDefs))
				{
					__result = false;
					return false;
				}
			}

			__result = true;
			return false;
		}

		// Intentionally empty: prior UpdateVariety_AllowMix postfix recorded speculative picks from
		// TryFindBestBillIngredientsInSet_AllowMix, which fires on both real work scans AND menu
		// probes / region-BFS probes that never actually result in a cook job. That polluted the
		// recent-meal list with phantom picks and broke the rotation formula (F-027 diagnosed via
		// F-028 trace). The real recording now happens on `Bill.Notify_IterationCompleted` — see
		// BillPatches below — which fires exactly once per produced meal with the actual
		// ingredients consumed.

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

		// Intentionally empty (see comment on UpdateVariety_AllowMix above). NoMix postfix is also
		// dropped; Bill.Notify_IterationCompleted handles both paths uniformly.

		// Pre-scan helper for E' Phase A. Returns the count of distinct ThingDefs in availableThings matching this slot's filter. Predicate MUST match the Pass-1 top-K loop in TryFillIngredientSlotWithCap — they share _distinctDefsScratch, so any leaked reference would observe Phase-B overwrites. Returns int only.
		private static int CountEligibleDefs(List<Thing> availableThings, IngredientCount ic, Bill bill)
		{
			_distinctDefsScratch.Clear();
			for (int j = 0; j < availableThings.Count; j++)
			{
				Thing t = availableThings[j];
				if (!ic.filter.Allows(t)) continue;
				if (!ic.IsFixedIngredient && !bill.ingredientFilter.Allows(t)) continue;
				_distinctDefsScratch.Add(t.def);
			}
			return _distinctDefsScratch.Count;
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
				// Collect distinct defs in sort order, capped at this slot's precomputed E' allowance (slotMaxDefs) from the Phase-A pre-scan + water-fill. Top-K by sort order preserves variety/distance/spoiling priority.
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
			if (availableThings.Count <= 1) return;

			var cookingRecord = (bill?.billStack?.billGiver as Thing)?.Map?.GetComponent<VarietyCookingRecord>();
			(List<string> list, int startIndex, int count) recent = default;
			if (cookingRecord != null) recent = cookingRecord.CheckRecentRecipes(chef);

			_penaltyPerDefScratch.Clear();
			bool hasVarietyPenalty = recent.list != null && recent.count > 0;
			if (hasVarietyPenalty)
			{
				// Build penalty once per unique ThingDef from availableThings, not once per Thing. Same def across multiple stacks gets a single scan of the recent slice, not N scans. String label comparison preserved against recent.list (which remains List<string> — no Scribe change).
				int recentEnd = recent.startIndex + recent.count;
				for (int i = 0; i < availableThings.Count; i++)
				{
					ThingDef def = availableThings[i].def;
					if (_penaltyPerDefScratch.ContainsKey(def)) continue;
					int matches = 0;
					string label = def.label;
					for (int j = recent.startIndex; j < recentEnd; j++)
					{
						if (recent.list[j] == label) matches++;
					}
					_penaltyPerDefScratch[def] = matches;
				}
			}

			bool preferSpoiling = ModSettings_VarietyMatters.preferSpoiling;

			// F-027 rotation:
			//   key = distance
			//       + immediateRepeatPenalty × I(def in last meal)   // strong — breaks close-pile distance ties
			//       + recentPenalty × max(0, recentMatches - I(def in last meal))  // soft cumulative pressure on older repeats
			//       - spoilPull × rotProgressPct                      // near-rot override (weaker than immediate repeat)
			// varietyPull knob = tiles the cook will walk further to avoid the last meal's defs. Default 10.
			float varietyPull = ModSettings_VarietyMatters.varietyPull;
			float immediateRepeatPenalty = varietyPull;               // full pull on last-meal defs
			float recentPenalty = varietyPull * 0.35f;                // cumulative older-repeat nudge
			float spoilPull = varietyPull * 0.75f;                    // rot override ceiling

			_sortKeysScratch.Clear();
			for (int i = 0; i < availableThings.Count; i++)
			{
				Thing t = availableThings[i];
				float key = (t.Position - rootCell).LengthHorizontal;
				if (hasVarietyPenalty)
				{
					_penaltyPerDefScratch.TryGetValue(t.def, out int count);
					bool inLastMeal = cookingRecord != null && cookingRecord.IsDefInLastMeal(chef, t.def.label);
					if (inLastMeal)
					{
						// Discount 1 from count (it counted the last-meal occurrence); older repeats still get cumulative pressure.
						key += immediateRepeatPenalty + recentPenalty * Math.Max(0, count - 1);
					}
					else if (count == 0)
					{
						// Fresh bonus: never used inside the recent window. Strong pull.
						key -= varietyPull;
					}
					else
					{
						// Linear cumulative penalty — older uses still bias away, but weak so distance decides among tied older defs.
						key += recentPenalty * count;
					}
				}
				if (preferSpoiling)
				{
					CompRottable compRottable = ThingCompUtility.TryGetComp<CompRottable>(t);
					if (compRottable != null)
					{
						// Near-rot pull; doesn't override a large distance gap but can outweigh the cumulative older-repeat nudge.
						key -= spoilPull * compRottable.RotProgressPct;
					}
				}
				_sortKeysScratch[t] = key;
			}

			availableThings.Sort(_sortKeyComparison);
		}
	}

	// Records cook-recipe history at the true "meal produced" event, not at search time.
	// F-029: `Bill.Notify_IterationCompleted` fires once per produced meal with the actually-consumed
	// ingredients, bypassing the speculative-probe pollution that broke F-025/F-027.
	[HarmonyLib.HarmonyPatch(typeof(RimWorld.Bill), nameof(RimWorld.Bill.Notify_IterationCompleted))]
	internal static class Patch_Bill_Notify_IterationCompleted
	{
		[HarmonyLib.HarmonyPostfix]
		private static void Postfix(RimWorld.Bill __instance, Verse.Pawn billDoer, System.Collections.Generic.List<Verse.Thing> ingredients)
		{
			if (__instance == null || billDoer == null || ingredients == null || ingredients.Count == 0) return;
			if (ModSettings_VarietyMatters.foodTrackingType == New.FoodTrackingType.ByMealNames || !ModSettings_VarietyMatters.preferVariety) return;
			if (__instance.recipe?.workSkill != RimWorld.SkillDefOf.Cooking) return;
			Verse.ThingDef producedThingDef = __instance.recipe?.ProducedThingDef;
			if (producedThingDef?.ingestible == null) return;
			if (__instance.recipe.specialProducts != null) return;

			Verse.Thing billGiver = __instance.billStack?.billGiver as Verse.Thing;
			var cookingRecord = billGiver?.Map?.GetComponent<VarietyCookingRecord>();
			if (cookingRecord == null) return;

			// Convert ingredients (List<Thing>) into the ThingCount shape UpdateCookingRecord expects.
			// Per-call allocation here is acceptable — Notify_IterationCompleted fires once per produced
			// meal, not per scan.
			var asCounts = new System.Collections.Generic.List<Verse.ThingCount>(ingredients.Count);
			for (int i = 0; i < ingredients.Count; i++)
			{
				Verse.Thing t = ingredients[i];
				if (t == null) continue;
				asCounts.Add(new Verse.ThingCount(t, t.stackCount));
			}
			cookingRecord.UpdateCookingRecord(billDoer, asCounts);
		}
	}
}
