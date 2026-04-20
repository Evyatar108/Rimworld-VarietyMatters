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
            float tierBaseline;
            if (tier == null)                               tierBaseline = 0f;
            else if (tier == ExpectationDefOf.ExtremelyLow) tierBaseline = 0.00f;
            else if (tier == ExpectationDefOf.VeryLow)      tierBaseline = 0.05f;
            else if (tier == ExpectationDefOf.Low)          tierBaseline = 0.15f;
            else if (tier == ExpectationDefOf.Moderate)     tierBaseline = 0.30f;
            else if (tier == ExpectationDefOf.High)         tierBaseline = 0.45f;
            else if (tier == DefOf_VarietyMatters.SkyHigh)  tierBaseline = 0.55f;
            // Noble + any unrecognized DLC-added higher tier (default arm).
            else                                            tierBaseline = 0.65f;

            // Slave / prisoner status suppresses palate refinement alongside their variety count,
            // reusing the existing Mod Settings percentages (default slave 65%, prisoner 50%).
            // Noble slave → 0.65 × 0.65 = 0.42; noble prisoner → 0.65 × 0.50 = 0.33. Tribal slave
            // or prisoner stays at 0 (baseline was zero anyway).
            if (pawn.IsSlave)
            {
                tierBaseline *= Mathf.Max(0f, ModSettings_VarietyMatters.slaveExpectedVarietyPercentage / 100f);
            }
            if (pawn.IsPrisoner)
            {
                tierBaseline *= Mathf.Max(0f, ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage / 100f);
            }
            return tierBaseline;
        }

        // Warmup grace: fresh colonies / freshly-added pawns have an empty-ish queue, and the
        // baseline curve would starve them of mood until they accumulate enough meals. Short-
        // circuit to the raw meanScore formula until this many meals are in memory.
        private const int WarmupMealCount = 3;

        // US-026 tuning: soft floor — even meals below the tier baseline contribute a small
        // fraction of their raw ingredient-score to the mood pool. Breaks the "noble stuck at 0%"
        // lock in vanilla-pantry colonies while preserving the challenge at higher scores (the
        // baseline curve beats the floor once (score - b)/(1 - b) > floor × score, around score
        // 0.68 for noble b=0.65). Dial up if vanilla nobles still feel stuck; down if challenge
        // is erased.
        private const float SoftFloorCoeff = 0.25f;

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
                    float hardCurved = ingScore > baseline
                        ? (ingScore - baseline) / inverseBaseline
                        : 0f;
                    // Soft floor ensures mediocre meals register *something* for high-tier pawns —
                    // prevents the noble-stuck-at-0% trap in vanilla colonies. Takes whichever is
                    // higher of the hard baseline curve or SoftFloorCoeff × raw score.
                    float softFloor = SoftFloorCoeff * ingScore;
                    float curved = Mathf.Max(hardCurved, softFloor);

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
