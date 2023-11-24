namespace VarietyMatters
{
    using System.Collections.Generic;
    using System.Reflection;
    using HarmonyLib;
    using RimWorld;
    using UnityEngine;
    using VarietyMatters.New;
    using Verse;
    using Verse.Sound;

    // Token: 0x0200000B RID: 11
    internal class Mod_VarietyMatters : Mod
	{
		// Token: 0x06000012 RID: 18 RVA: 0x00002744 File Offset: 0x00000944
		public Mod_VarietyMatters(ModContentPack content) : base(content)
		{
			base.GetSettings<ModSettings_VarietyMatters>();
			Harmony harmony = new Harmony("rimworld.varietymatters");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
		}

		// Token: 0x06000013 RID: 19 RVA: 0x00002778 File Offset: 0x00000978
		public override void WriteSettings()
		{
			base.WriteSettings();
		}

		// Token: 0x06000014 RID: 20 RVA: 0x000027A0 File Offset: 0x000009A0
		public override void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard listing_Standard = new Listing_Standard();

            // After this line:
            Rect rectScroll = new Rect(5f, 35f, inRect.width * 0.54f, inRect.height);
            // Add these lines:
            float contentHeight1 = 700f; // This is a tentative value; adjust as needed based on the actual content of your column
            Rect viewRect1 = new Rect(0f, 0f, rectScroll.width - 16f, contentHeight1); // 16f is the width of the scrollbar
            Widgets.BeginScrollView(rectScroll, ref leftScrollPosition, viewRect1);

            Rect rect = new Rect(10f, 0f, inRect.width * 0.5f, inRect.height*1.05f);
			listing_Standard.Begin(rect);
			listing_Standard.Label("Variety Tracking Options:", -1f, null);

			var previousFoodTrackingType = ModSettings_VarietyMatters.foodTrackingType;

			bool flag = listing_Standard.RadioButton("     Track meals and ingredients: ", ModSettings_VarietyMatters.foodTrackingType == FoodTrackingType.ByMealAndIngredients, 0f, null, null);
			if (flag)
			{
				ModSettings_VarietyMatters.foodTrackingType = FoodTrackingType.ByMealAndIngredients;
			}

			bool flag2 = listing_Standard.RadioButton("     Track ingredients only: ", ModSettings_VarietyMatters.foodTrackingType == FoodTrackingType.ByIngredients, 0f, null, null);
			if (flag2)
			{
				ModSettings_VarietyMatters.foodTrackingType = FoodTrackingType.ByIngredients;
			}

			bool flag3 = listing_Standard.RadioButton("     Track meals only: ", ModSettings_VarietyMatters.foodTrackingType == FoodTrackingType.ByMeal, 0f, null, null);
			if (flag3)
			{
				ModSettings_VarietyMatters.foodTrackingType = FoodTrackingType.ByMeal;
			}

			if (previousFoodTrackingType != ModSettings_VarietyMatters.foodTrackingType)
			{
                foreach (DietTracker dietTracker in VarietyRecord.varietyRecord.Values)
				{
					dietTracker.ReCount();

                    int expectedVariety = VarietyExpectation.GetVarietyExpectation(dietTracker.Pawn);
                    VarietyAdjuster.AdjustVarietyLevel(dietTracker, expectedVariety);
                }
            }

			listing_Standard.Label("Other Options:", -1f, null);
			bool flag4 = ModSettings_VarietyMatters.foodTrackingType == New.FoodTrackingType.ByIngredients || ModSettings_VarietyMatters.foodTrackingType == New.FoodTrackingType.ByMealAndIngredients;
            if (flag4)
			{
				listing_Standard.CheckboxLabeled("     Cooks use different ingredients: ", ref ModSettings_VarietyMatters.preferVariety, null, 0f, 1f);
				listing_Standard.CheckboxLabeled("     Cooks prefer spoiling ingredients: ", ref ModSettings_VarietyMatters.preferSpoiling, null, 0f, 1f);
				listing_Standard.CheckboxLabeled("     Stack meals by ingredients: ", ref ModSettings_VarietyMatters.stackByIngredients, null, 0f, 1f);

				string label = "     Ingredients When Stacking (vanilla = 3):";
				string text = ModSettings_VarietyMatters.numIngredients.ToString();
				this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label, ref ModSettings_VarietyMatters.numIngredients, ref text, 1, 1, 1, 10);
			}

            listing_Standard.GapLine(12f);
			bool flag5 = listing_Standard.ButtonTextLabeled("Expectation Level Base Varieties:", "Reset", 0, null, null);
			if (flag5)
			{
				ModSettings_VarietyMatters.extremelyLowVariety = 2;
				ModSettings_VarietyMatters.veryLowVariety = 4;
				ModSettings_VarietyMatters.lowVariety = 6;
				ModSettings_VarietyMatters.moderateVariety = 8;
				ModSettings_VarietyMatters.highVariety = 10;
				ModSettings_VarietyMatters.skyHighVariety = 12;
				ModSettings_VarietyMatters.nobleVariety = 15;
                ModSettings_VarietyMatters.slaveExpectedVarietyPercentage = 3;
                ModSettings_VarietyMatters.slavesHaveVarietyNeed = false;
                ModSettings_VarietyMatters.prisonersHaveVarietyNeed = false;
            }
            listing_Standard.Gap(8f);
			string label2 = "     Extremely Low (default 2):";
			string text2 = ModSettings_VarietyMatters.extremelyLowVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label2, ref ModSettings_VarietyMatters.extremelyLowVariety, ref text2, 1, 5, 1, 50);
			string label3 = "     Very Low (default 4):";
			string text3 = ModSettings_VarietyMatters.veryLowVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label3, ref ModSettings_VarietyMatters.veryLowVariety, ref text3, 1, 5, 1, 50);
			string label4 = "     Low (default 6):";
			string text4 = ModSettings_VarietyMatters.lowVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label4, ref ModSettings_VarietyMatters.lowVariety, ref text4, 1, 5, 1, 50);
			string label5 = "     Moderate (default 8):";
			string text5 = ModSettings_VarietyMatters.moderateVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label5, ref ModSettings_VarietyMatters.moderateVariety, ref text5, 1, 5, 1, 50);
			string label6 = "     High (default 10):";
			string text6 = ModSettings_VarietyMatters.highVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label6, ref ModSettings_VarietyMatters.highVariety, ref text6, 1, 5, 1, 50);
			string label7 = "     Sky High (default 12):";
			string text7 = ModSettings_VarietyMatters.skyHighVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label7, ref ModSettings_VarietyMatters.skyHighVariety, ref text7, 1, 5, 1, 50);
			string label8 = "     Noble and Royal (default 15):";
			string text8 = ModSettings_VarietyMatters.nobleVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label8, ref ModSettings_VarietyMatters.nobleVariety, ref text8, 1, 5, 1, 50);


            string labelSlavesHaveVarietyNeed = "     Slaves have variety need (default true):";
			listing_Standard.CheckboxLabeled(labelSlavesHaveVarietyNeed, ref ModSettings_VarietyMatters.slavesHaveVarietyNeed, null, 0f, 1f);

			if (ModSettings_VarietyMatters.slavesHaveVarietyNeed)
			{
				string labelDivideSlaveVariety = "     Slave expected variety (default 65%):";
				string textDivideSlaveVariety = ModSettings_VarietyMatters.slaveExpectedVarietyPercentage.ToString();
				this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), labelDivideSlaveVariety, ref ModSettings_VarietyMatters.slaveExpectedVarietyPercentage, ref textDivideSlaveVariety, 1, 5, 1, 50);
			}

            string labelPrisonersHaveVarietyNeed = "     Prisoners have variety need (default true):";
            listing_Standard.CheckboxLabeled(labelPrisonersHaveVarietyNeed, ref ModSettings_VarietyMatters.prisonersHaveVarietyNeed, null, 0f, 1f);

            if (ModSettings_VarietyMatters.prisonersHaveVarietyNeed)
            {
                string labelDividePrisonerVariety = "     Prisoner expected variety (default 50%):";
                string textDividePrisonerVariety = ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage.ToString();
                this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), labelDividePrisonerVariety, ref ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage, ref textDividePrisonerVariety, 1, 5, 1, 50);
            }

            listing_Standard.GapLine(12f);
			listing_Standard.Label("Optional Variety Expectation Adjustments Factors:", -1f, null);
            listing_Standard.CheckboxLabeled("     Halve variety mood effect: ", ref ModSettings_VarietyMatters.halveVarietyMoodImpact, null, 0f, 1f);
            listing_Standard.CheckboxLabeled("     Longer variety meals memory for pawns (Easier): ", ref ModSettings_VarietyMatters.moreVarietyMemory, null, 0f, 1f);
            listing_Standard.End();
            Widgets.EndScrollView();

            Rect rect2 = new Rect(50f + (inRect.width * 0.5f), 0f, inRect.width * 0.4f, 50f);
			listing_Standard.Begin(rect2);
			bool flag6 = listing_Standard.ButtonTextLabeled("Enable/Disable Variety:", "Reset Current Races", 0, null, null);
			if (flag6)
			{
				ModSettings_VarietyMatters.raceVariety.Clear();
				ModSettings_VarietyMatters.GenerateRaces();
			}
			listing_Standard.End();
			List<string> curRaces = ModSettings_VarietyMatters.curRaces;
			Rect rect3 = new Rect(50f + (inRect.width * 0.5f), 50f, inRect.width * 0.4f, inRect.height - 10f);
			Rect rect4 = new Rect(0f, 0f, rect3.width - 30f, (float)curRaces.Count * 24f);
			Widgets.BeginScrollView(rect3, ref Mod_VarietyMatters.rightScrollPosition, rect4, true);
			listing_Standard.Begin(rect4);
			for (int i = 0; i < curRaces.Count; i++)
			{
				string text10 = curRaces[i];
				bool value = ModSettings_VarietyMatters.raceVariety[text10];
				listing_Standard.CheckboxLabeled(GenText.CapitalizeFirst(text10), ref value, null, 0f, 1f);
				ModSettings_VarietyMatters.raceVariety[text10] = value;
			}
			listing_Standard.End();
			Widgets.EndScrollView();
			base.DoSettingsWindowContents(inRect);
		}

		// Token: 0x06000015 RID: 21 RVA: 0x00002D50 File Offset: 0x00000F50
		public void LabeledIntEntry(Rect rect, string label, ref int value, ref string editBuffer, int multiplier, int largeMultiplier, int min, int max)
		{
			int num = (int)rect.width / 15;
			Widgets.Label(rect, label);
			bool flag = multiplier != largeMultiplier;
			if (flag)
			{
				bool flag2 = Widgets.ButtonText(new Rect(rect.xMax - ((float)num * 5f), rect.yMin, (float)num, rect.height), (-1 * largeMultiplier).ToString(), true, true, true, null);
				if (flag2)
				{
					value -= largeMultiplier * GenUI.CurrentAdjustmentMultiplier();
					editBuffer = value.ToString();
					SoundStarter.PlayOneShotOnCamera(SoundDefOf.Checkbox_TurnedOff, null);
				}
				bool flag3 = Widgets.ButtonText(new Rect(rect.xMax - (float)num, rect.yMin, (float)num, rect.height), "+" + largeMultiplier.ToString(), true, true, true, null);
				if (flag3)
				{
					value += largeMultiplier * multiplier * GenUI.CurrentAdjustmentMultiplier();
					editBuffer = value.ToString();
					SoundStarter.PlayOneShotOnCamera(SoundDefOf.Checkbox_TurnedOn, null);
				}
			}
			bool flag4 = Widgets.ButtonText(new Rect(rect.xMax - ((float)num * 4f), rect.yMin, (float)num, rect.height), (-1 * multiplier).ToString(), true, true, true, null);
			if (flag4)
			{
				value -= GenUI.CurrentAdjustmentMultiplier();
				editBuffer = value.ToString();
				SoundStarter.PlayOneShotOnCamera(SoundDefOf.Checkbox_TurnedOff, null);
			}
			bool flag5 = Widgets.ButtonText(new Rect(rect.xMax - (float)(num * 2), rect.yMin, (float)num, rect.height), "+" + multiplier.ToString(), true, true, true, null);
			if (flag5)
			{
				value += multiplier * GenUI.CurrentAdjustmentMultiplier();
				editBuffer = value.ToString();
				SoundStarter.PlayOneShotOnCamera(SoundDefOf.Checkbox_TurnedOn, null);
			}
			Widgets.TextFieldNumeric<int>(new Rect(rect.xMax - (float)(num * 3), rect.yMin, (float)num, rect.height), ref value, ref editBuffer, (float)min, (float)max);
		}

		// Token: 0x06000016 RID: 22 RVA: 0x00002F68 File Offset: 0x00001168
		public override string SettingsCategory()
		{
			return "VarietyMatters";
		}

		// Token: 0x04000023 RID: 35
		private static Vector2 rightScrollPosition = Vector2.zero;

        private static Vector2 leftScrollPosition = Vector2.zero;

    }
}
