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
			if (ModSettings_VarietyMatters.raceVariety == null)
			{
				ModSettings_VarietyMatters.raceVariety = new Dictionary<string, RaceSetting>();
            }

			var oldRaceVariety = ModSettings_VarietyMatters.raceVariety;

            ModSettings_VarietyMatters.raceVariety = new Dictionary<string, RaceSetting>();

            var humanlikeDefs = DefDatabase<ThingDef>
				.AllDefsListForReading
				.Where(def => def.race != null && def.race.intelligence == Intelligence.Humanlike);

			foreach (ThingDef thingDef in humanlikeDefs)
            {
                string key = GetRaceKey(thingDef);

				RaceSetting raceSetting;
				if (!oldRaceVariety.TryGetValue(key, out raceSetting))
				{
					raceSetting = new RaceSetting { name = thingDef.label ?? "null", modName = thingDef.modContentPack?.Name ?? "null", isVarietyEnabled = true };
				}

				ModSettings_VarietyMatters.raceVariety.Add(key, raceSetting);
			}

			ModSettings_VarietyMatters.raceVarietyVersion++;
        }

		public static string GetRaceKey(ThingDef thingDef)
		{
			return (thingDef.modContentPack?.Name ?? "null") + "-" + thingDef.defName;
        }

		// Token: 0x0600000F RID: 15 RVA: 0x00002520 File Offset: 0x00000720
		public override void ExposeData()
		{
			Scribe_Values.Look<FoodTrackingType>(ref ModSettings_VarietyMatters.foodTrackingType, "foodTrackingType", FoodTrackingType.ByIngredientOverlap, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.nonControlledPawnsHaveVarietyNeed, "nonControlledPawnsHaveVarietyNeed", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.foodDrugsAreOnlyInMemoryOnce, "foodDrugsAreOnlyInMemoryOnce", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.clusterSimilarMealsTogether, "clusterSimilarMealsTogether", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.halveVarietyMoodImpact, "halveVarietyMoodImpact", false, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.stackByIngredients, "stackByIngredients", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.moreVarietyMemory, "moreVarietyMemory", false, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.extremelyLowVariety, "extremelyLowVariety", 4, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.veryLowVariety, "veryLowVariety", 5, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.lowVariety, "lowVariety", 6, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.moderateVariety, "moderateVariety", 8, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.highVariety, "highVariety", 10, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.skyHighVariety, "skyHighVariety", 12, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.nobleVariety, "nobleVariety", 15, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.lowExpectationFloor, "lowExpectationFloor", 3, false);
			// US-029 direct-value tier settings. Loaded with default values for saves written by
			// older versions (no migration from the legacy `extremelyLowVariety`..`nobleVariety`
			// integers) — players upgrading get the new defaults and re-tune from the preset strip.
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.extremelyLowMemory, "extremelyLowMemoryV2", 9, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.veryLowMemory, "veryLowMemoryV2", 11, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.lowMemory, "lowMemoryV2", 14, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.moderateMemory, "moderateMemoryV2", 18, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.highMemory, "highMemoryV2", 23, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.skyHighMemory, "skyHighMemoryV2", 27, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.nobleMemory, "nobleMemoryV2", 34, false);
			Scribe_Values.Look<float>(ref ModSettings_VarietyMatters.extremelyLowPalate, "extremelyLowPalateV2", 0.04f, false);
			Scribe_Values.Look<float>(ref ModSettings_VarietyMatters.veryLowPalate, "veryLowPalateV2", 0.08f, false);
			Scribe_Values.Look<float>(ref ModSettings_VarietyMatters.lowPalate, "lowPalateV2", 0.12f, false);
			Scribe_Values.Look<float>(ref ModSettings_VarietyMatters.moderatePalate, "moderatePalateV2", 0.20f, false);
			Scribe_Values.Look<float>(ref ModSettings_VarietyMatters.highPalate, "highPalateV2", 0.28f, false);
			Scribe_Values.Look<float>(ref ModSettings_VarietyMatters.skyHighPalate, "skyHighPalateV2", 0.36f, false);
			Scribe_Values.Look<float>(ref ModSettings_VarietyMatters.noblePalate, "noblePalateV2", 0.48f, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.numIngredients, "numIngredients", 3, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.cookingMemoryDepth, "cookingMemoryDepth", 10, false);
			Scribe_Values.Look<float>(ref ModSettings_VarietyMatters.varietyPull, "varietyPull", 10f, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.slavesHaveVarietyNeed, "slavesHaveVarietyNeed", true, false);
            Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.slaveExpectedVarietyPercentage, "slaveExpectedVarietyPercentage", 65, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.prisonersHaveVarietyNeed, "prisonersHaveVarietyNeed", true, false);
            Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage, "prisonerExpectedVarietyPercentage", 50, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.preferVariety, "preferVariety", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.preferSpoiling, "preferSpoiling", true, false);
			Scribe_Collections.Look<string, RaceSetting>(ref ModSettings_VarietyMatters.raceVariety, "raceVarietyV2", LookMode.Value, LookMode.Deep, ref ModSettings_VarietyMatters.raceKeys, ref ModSettings_VarietyMatters.raceValues, true);

            base.ExposeData();

			ModSettings_VarietyMatters.raceVarietyVersion++;

			// Post-load: tracker.varietyScore may be stale (old save wrote 1f default, or legacy
			// mode was active when saved). Rebuild totals from stored IngredientsDefs/AteAtTick.
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				VarietyRecord.ReCountAll();
			}
		}

		public static FoodTrackingType foodTrackingType = FoodTrackingType.ByIngredientOverlap;

		public static bool clusterSimilarMealsTogether = true;

		public static bool halveVarietyMoodImpact = false;

		// Token: 0x04000010 RID: 16
		public static bool stackByIngredients = true;

		public static bool nonControlledPawnsHaveVarietyNeed = true;

		public static bool foodDrugsAreOnlyInMemoryOnce = true;

		// DEPRECATED (US-029): the moreVarietyMemory toggle no longer affects gameplay. Kept
		// Scribed per CLAUDE.md §8 so old saves still deserialize cleanly. Players now set
		// per-tier memory directly via the new *Memory fields.
		public static bool moreVarietyMemory = false;

		// DEPRECATED: retained only for backward Scribe compatibility with pre-US-029 saves.
		// Gameplay reads per-tier *Memory fields directly now.
		public static float memoryMultiplier => 2.25f;

		// US-029 direct-value tier fields. Player sets queue size (meals remembered) per tier
		// directly — no hidden multiplier. Defaults preserve the prior `tier × 2.25` values so
		// gameplay baselines stay intact for anyone tuning from the preset strip.
		public static int extremelyLowMemory = 9;
		public static int veryLowMemory = 11;
		public static int lowMemory = 14;
		public static int moderateMemory = 18;
		public static int highMemory = 23;
		public static int skyHighMemory = 27;
		public static int nobleMemory = 34;

		// US-029 per-tier palate threshold. 0.0 → any variety counts. 0.50 → only genuinely fresh
		// meals matter. Defaults match the previous clamp((tier − 3)/25, 0, 0.50) formula at
		// default tier values. Exposed as a separate slider so players can tune palate pickiness
		// independently from memory depth.
		public static float extremelyLowPalate = 0.04f;
		public static float veryLowPalate = 0.08f;
		public static float lowPalate = 0.12f;
		public static float moderatePalate = 0.20f;
		public static float highPalate = 0.28f;
		public static float skyHighPalate = 0.36f;
		public static float noblePalate = 0.48f;

		// Token: 0x04000011 RID: 17
		public static int numIngredients = 3;

		public static int cookingMemoryDepth = 10;

		// F-025 rotation-formula knob. Default 10 tiles = cook walks up to 10 extra tiles for an unused ingredient.
		public static float varietyPull = 10f;

		// Token: 0x04000015 RID: 21
		public static int extremelyLowVariety = 4;

		// Token: 0x04000016 RID: 22
		public static int veryLowVariety = 5;

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

		// Minimum variety expectation floor. GetVarietyExpectation clamps its
		// return via Math.Max(raw, lowExpectationFloor) so tribal/Naked-Brutality starts
		// don't crater to -7 mood on day 1.
		public static int lowExpectationFloor = 3;

		public static bool slavesHaveVarietyNeed = true;
		public static int slaveExpectedVarietyPercentage = 65;

        public static bool prisonersHaveVarietyNeed = true;
        public static int prisonerExpectedVarietyPercentage = 50;

        // Token: 0x0400001C RID: 28
        public static bool preferVariety = true;

		// Token: 0x0400001D RID: 29
		public static bool preferSpoiling = true;

		// Token: 0x0400001F RID: 31
		public static Dictionary<string, RaceSetting> raceVariety = new Dictionary<string, RaceSetting>();

		// Token: 0x04000020 RID: 32
		public static List<string> raceKeys = new List<string>();

		// Token: 0x04000021 RID: 33
		public static List<RaceSetting> raceValues = new List<RaceSetting>();

		public static int raceVarietyVersion = 0;
    }
}
