namespace VarietyMatters.New
{
    using System;
    using RimWorld;
    using UnityEngine;
    using Verse;

    public static class VarietyAdjuster
    {
        // Per-meal "palate threshold" keyed on the vanilla ExpectationDef tier (not on the int
        // expectation count, which is modified by slave / prisoner / gourmand status). A meal's
        // ingredient-overlap score must exceed this threshold to register; otherwise the meal
        // contributes zero to that pawn's variety pool. Extreme Low → 0 (any variety counts).
        // Noble → 0.65 (only genuinely-fresh meals matter). Capped below 1.0 so every tier can
        // still reach full mood on a perfect (all-new-ingredients) meal.
        private static float BaselineForTier(Pawn pawn)
        {
            if (pawn == null) return 0f;
            ExpectationDef tier = ExpectationsUtility.CurrentExpectationFor(pawn);
            if (tier == null) return 0f;

            if (tier == ExpectationDefOf.ExtremelyLow) return 0.00f;
            if (tier == ExpectationDefOf.VeryLow)      return 0.05f;
            if (tier == ExpectationDefOf.Low)          return 0.15f;
            if (tier == ExpectationDefOf.Moderate)     return 0.30f;
            if (tier == ExpectationDefOf.High)         return 0.45f;
            if (tier == DefOf_VarietyMatters.SkyHigh)  return 0.55f;
            // Noble + any unrecognized DLC-added higher tier (default arm).
            return 0.65f;
        }

        // Warmup grace: fresh colonies / freshly-added pawns have an empty-ish queue, and the
        // baseline curve would starve them of mood until they accumulate enough meals. Short-
        // circuit to the raw meanScore formula until this many meals are in memory.
        private const int WarmupMealCount = 3;

        public static void AdjustVarietyLevel(DietTracker dietTracker)
        {
            if (dietTracker == null)
            {
                throw new ArgumentNullException(nameof(dietTracker));
            }

            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(dietTracker.Pawn);

            Need_FoodVariety varietyNeed = dietTracker.Pawn.needs.TryGetNeed<Need_FoodVariety>();

            double percentageOfVarietyComparedToExpected;

            int mealsInMemory = dietTracker.MealsInMemory;
            if (mealsInMemory < WarmupMealCount)
            {
                // Pre-warmup: use raw meanScore without baseline so fresh pawns don't sit at 0%
                // mood for the first few meals. Bonus is already baked into stored score in this
                // path because legacy behavior is fine for warmup.
                double meanScoreWarmup = mealsInMemory > 0
                    ? (double)dietTracker.TotalVarietyScore / mealsInMemory
                    : 0.0;
                percentageOfVarietyComparedToExpected = meanScoreWarmup * ModSettings_VarietyMatters.memoryMultiplier;
            }
            else
            {
                // I″: per-meal baseline, then post-curve additive bonus for meal-def novelty.
                // `varietyScore` on each EatenFoodSource is ingredient-only (US-026 semantic);
                // recompute the US-025 meal-def bonus via IsMealDefFreshInWindow at read time.
                float baseline = BaselineForTier(dietTracker.Pawn);
                float inverseBaseline = Mathf.Max(0.01f, 1f - baseline);

                double sumEffective = 0.0;
                int counted = 0;
                foreach (var foodByPawn in dietTracker.EatenFoodSourcesByOrder)
                {
                    var src = foodByPawn?.Source;
                    if (src == null || src.IsForgotten) continue;

                    float ingScore = src.VarietyScore;   // ingredient-only post US-026
                    float curved = ingScore > baseline
                        ? (ingScore - baseline) / inverseBaseline
                        : 0f;

                    float bonus = 0f;
                    if (dietTracker.IsMealDefFreshInWindow(src))
                    {
                        int complexity = (src.IngredientsDefs != null && src.IngredientsDefs.Count > 0)
                            ? src.IngredientsDefs.Count
                            : 1;
                        bonus = Mathf.Min(
                            DietTracker.MealDefBonusCap,
                            DietTracker.MealDefBonusPerIngredient * complexity);
                    }

                    float effective = Mathf.Clamp01(curved + bonus);
                    sumEffective += effective;
                    counted++;
                }
                double meanEffective = counted > 0 ? sumEffective / counted : 0.0;
                percentageOfVarietyComparedToExpected = meanEffective * ModSettings_VarietyMatters.memoryMultiplier;
            }

            float varietyNeedValue = (float)(percentageOfVarietyComparedToExpected / 2);
            varietyNeed.CurLevel = varietyNeedValue;
        }
    }
}
