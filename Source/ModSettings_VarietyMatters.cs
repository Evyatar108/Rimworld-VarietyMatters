using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VarietyMatters
{
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
				return race != null && (int)race.intelligence == 2 && !ModSettings_VarietyMatters.raceVariety.Keys.Contains(def.label);
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
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.maxVariety, "maxVariety", false, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.ignoreIngredients, "ignoreIngredients", false, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.sickPawns, "sickPawns", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.stackByIngredients, "stackByIngredients", false, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.curNeedAdjustments, "curNeedAdjustments", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.foodModAdjustments, "foodModAdjustments", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.tempAdjustments, "tempAdjustments", true, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.extremelyLowVariety, "extremelyLowVariety", 2, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.veryLowVariety, "veryLowVariety", 4, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.lowVariety, "lowVariety", 6, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.moderateVariety, "moderateVariety", 8, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.highVariety, "highVariety", 10, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.skyHighVariety, "skyHighVariety", 12, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.nobleVariety, "nobleVariety", 15, false);
			Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.numIngredients, "numIngredients", 3, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.slavesHaveVarietyNeed, "slavesHaveVarietyNeed", false, false);
            Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.divideSlaveVarietyBy, "divideSlaveVarietyBy", 3, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.prisonersHaveVarietyNeed, "prisonersHaveVarietyNeed", false, false);
            Scribe_Values.Look<int>(ref ModSettings_VarietyMatters.dividePrisonerVarietyBy, "dividePrisonerVarietyBy", 3, false);
            Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.mealUpdateDisplayed, "mealupdateDisplayed", false, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.preferVariety, "preferVariety", true, false);
			Scribe_Values.Look<bool>(ref ModSettings_VarietyMatters.preferSpoiling, "preferSpoiling", true, false);
			Scribe_Collections.Look<string, bool>(ref ModSettings_VarietyMatters.raceVariety, "raceVariety", (LookMode)1, (LookMode)1, ref ModSettings_VarietyMatters.raceKeys, ref ModSettings_VarietyMatters.raceValues, true);

            Scribe_Values.Look(ref fixDesserts, nameof(fixDesserts), false);
            Scribe_Values.Look(ref betterGourmet, nameof(betterGourmet), false);
            Scribe_Values.Look(ref betterArchotech, nameof(betterArchotech), false);


            base.ExposeData();
		}

		// Token: 0x0400000D RID: 13
		public static bool maxVariety = false;

		// Token: 0x0400000E RID: 14
		public static bool ignoreIngredients;

		// Token: 0x0400000F RID: 15
		public static bool sickPawns = true;

		// Token: 0x04000010 RID: 16
		public static bool stackByIngredients = false;

		// Token: 0x04000011 RID: 17
		public static int numIngredients = 3;

		// Token: 0x04000012 RID: 18
		public static bool curNeedAdjustments = true;

		// Token: 0x04000013 RID: 19
		public static bool foodModAdjustments = true;

		// Token: 0x04000014 RID: 20
		public static bool tempAdjustments = true;

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

		public static bool slavesHaveVarietyNeed = false;
		public static int divideSlaveVarietyBy = 3;

        public static bool prisonersHaveVarietyNeed = false;
        public static int dividePrisonerVarietyBy = 3;

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


        public static bool fixDesserts = false;
        public static bool betterGourmet = false;
        public static bool betterArchotech = false;
    }
}
