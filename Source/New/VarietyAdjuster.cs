namespace VarietyMatters.New
{
    using System;
    using System.Collections.Generic;
    using RimWorld;
    using UnityEngine;
    using Verse;

    public static class VarietyAdjuster
    {
        // Cache of `ThingDef (produced meal) → recipe.ingredients.Count (slot count)`. Populated
        // lazily on first lookup, rarely grows (one entry per meal ThingDef in the game). Used
        // for the US-025 meal-def-new bonus: complexity should reflect the recipe's intrinsic
        // slot count (1 for simple meal, 2 for fine/lavish), NOT how many distinct defs the mod
        // stuffed into the cooked meal via BestVariety_AllowMix. Per user feedback: "simple meal
        // with 3 ingredients stuffed in should still score as a 1-slot recipe."
        private static readonly Dictionary<ThingDef, int> _recipeSlotCountCache = new Dictionary<ThingDef, int>();

        public static int GetRecipeSlotComplexity(ThingDef mealDef, List<ThingDef> fallbackInstanceIngredients)
        {
            if (mealDef != null)
            {
                if (_recipeSlotCountCache.TryGetValue(mealDef, out int cached))
                {
                    if (cached > 0) return cached;
                }
                else
                {
                    int slots = 0;
                    var recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
                    for (int i = 0; i < recipes.Count; i++)
                    {
                        var r = recipes[i];
                        if (r != null && r.ProducedThingDef == mealDef && r.ingredients != null)
                        {
                            slots = r.ingredients.Count;
                            break;
                        }
                    }
                    _recipeSlotCountCache[mealDef] = slots;
                    if (slots > 0) return slots;
                }
            }
            // No recipe produces this meal (raw, paste, kibble-eaten-directly, modded food with no
            // crafting entry). Fall back to the instance ingredient count, then 1.
            int fallback = fallbackInstanceIngredients != null && fallbackInstanceIngredients.Count > 0
                ? fallbackInstanceIngredients.Count
                : 1;
            return fallback;
        }

        // Per-meal "palate threshold" keyed on the vanilla ExpectationDef tier (not on the int
        // expectation count, which is modified by slave / prisoner / gourmand status). A meal's
        // ingredient-overlap score must exceed this threshold to register; otherwise the meal
        // contributes zero to that pawn's variety pool. Extreme Low → 0 (any variety counts).
        // Noble → 0.65 (only genuinely-fresh meals matter). Capped below 1.0 so every tier can
        // still reach full mood on a perfect (all-new-ingredients) meal.
        // Public variant of BaselineForTier so external callers (Patch_FoodUtility) can compute
        // predicted mood contribution for a candidate meal without duplicating the tier table.
        public static float BaselineForTierPublic(Pawn pawn) => BaselineForTier(pawn);

        // Single-meal effective-score formula shared between mood aggregation (VarietyAdjuster) and
        // candidate prediction (Patch_FoodUtility). Pure function: soft floor + baseline curve +
        // post-curve bonus, clamp01. Keep this the ONLY place the per-meal combine happens.
        public static float ComputeEffectiveScore(float ingredientScore, float baseline,
            bool mealDefFresh, int ingredientsCount)
        {
            float inverseBaseline = Mathf.Max(0.01f, 1f - baseline);
            float hardCurved = ingredientScore > baseline
                ? (ingredientScore - baseline) / inverseBaseline
                : 0f;
            float softFloor = SoftFloorCoeff * ingredientScore;
            float curved = Mathf.Max(hardCurved, softFloor);

            float bonus = 0f;
            if (mealDefFresh)
            {
                int complexity = ingredientsCount > 0 ? ingredientsCount : 1;
                bonus = Mathf.Min(
                    DietTracker.MealDefBonusCap,
                    DietTracker.MealDefBonusPerIngredient * complexity);
            }
            return Mathf.Clamp01(curved + bonus);
        }

        // Baseline derived from per-tier Mod Settings so the player's tuning of
        // extremelyLowVariety..nobleVariety drives both memory capacity AND palate threshold
        // consistently. Formula: baseline = clamp((rawSetting − 3) / 20, 0, 0.9).
        //   Defaults: ExLow=4 → 0.05, VLow=5 → 0.10, Low=6 → 0.15, Mod=8 → 0.25,
        //             High=10 → 0.35, SkyHi=12 → 0.45, Noble=15 → 0.60.
        // Reads the *raw* tier setting (no slave / prisoner / gourmand modifiers) — those apply
        // to variety count (GetVarietyExpectation) but not to palate refinement, which is purely
        // a tier-identity property. Slave/prisoner multipliers still apply to the baseline below,
        // consistent with how they already modulate expected variety count.
        private static float BaselineForTier(Pawn pawn)
        {
            if (pawn == null) return 0f;
            ExpectationDef tier = ExpectationsUtility.CurrentExpectationFor(pawn);
            if (tier == null) return 0f;
            int raw = GetRawTierVariety(tier);
            float tierBaseline = Mathf.Clamp((raw - 3) / 20f, 0f, 0.9f);

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

        private static int GetRawTierVariety(ExpectationDef tier)
        {
            if (tier == ExpectationDefOf.ExtremelyLow) return ModSettings_VarietyMatters.extremelyLowVariety;
            if (tier == ExpectationDefOf.VeryLow)      return ModSettings_VarietyMatters.veryLowVariety;
            if (tier == ExpectationDefOf.Low)          return ModSettings_VarietyMatters.lowVariety;
            if (tier == ExpectationDefOf.Moderate)     return ModSettings_VarietyMatters.moderateVariety;
            if (tier == ExpectationDefOf.High)         return ModSettings_VarietyMatters.highVariety;
            if (tier == DefOf_VarietyMatters.SkyHigh)  return ModSettings_VarietyMatters.skyHighVariety;
            // Noble + any unrecognized DLC-added higher tier (default arm).
            return ModSettings_VarietyMatters.nobleVariety;
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
        public const float SoftFloorCoeff = 0.25f;

        // Overlap-scoring window: number of most-recent queued meals that contribute to ingredient
        // overlap for a given candidate meal. Fixed per-tier (nobles and tribals both score
        // against last N=6 meals). Decouples storage depth (per-tier × memoryMultiplier, used
        // for meal-def rotation lookup + UI history) from scoring saturation. Without this cap
        // noble's 34-meal queue would make ingredient overlap saturate any pantry < 100 defs,
        // locking noble mood at 0% regardless of cooking effort.
        public const int ScoringWindow = 6;

        public static void AdjustVarietyLevel(DietTracker dietTracker)
        {
            if (dietTracker == null)
            {
                throw new ArgumentNullException(nameof(dietTracker));
            }

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
                // per-meal effective combine lives in ComputeEffectiveScore so Patch_FoodUtility's
                // candidate-estimate helper uses the exact same math.
                float baseline = BaselineForTier(dietTracker.Pawn);

                double sumEffective = 0.0;
                int counted = 0;
                foreach (var foodByPawn in dietTracker.EatenFoodSourcesByOrder)
                {
                    var src = foodByPawn?.Source;
                    if (src == null || src.IsForgotten) continue;
                    // Skip meals flagged at ingest as non-variety (raw meat, insect, humanlike,
                    // rotten, venerated, etc.). Neither their score nor their slot contributes —
                    // so eating raw meat doesn't lift mood but also doesn't drag it down.
                    if (dietTracker.HasNoVarietyReason(foodByPawn)) continue;

                    int complexity = GetRecipeSlotComplexity(src.ThingDef, src.IngredientsDefs);
                    float effective = ComputeEffectiveScore(
                        src.VarietyScore,
                        baseline,
                        dietTracker.IsMealDefFreshInWindow(src),
                        complexity);
                    sumEffective += effective;
                    counted++;
                }
                // Guard: counted==0 means every meal in memory was forgotten or NoVariety-flagged
                // (raw/kibble/etc.). Avoids NaN from 0/0 division — fall back to 0 mood, matching
                // the warmup-with-zero-meals branch. See codex review F-035.
                double meanEffective = counted > 0 ? sumEffective / counted : 0.0;
                percentageOfVarietyComparedToExpected = meanEffective * ModSettings_VarietyMatters.memoryMultiplier;
            }

            float varietyNeedValue = (float)(percentageOfVarietyComparedToExpected / 2);
            varietyNeed.CurLevel = varietyNeedValue;
        }
    }
}
