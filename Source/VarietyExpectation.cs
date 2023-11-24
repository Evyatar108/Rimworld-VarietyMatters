namespace VarietyMatters
{
    using System;
    using RimWorld;
    using Verse;

    public class VarietyExpectation
	{
		public static int GetVarietyExpectation(Pawn ingester)
		{
			ExpectationDef expectationDef = ExpectationsUtility.CurrentExpectationFor(ingester);
            int expectedVariety;

            switch (expectationDef)
            {
				case var _ when expectationDef == ExpectationDefOf.ExtremelyLow:
                    expectedVariety = ModSettings_VarietyMatters.extremelyLowVariety;
                    break;
                case var _ when expectationDef == ExpectationDefOf.VeryLow:
                    expectedVariety = ModSettings_VarietyMatters.veryLowVariety;
                    break;
                case var _ when expectationDef ==  ExpectationDefOf.Low:
                    expectedVariety = ModSettings_VarietyMatters.lowVariety;
                    break;
                case var _ when expectationDef ==  ExpectationDefOf.Moderate:
                    expectedVariety = ModSettings_VarietyMatters.moderateVariety;
                    break;
                case var _ when expectationDef == ExpectationDefOf.High:
                    expectedVariety = ModSettings_VarietyMatters.highVariety;
                    break;
                case var _ when expectationDef == ExpectationDefOf.SkyHigh:
                    expectedVariety = ModSettings_VarietyMatters.skyHighVariety;
                    break;
                default:
                    expectedVariety = ModSettings_VarietyMatters.nobleVariety;
                    break;
            }

            if (ingester.IsSlave)
            {
                expectedVariety = Math.Max(1, expectedVariety * ModSettings_VarietyMatters.slaveExpectedVarietyPercentage / 100);
            }

            if (ingester.IsPrisoner)
            {
                expectedVariety = Math.Max(1, expectedVariety * ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage / 100);
            }

            if (ingester.story.traits.HasTrait(TraitDef.Named("Gourmand")))
            {
                expectedVariety = expectedVariety * 5 / 4; // *1.25
            }

            return expectedVariety;
		}

		public static float moddedMeals;

		public static float moddedIngredients;

		public static float meatTypes;
	}
}
