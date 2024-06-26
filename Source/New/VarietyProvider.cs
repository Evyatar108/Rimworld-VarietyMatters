﻿namespace VarietyMatters.New
{
    using RimWorld;
    using System;
    using VarietyMatters.New.Performance;
    using Verse;

    public static class VarietyProvider
    {
        public static bool DoesFoodHasVarietyValueForPawn(Pawn pawn, EatenFoodByPawn eatenFoodByPawn, out NoVarietyReason noVarietyReason)
        {
            EatenFoodSource foodSource = eatenFoodByPawn.Source;
            ValidateInputs(pawn, foodSource);

            if (foodSource.IsForgotten)
            {
                noVarietyReason = NoVarietyReason.NA;
                return true;
            }

            if (foodSource.IsRotten)
            {
                noVarietyReason = NoVarietyReason.Rotten;
                return false;
            }

            if (pawn.Ideo != null && !IsFoodIdeologicallyAcceptable(pawn, eatenFoodByPawn, out noVarietyReason))
            {
                return false;
            }

            if (IsHumanlikeFoodUnacceptable(pawn, foodSource))
            {
                noVarietyReason = NoVarietyReason.HumanLikeMeat;
                return false;
            }

            return CheckFoodPreferability(pawn, eatenFoodByPawn, out noVarietyReason);
        }

        private static void ValidateInputs(Pawn pawn, EatenFoodSource foodSource)
        {
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            if (foodSource == null) throw new ArgumentNullException(nameof(foodSource));
        }

        private static bool IsFoodIdeologicallyAcceptable(Pawn pawn, EatenFoodByPawn eatenFood, out NoVarietyReason noVarietyReason)
        {
            var foodSource = eatenFood.Source;

            if (eatenFood.IsVeneratedAnimalMeatOrCorpseOrHasIngredients)
            {
                noVarietyReason = NoVarietyReason.IsOrHasVeneratedAnimalMeat;
                return false;
            }

            if (!foodSource.IsAcceptableToCarnivores && FoodUtility.HasMeatEatingRequiredPrecept(pawn.Ideo))
            {
                noVarietyReason = NoVarietyReason.UnacceptableByCarnivores;
                return false;
            }

            if (!foodSource.IsAcceptableToVegetarians && FoodUtility.HasVegetarianRequiredPrecept(pawn.Ideo))
            {
                noVarietyReason = NoVarietyReason.UnacceptableByVegetarians;
                return false;
            }

            if (foodSource.IsFungus && HatesFungus(pawn))
            {
                noVarietyReason = NoVarietyReason.Fungus;
                return false;
            }

            noVarietyReason = NoVarietyReason.NA;
            return true;
        }

        private static bool IsHumanlikeFoodUnacceptable(Pawn pawn, EatenFoodSource foodSource)
        {
            return foodSource.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient && !IsCannibal(pawn);
        }

        private static bool CheckFoodPreferability(Pawn pawn, EatenFoodByPawn eatenFood, out NoVarietyReason noVarietyReason)
        {
            EatenFoodSource foodSource = eatenFood.Source;
            FastLazy<bool> hasNegativeMoodImpact = new FastLazy<bool>(() => HasNegativeMoodImpact(eatenFood));
            switch (foodSource.FoodPreferability) 
            {
                case FoodPreferability.Undefined:
                    return HandleUndefinedPreferability(pawn, foodSource, hasNegativeMoodImpact, out noVarietyReason);
                case FoodPreferability.NeverForNutrition:
                    return HandleNeverForNutrition(pawn, foodSource, hasNegativeMoodImpact, out noVarietyReason);
                case FoodPreferability.DesperateOnly:
                case FoodPreferability.DesperateOnlyForHumanlikes:
                case FoodPreferability.RawBad:
                    return IsBadFoodAcceptableByPawn(pawn, foodSource, hasNegativeMoodImpact, out noVarietyReason);
                case FoodPreferability.MealTerrible:
                case FoodPreferability.MealAwful:
                    return HandleTerribleOrAwfulMeals(pawn, foodSource, hasNegativeMoodImpact, out noVarietyReason);
                case FoodPreferability.RawTasty:
                case FoodPreferability.MealSimple:
                case FoodPreferability.MealLavish:
                case FoodPreferability.MealFine:
                    return IsMealAcceptableByPawn(foodSource, pawn, hasNegativeMoodImpact, out noVarietyReason);
                default:
                    throw new Exception($"Unsupported food preferability: {foodSource.FoodPreferability}");
            };
        }

        private static bool HandleUndefinedPreferability(Pawn pawn, EatenFoodSource foodSource, FastLazy<bool> hasNegativeMoodImpact, out NoVarietyReason noVarietyReason)
        {
            if (!IsMealAcceptableByPawn(foodSource, pawn, hasNegativeMoodImpact, out noVarietyReason))
            {
                return false;
            }

            if (IsChemicalFood(foodSource.ThingDef))
            {
                return IsChemicalFoodAcceptableByPawn(pawn, hasNegativeMoodImpact, out noVarietyReason);
            }

            if (hasNegativeMoodImpact.Value)
            {
                noVarietyReason = NoVarietyReason.DisgustingMeal;
                return false;
            }

            noVarietyReason = NoVarietyReason.NA;
            return true;
        }

        private static bool HandleNeverForNutrition(Pawn pawn, EatenFoodSource foodSource, FastLazy<bool> hasNegativeMoodImpact, out NoVarietyReason noVarietyReason)
        {
            if (IsChemicalFood(foodSource.ThingDef) && pawn.IsTeetotaler())
            {
                noVarietyReason = NoVarietyReason.HasChemicals;
                return false;
            }

            if (hasNegativeMoodImpact.Value)
            {
                noVarietyReason = NoVarietyReason.DisgustingMeal;
                return false;
            }

            noVarietyReason = NoVarietyReason.NA;
            return true;
        }

        private static bool HandleTerribleOrAwfulMeals(Pawn pawn, EatenFoodSource foodSource, FastLazy<bool> hasNegativeMoodImpact, out NoVarietyReason noVarietyReason)
        {
            if (IsNutrientPasteMeal(foodSource))
            {
                return IsNotMindingNutrientMeal(foodSource, pawn, hasNegativeMoodImpact, out noVarietyReason);
            }

            noVarietyReason = NoVarietyReason.DisgustingMeal;
            return false;
        }

        private static bool IsCannibal(Pawn pawn)
        {
            return pawn.story?.traits?.HasTrait(DefOf_VarietyMatters.Cannibal) == true ||
                   (pawn.Ideo != null && pawn.Ideo.HasAnyCannibalismPrecept());
        }

        private static bool HasAnyCannibalismPrecept(this Ideo ideo)
        {
            return ideo.HasPrecept(PreceptDefOf.Cannibalism_Preferred) ||
                   ideo.HasPrecept(PreceptDefOf.Cannibalism_RequiredRavenous) ||
                   ideo.HasPrecept(PreceptDefOf.Cannibalism_RequiredStrong) ||
                   ideo.HasPrecept(DefOf_VarietyMatters.Cannibalism_Acceptable);
        }

        private static bool IsBadFoodAcceptableByPawn(Pawn pawn, EatenFoodSource foodSource, FastLazy<bool> hasNegativeMoodImpact, out NoVarietyReason noVarietyReason)
        {
            if (IsInsectMeatPawnDoesntLike(pawn, foodSource.MeatSourceCategory, foodSource.ThingDef))
            {
                noVarietyReason = NoVarietyReason.InsectMeat;
                return false;
            }

            if (IsChemicalFood(foodSource.ThingDef))
            {
                return IsChemicalFoodAcceptableByPawn(pawn, hasNegativeMoodImpact, out noVarietyReason);
            }

            if (!hasNegativeMoodImpact.Value)
            {
                noVarietyReason = NoVarietyReason.NA;
                return true;
            }

            noVarietyReason = NoVarietyReason.RawOrRawlikeFood;
            return false;
        }

        private static bool IsInsectMeatPawnDoesntLike(Pawn pawn, MeatSourceCategory meatSourceCategory, ThingDef thingDef)
        {
            return meatSourceCategory == MeatSourceCategory.Insect
                && thingDef.ingestible.joyKind == null
                && (!ModsConfig.IdeologyActive || pawn.Ideo?.HasPrecept(DefOf_VarietyMatters.InsectMeatEating_Despised_Classic) == true);
        }

        private static bool IsChemicalFood(ThingDef ingredientOrMealDef)
        {
            return ingredientOrMealDef?.ingestible?.joyKind?.defName == "Chemical";
        }

        private static bool IsNutrientPasteMeal(EatenFoodSource eatenFoodSource)
        {
            return eatenFoodSource.ThingDef == DefOf_VarietyMatters.MealNutrientPaste;
        }

        private static bool IsNotMindingNutrientMeal(EatenFoodSource foodSource, Pawn pawn, FastLazy<bool> hasNegativeMoodImpact, out NoVarietyReason noVarietyReason)
        {
            if (!ModsConfig.IdeologyActive || pawn.Ideo?.HasPrecept(DefOf_VarietyMatters.NutrientPasteEating_Disgusting) == true)
            {
                noVarietyReason = NoVarietyReason.DisgustingMeal;
                return false;
            }

            return IsMealAcceptableByPawn(foodSource, pawn, hasNegativeMoodImpact, out noVarietyReason);
        }

        private static bool IsChemicalFoodAcceptableByPawn(Pawn pawn, FastLazy<bool> hasNegativeMoodImpact, out NoVarietyReason noVarietyReason)
        {
            if (pawn.IsTeetotaler() || hasNegativeMoodImpact)
            {
                noVarietyReason = NoVarietyReason.HasChemicals;
                return false;
            }

            noVarietyReason = NoVarietyReason.NA;
            return true;
        }

        private static bool IsMealAcceptableByPawn(EatenFoodSource foodSource, Pawn pawn, FastLazy<bool> hasNegativeMoodImpact, out NoVarietyReason noVarietyReason)
        {
            foreach (ThingDef ingredientDef in foodSource.IngredientsDefs)
            {
                if (IsChemicalFood(ingredientDef))
                {
                    noVarietyReason = NoVarietyReason.HasChemicals;
                    return false;
                }

                if (ingredientDef.IsFungus && HatesFungus(pawn))
                {
                    noVarietyReason = NoVarietyReason.Fungus;
                    return false;
                }

                if (IsInsectMeatPawnDoesntLike(pawn, FoodUtility.GetMeatSourceCategory(ingredientDef), ingredientDef))
                {
                    noVarietyReason = NoVarietyReason.InsectMeat;
                    return false;
                }
            }

            if (hasNegativeMoodImpact.Value)
            {
                noVarietyReason = NoVarietyReason.DisgustingMeal;
                return false;
            }

            noVarietyReason = NoVarietyReason.NA;
            return true;
        }

        private static bool HatesFungus(Pawn pawn)
        {
            return !ModsConfig.IdeologyActive || pawn.Ideo?.HasPrecept(DefOf_VarietyMatters.FungusEating_Despised) == true;
        }

        private static bool HasNegativeMoodImpact(EatenFoodByPawn eatenFoodSource)
        {
            return eatenFoodSource.MoodFromIngesting < 0;
        }
    }
}
