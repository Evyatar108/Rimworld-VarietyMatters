namespace VarietyMatters.New
{
    using System;
    using UnityEngine;

    public static class VarietyAdjuster
    {
        // Baseline curve constants (US-026 I″ formulation). A pawn's variety expectation sets a
        // per-meal "palate threshold" — meals whose ingredient-overlap score is below this value
        // contribute nothing to that pawn's variety pool. Tribal (E=3) → 0 (no threshold, any
        // variety registers). Noble (E=15) → 0.6 (only 60%+ fresh meals count). Formula:
        //   baseline = clamp((expected - 3) / 20, 0, 0.9)
        // Capped at 0.9 so even legendary-tier pawns can still register variety on perfect meals.
        private const float BaselineExpectedAnchor = 3f;
        private const float BaselineExpectedDivisor = 20f;
        private const float BaselineMaxClamp = 0.9f;

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
                float baseline = Mathf.Clamp(
                    (varietyExpectation - BaselineExpectedAnchor) / BaselineExpectedDivisor,
                    0f, BaselineMaxClamp);
                float inverseBaseline = 1f - baseline;   // always > 0 because baseline <= 0.9

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
