namespace VarietyMatters
{
    using System.Collections.Generic;
    using System.Linq;
    using VarietyMatters.New;
    using Verse;

    // Token: 0x0200000A RID: 10
    internal class ModSettings_VarietyMatters : ModSettings
	{
		// Token: 0x0600000E RID: 14 RVA: 0x00002444 File Offset: 0x00000644
		public static void GenerateRaces()
		{
			bool flag = ModSettings_VarietyMatters.raceVariety == null;
			if (flag)
			{
				ModSettings_VarietyMatters.raceVariety = new Dictionary<string, bool>();
			}
			foreach (ThingDef thingDef in from def in DefDatabase<ThingDef>.AllDefsListForReading.Where(delegate(ThingDef def)
			{
				RaceProperties race = def.race;
				return race != null && race.intelligence == Intelligence.Humanlike && !ModSettings_VarietyMatters.raceVariety.Keys.Contains(def.label);
			})
			orderby def.label
			select def)
			{
				ModSettings_VarietyMatters.raceVariety.Add(thingDef.label, true);
			}
			ModSettings_VarietyMatters.curRaces = ModSettings_VarietyMatters.raceVariety.Keys.ToList<string>();
			ModSettings_VarietyMatters.curRaces.Sort();
		}

		// Token: 0x0600000F RID: 15 RVA: 0x00002520 File Offset: 0x00000720
		public override void ExposeData()
		{
			Scribe_Values.Look<FoodTrackingType>(ref ModSettings_VarietyMatters.foodTrackingType, "foodTrackingType", FoodTrackingType.ByMealAndIngredients, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.halveVarietyMoodImpact, "halveVarietyMoodImpact", false, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.stackByIngredients, "stackByIngredients", false, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.moreVarietyMemory, "moreVarietyMemory", false, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.extremelyLowVariety, "extremelyLowVariety", 2, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.veryLowVariety, "veryLowVariety", 4, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.lowVariety, "lowVariety", 6, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.moderateVariety, "moderateVariety", 8, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.highVariety, "highVariety", 10, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.skyHighVariety, "skyHighVariety", 12, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.nobleVariety, "nobleVariety", 15, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.numIngredients, "numIngredients", 3, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.slavesHaveVarietyNeed, "slavesHaveVarietyNeed", true, false);
            Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.slaveExpectedVarietyPercentage, "slaveExpectedVarietyPercentage", 65, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.prisonersHaveVarietyNeed, "prisonersHaveVarietyNeed", true, false);
            Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage, "prisonerExpectedVarietyPercentage", 50, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.mealUpdateDisplayed, "mealupdateDisplayed", false, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.preferVariety, "preferVariety", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.preferSpoiling, "preferSpoiling", true, false);
			Scribe_Collections.Look<string, bool>(ref ModSettings_VarietyMatters.raceVariety, "raceVariety", (LookMode)1, (LookMode)1, ref ModSettings_VarietyMatters.raceKeys, ref ModSettings_VarietyMatters.raceValues, true);

            base.ExposeData();
		}

		// Token: 0x0400000D RID: 13
		public static FoodTrackingType foodTrackingType = FoodTrackingType.ByMealAndIngredients;

		public static bool halveVarietyMoodImpact = false;

		// Token: 0x04000010 RID: 16
		public static bool stackByIngredients = false;

		public static bool moreVarietyMemory = false;

		public static float memoryMultiplier => moreVarietyMemory ? 3f : 2.25f;

		// Token: 0x04000011 RID: 17
		public static int numIngredients = 3;

		// Token: 0x04000015 RID: 21
		public static int extremelyLowVariety = 2;

		// Token: 0x04000016 RID: 22
		public static int veryLowVariety = 4;

		// Token: 0x04000017 RID: 23
		public static int lowVariety = 6;

		// Token: 0x04000018 RID: 24
		public static int moderateVariety = 8;

		// Token: 0x04000019 RID: 25
		public static int highVariety = 10;

		// Token: 0x0400001A RID: 26
		public static int skyHighVariety = 12;

		// Token: 0x0400001B RID: 27
		public static int nobleVariety = 15;

		public static bool slavesHaveVarietyNeed = true;
		public static int slaveExpectedVarietyPercentage = 65;

        public static bool prisonersHaveVarietyNeed = true;
        public static int prisonerExpectedVarietyPercentage = 50;

        // Token: 0x0400001C RID: 28
        public static bool preferVariety = true;

		// Token: 0x0400001D RID: 29
		public static bool preferSpoiling = true;

		// Token: 0x0400001E RID: 30
		public static bool mealUpdateDisplayed = false;

		// Token: 0x0400001F RID: 31
		public static Dictionary<string, bool> raceVariety = new Dictionary<string, bool>();

		// Token: 0x04000020 RID: 32
		public static List<string> raceKeys = new List<string>();

		// Token: 0x04000021 RID: 33
		public static List<bool> raceValues = new List<bool>();

		// Token: 0x04000022 RID: 34
		public static List<string> curRaces = new List<string>();
    }
}
