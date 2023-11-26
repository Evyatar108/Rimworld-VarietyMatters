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

            int currentVariety = dietTracker.TotalVariety;

            double percentageOfVarietyComparedToExpected = (double)currentVariety / (double)varietyExpectation;

            float varietyNeedValue = (float)(percentageOfVarietyComparedToExpected / 2);

            varietyNeed.CurLevel = varietyNeedValue;
        }
    }
}
