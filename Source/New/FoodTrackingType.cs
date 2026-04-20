namespace VarietyMatters.New
{
    // Single tracking mode. The former ByMealNames / ByIngredientsCombination /
    // ByMealNamesAndIngredientsCombination options were removed in favor of the continuous
    // per-meal overlap score (see US-023..US-025). Old saves that stored any of those names
    // will deserialize to the default (ByIngredientOverlap, ordinal 0).
    public enum FoodTrackingType
    {
        ByIngredientOverlap,
    }
}
