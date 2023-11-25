namespace VarietyMatters.New
{
    public static class VarietyAdjuster
    {
        public static void AdjustVarietyLevel(DietTracker dietTracker)
        {
            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(dietTracker.Pawn);

            Need_FoodVariety varietyNeed = dietTracker.Pawn.needs.TryGetNeed<Need_FoodVariety>();

            int currentVariety = dietTracker.TotalVariety;

            double percentageOfVarietyComparedToExpected = (double)currentVariety / (double)varietyExpectation;

            float varietyNeedValue = (float)(percentageOfVarietyComparedToExpected / 2);

            varietyNeed.CurLevel = varietyNeedValue;
        }
    }
}
