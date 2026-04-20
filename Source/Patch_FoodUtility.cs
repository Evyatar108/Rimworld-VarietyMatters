namespace VarietyMatters
{
    using HarmonyLib;
    using RimWorld;
    using VarietyMatters.New;
    using Verse;

    // Token: 0x0200000D RID: 13
    // HarmonyPriority.LowerThanNormal: defer to MOPF (Meals Over Preserved Foods) and other
    // postfixes that adjust FoodOptimality raw score. Our +60f variety bonus stacks cleanly
    // on top of their adjustments instead of being overwritten.
    [HarmonyPatch]
    [HarmonyPriority(Priority.LowerThanNormal)]
	public static class Patch_FoodUtility
	{
		// Token: 0x06000020 RID: 32 RVA: 0x00003298 File Offset: 0x00001498
		[HarmonyPatch(typeof(FoodUtility), "FoodOptimality")]
		[HarmonyPostfix]
		private static void Postfix_FoodOptimality(ref float __result, Pawn eater, Thing foodSource, bool takingToInventory)
		{
			if (takingToInventory)
			{
				return;
			}

			if (foodSource.def.ingestible == null)
			{
				return;
			}

			Need_FoodVariety need = eater.needs?.TryGetNeed<Need_FoodVariety>();
			if (need == null || need.Disabled)
			{
				return;
			}

			if (!VarietyRecord.TryGetVarietyRecord(eater, out DietTracker dietTracker)
				|| dietTracker.MostRecentEatenFoodSource == null)
			{
				return;
			}

			CompVariety compVariety = ThingCompUtility.TryGetComp<CompVariety>(foodSource);
			if (compVariety == null)
			{
				return;
			}

			if (FoodUtility.Starving(eater)
				|| eater.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning, true)
				|| HealthAIUtility.ShouldSeekMedicalRest(eater)
				|| FoodUtility.WillGiveNegativeThoughts(foodSource, eater))
			{
				return;
			}

			// US-026 food-search alignment: instead of the legacy binary "is this meal key new?"
			// check, score the candidate by the same per-meal effective formula used for mood.
			// A meal whose ingredients overlap the pawn's recent window scores low; a fresh-def
			// meal with novel ingredients scores ~1.0. Pawn's pick preference now tracks what
			// will actually improve their variety mood.
			float ingScore = dietTracker.EstimateIngredientScoreForCandidate(foodSource);
			bool mealDefFresh = dietTracker.IsCandidateMealDefFresh(foodSource);
			var comp = foodSource.TryGetComp<CompIngredients>();
			int complexity = VarietyAdjuster.GetRecipeSlotComplexity(foodSource.def, comp?.ingredients);
			float baseline = VarietyAdjuster.BaselineForTierPublic(eater);
			float effective = VarietyAdjuster.ComputeEffectiveScore(ingScore, baseline, mealDefFresh, complexity);

			// Original patch awarded a flat +60 for a new-key meal. Keep the same maximum so
			// late-game mod interactions behave the same; just scale by predicted effectiveness.
			__result += 60f * effective;
        }
    }
}
