namespace VarietyMatters.New
{
    using RimWorld;
    using System;
    using Verse;

    public static class VarietyProvider
    {
        public static bool DoesFoodHasVarietyValueForPawn(Pawn pawn, EatenFoodSource foodSource, out NoVarietyReason noVarietyReason)
        {
            if (pawn == null)
            {
                throw new ArgumentNullException(nameof(pawn));
            }

            if (foodSource == null)
            {
                throw new ArgumentNullException(nameof(foodSource));
            }

            if (foodSource.IsForgotton)
            {
                noVarietyReason = NoVarietyReason.NA;
                return true;
            }

            if (foodSource.IsRotten)
            {
                noVarietyReason = NoVarietyReason.Rotten;
                return false;
            }

            if (pawn.Ideo != null)
            {
                if (foodSource.Thing != null && FoodUtility.IsVeneratedAnimalMeatOrCorpseOrHasIngredients(foodSource.Thing, pawn))
                {
                    noVarietyReason = NoVarietyReason.IsOrHasVenetratedAnimalMeat;
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

                if (foodSource.IsFungus && !pawn.Ideo.HasPrecept(DefOf_VarietyMatters.FungusEating_Preferred))
                {
                    noVarietyReason = NoVarietyReason.Fungus;
                    return false;
                }
            }

            if (foodSource.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient
                && pawn.story?.traits?.HasTrait(TraitDefOf.Cannibal) != true
                && (pawn.Ideo == null
                    || (!pawn.Ideo.HasPrecept(PreceptDefOf.Cannibalism_Preferred)
                        && !pawn.Ideo.HasPrecept(PreceptDefOf.Cannibalism_RequiredRavenous)
                        && !pawn.Ideo.HasPrecept(PreceptDefOf.Cannibalism_RequiredStrong)
                        && !pawn.Ideo.HasPrecept(DefOf_VarietyMatters.Cannibalism_Acceptable))))
            {
                noVarietyReason = NoVarietyReason.HumanLikeMeat;
                return false;
            }

            switch (foodSource.FoodPreferability)
            {
                case FoodPreferability.Undefined:
                case FoodPreferability.NeverForNutrition:
                case FoodPreferability.DesperateOnly:
                case FoodPreferability.DesperateOnlyForHumanlikes:
                case FoodPreferability.RawBad:
                    return IsBadFoodAcceptableByPawn(pawn, foodSource, out noVarietyReason);
                case FoodPreferability.MealTerrible:
                case FoodPreferability.MealAwful:
                    noVarietyReason = NoVarietyReason.DisgustingMeal;
                    return false;
                case FoodPreferability.RawTasty:
                case FoodPreferability.MealSimple:
                case FoodPreferability.MealLavish:
                case FoodPreferability.MealFine:
                    return IsMealAcceptableByPawn(foodSource, pawn, out noVarietyReason);
                default:
                    throw new Exception($"Unsupported food preferability: {foodSource.FoodPreferability}");
            }
        }

        private static bool IsBadFoodAcceptableByPawn(Pawn pawn, EatenFoodSource foodSource, out NoVarietyReason noVarietyReason)
        {
            if (IsChemicalFood(foodSource.ThingDef))
            {
                return IsChemicalFoodAcceptableByPawn(pawn, foodSource, out noVarietyReason);
            }

            if (foodSource.MeatSourceCategory == MeatSourceCategory.Insect)
            {
                noVarietyReason = NoVarietyReason.InsectMeat;
                return false;
            }

            noVarietyReason = NoVarietyReason.RawOrRawlikeFood;
            return false;
        }

        private static bool IsChemicalFood(ThingDef ingredientOrMealDef)
        {
            return ingredientOrMealDef?.ingestible?.joyKind?.defName == "Chemical";
        }

        private static bool IsChemicalFoodAcceptableByPawn(Pawn pawn, EatenFoodSource foodSource, out NoVarietyReason noVarietyReason)
        {
            if (pawn.IsTeetotaler())
            {
                noVarietyReason = NoVarietyReason.HasChemicals;
                return false;
            }

            noVarietyReason = NoVarietyReason.NA;
            return true;
        }

        private static bool IsMealAcceptableByPawn(EatenFoodSource foodSource, Pawn pawn, out NoVarietyReason noVarietyReason)
        {
            foreach (ThingDef ingredientDef in foodSource.IngredientsDefs)
            {
                if (IsChemicalFood(ingredientDef))
                {
                    return IsChemicalFoodAcceptableByPawn(pawn, foodSource, out noVarietyReason);
                }

                if (ingredientDef.IsFungus && !pawn.Ideo.HasPrecept(DefOf_VarietyMatters.FungusEating_Preferred))
                {
                    noVarietyReason = NoVarietyReason.Fungus;
                    return false;
                }

                if (FoodUtility.GetMeatSourceCategory(ingredientDef) == MeatSourceCategory.Insect
                    && foodSource.ThingDef.ingestible.joyKind == null)
                {
                    noVarietyReason = NoVarietyReason.InsectMeat;
                    return false;
                }
            }

            noVarietyReason = NoVarietyReason.NA;
            return true;
        }
    }
}
