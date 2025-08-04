namespace VarietyMatters.New.Performance
{
    using System.Linq;

    /// <summary>
    /// Consolidates all food string computations into a single lazy-loaded structure
    /// to reduce FastLazy object proliferation
    /// </summary>
    public class ConsolidatedFoodStrings
    {
        private readonly FastLazy<FoodStringSet> allStrings;

        public ConsolidatedFoodStrings(EatenFoodSource source)
        {
            allStrings = new FastLazy<FoodStringSet>(() => GenerateAllStrings(source));
        }

        public string MealAsString => allStrings.Value.MealString;
        public string IngredientsAsString => allStrings.Value.IngredientsString;
        public string MealTypeAsString => allStrings.Value.MealTypeString;
        public string MealNameAndTypeAsString => allStrings.Value.MealNameAndTypeString;
        public string MealAndIngredientsAsString => allStrings.Value.MealAndIngredientsString;
        public string MealTypeAndIngredientsAsString => allStrings.Value.MealTypeAndIngredientsString;
        public string MealNameAndTypeAndIngredientsAsString => allStrings.Value.MealNameAndTypeAndIngredientsString;
        public string MealType => allStrings.Value.MealType;
        public string NormalizedThingName => allStrings.Value.NormalizedThingName;
        public bool HasMealType => allStrings.Value.HasMealType;

        private static FoodStringSet GenerateAllStrings(EatenFoodSource source)
        {
            string rottenPrefix = source.IsRotten ? "Rotten " : string.Empty;
            string ingredientsString = source.IngredientsDefs.Count > 0 
                ? string.Join(", ", source.IngredientsDefs.Select(x => x.label)) 
                : "Unknown";

            string mealType = GetMealType(source);
            string normalizedThingName = MealQualifiers.RemoveMealCountQualifiersFromMealLabel(source.ThingLabel);
            bool hasMealType = !source.IsForgotten && normalizedThingName != mealType;

            var strings = new FoodStringSet();
            // Pre-compute all string variations using StringBuilder pooling
            StringBuilderPool.Build(sb =>
            {
                // Basic strings
                strings.MealType = mealType;
                strings.NormalizedThingName = normalizedThingName;
                strings.HasMealType = hasMealType;

                // Meal string
                sb.Clear();
                sb.Append(rottenPrefix);
                sb.Append(normalizedThingName);
                strings.MealString = sb.ToString();

                // Meal type string
                sb.Clear();
                sb.Append(rottenPrefix);
                sb.Append(mealType);
                strings.MealTypeString = sb.ToString();

                // Ingredients string
                sb.Clear();
                sb.Append(rottenPrefix);
                sb.Append("meal, ingredients: ");
                sb.Append(ingredientsString);
                strings.IngredientsString = sb.ToString();

                // Meal name and type string
                sb.Clear();
                sb.Append(rottenPrefix);
                sb.Append(normalizedThingName);
                sb.Append(" (type: ");
                sb.Append(strings.MealTypeString);
                sb.Append(")");
                strings.MealNameAndTypeString = sb.ToString();

                // Meal and ingredients string
                sb.Clear();
                sb.Append(strings.MealString);
                sb.Append(", ingredients: ");
                sb.Append(ingredientsString);
                strings.MealAndIngredientsString = sb.ToString();

                // Meal type and ingredients string
                sb.Clear();
                sb.Append(strings.MealTypeString);
                sb.Append(", ingredients: ");
                sb.Append(ingredientsString);
                strings.MealTypeAndIngredientsString = sb.ToString();

                // Meal name, type and ingredients string
                sb.Clear();
                sb.Append(strings.MealNameAndTypeString);
                sb.Append(", ingredients: ");
                sb.Append(ingredientsString);
                strings.MealNameAndTypeAndIngredientsString = sb.ToString();
            });

            return strings;
        }

        private static string GetMealType(EatenFoodSource source)
        {
            if (source.FoodPreferability < RimWorld.FoodPreferability.MealSimple)
            {
                // It's not a meal, return the same type as the name of the meal.
                return MealQualifiers.RemoveMealCountQualifiersFromMealLabel(source.ThingLabel);
            }

            var mealLabelWithoutQualifiers = MealQualifiers.RemoveMealQualifiersFromMealLabel(source.ThingLabel);

            if (mealLabelWithoutQualifiers == "meal")
            {
                return "regular meal";
            }

            return mealLabelWithoutQualifiers;
        }

        public struct FoodStringSet
        {
            public string MealString;
            public string IngredientsString;
            public string MealTypeString;
            public string MealNameAndTypeString;
            public string MealAndIngredientsString;
            public string MealTypeAndIngredientsString;
            public string MealNameAndTypeAndIngredientsString;
            public string MealType;
            public string NormalizedThingName;
            public bool HasMealType;
        }
    }
}