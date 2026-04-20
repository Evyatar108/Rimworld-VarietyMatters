namespace VarietyMatters
{
    using System;
    using RimWorld;
    using VarietyMatters.New;
    using Verse;

    public class VarietyExpectation
	{
		public static int GetVarietyExpectation(Pawn ingester)
		{
            if (ingester == null)
            {
                throw new ArgumentNullException(nameof(ingester));
            }

			ExpectationDef expectationDef = ExpectationsUtility.CurrentExpectationFor(ingester);
            // US-029: expectation count now derives from the pawn's per-tier memory setting so
            // the vanilla Need_FoodVariety "Expected N" display stays meaningful after the tier
            // integer was replaced by a direct queue-size knob. Keeps the legacy factor 2.25 so
            // old values (ExLow=4, Noble=15) line up with new memory defaults (9, 34).
            int memory = VarietyAdjuster.GetMemoryForTier(expectationDef);
            int expectedVariety = Math.Max(1, (int)Math.Round(memory / 2.25));

            if (ingester.IsSlave)
            {
                expectedVariety = Math.Max(1, expectedVariety * ModSettings_VarietyMatters.slaveExpectedVarietyPercentage / 100);
            }

            if (ingester.IsPrisoner)
            {
                expectedVariety = Math.Max(1, expectedVariety * ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage / 100);
            }

            TraitDef gourmand = DefOf_VarietyMatters.Gourmand;
            if (gourmand != null && ingester?.story?.traits?.HasTrait(gourmand) == true)
            {
                expectedVariety = expectedVariety * 5 / 4; // *1.25, 25%+
            }

            // Math.Max(raw, setting) soft-cap prevents tribal/Naked-Brutality day-1 mood crater (-7 variety thought).
            return Math.Max(expectedVariety, ModSettings_VarietyMatters.lowExpectationFloor);
		}

		public static float moddedMeals;

		public static float moddedIngredients;

		public static float meatTypes;
	}
}
