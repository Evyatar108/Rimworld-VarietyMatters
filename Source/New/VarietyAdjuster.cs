namespace VarietyMatters.New
{
    using System;

    public static class VarietyAdjuster
    {
        public static void AdjustVarietyLevel(DietTracker dietTracker)
        {
            if (dietTracker == null)
            {
                throw new ArgumentNullException(nameof(dietTracker));
            }

            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(dietTracker.Pawn);

            Need_FoodVariety varietyNeed = dietTracker.Pawn.needs.TryGetNeed<Need_FoodVariety>();

            double percentageOfVarietyComparedToExpected;

            if (ModSettings_VarietyMatters.foodTrackingType == FoodTrackingType.ByIngredientOverlap)
            {
                percentageOfVarietyComparedToExpected = (double)dietTracker.TotalVarietyScore / (double)varietyExpectation;
            }
            else
            {
                percentageOfVarietyComparedToExpected = (double)dietTracker.TotalVariety / (double)varietyExpectation;
            }

            float varietyNeedValue = (float)(percentageOfVarietyComparedToExpected / 2);

            varietyNeed.CurLevel = varietyNeedValue;
        }
    }
}
