namespace VarietyMatters
{
    using System.Linq;
    using System.Reflection;
    using HarmonyLib;
    using RimWorld;
    using UnityEngine;
    using VarietyMatters.New;
    using VarietyMatters.New.Performance;
    using Verse;
    using Verse.Sound;

    // Token: 0x0200000B RID: 11
    internal class Mod_VarietyMatters : Mod
	{
		// Token: 0x06000012 RID: 18 RVA: 0x00002744 File Offset: 0x00000944
		public Mod_VarietyMatters(ModContentPack content) : base(content)
		{
            GetSettings<ModSettings_VarietyMatters>();
			Harmony harmony = new Harmony("rimworld.varietymatters");
			harmony.PatchAll(Assembly.GetExecutingAssembly());

			// Warn once at startup if Vanilla Food Variety Expanded is co-loaded —
			// both mods transpile CompIngredients.MergeIngredients and stacking behavior is undefined.
			if (LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "vanillaexpanded.vanillafoodvarietyexpanded"))
			{
				Log.Warning("VarietyMatters: Vanilla Food Variety Expanded is also loaded. Both mods transpile CompIngredients.MergeIngredients, which causes undefined stacking behavior. Disable one of them to avoid issues.");
			}
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

            // Scroll view height is driven by the measured content height from the previous frame
            // (_lastLeftContentHeight). Seeded at 900f so the first frame is tall enough for the
            // default settings layout; subsequent frames use the real measured value so conditional
            // sections (slave/prisoner sub-sliders) resize the scrollbar correctly.
            float contentHeight1 = Mathf.Max(_lastLeftContentHeight, 200f);
            Rect rectScroll = new Rect(5f, 35f, inRect.width * 0.54f, inRect.height);
            Rect viewRect1 = new Rect(0f, 0f, rectScroll.width - 16f, contentHeight1); // 16f is the width of the scrollbar
            Widgets.BeginScrollView(rectScroll, ref leftScrollPosition, viewRect1);

            Rect rect = new Rect(10f, 0f, inRect.width * 0.5f, contentHeight1);
			listing_Standard.Begin(rect);
			listing_Standard.Label("VM_Settings_VarietyTrackingHeader".Translate(), -1f);

			// Single tracking mode now (ByIngredientOverlap). Legacy mode picker removed — see
			// the mode description/tooltip for what this means.
			listing_Standard.Label("VM_Settings_TrackByIngredientOverlap".Translate(),
				-1f, new TipSignal((string)"VM_Settings_TrackByIngredientOverlapTooltip".Translate()));

			listing_Standard.Label("VM_Settings_OtherOptionsHeader".Translate(), -1f);
			listing_Standard.CheckboxLabeled("VM_Settings_NonControlledPawnsLabel".Translate(), ref ModSettings_VarietyMatters.nonControlledPawnsHaveVarietyNeed, null, 0f, 1f);
            listing_Standard.CheckboxLabeled("VM_Settings_FoodDrugsOnceLabel".Translate(), ref ModSettings_VarietyMatters.foodDrugsAreOnlyInMemoryOnce, "VM_Settings_FoodDrugsOnceTooltip".Translate(), 0f, 1f);
			listing_Standard.CheckboxLabeled("VM_Settings_PreferVarietyLabel".Translate(), ref ModSettings_VarietyMatters.preferVariety, "VM_Settings_PreferVarietyTooltip".Translate(), 0f, 1f);
			listing_Standard.CheckboxLabeled("VM_Settings_PreferSpoilingLabel".Translate(), ref ModSettings_VarietyMatters.preferSpoiling, null, 0f, 1f);
			listing_Standard.CheckboxLabeled("VM_Settings_StackByIngredientsLabel".Translate(), ref ModSettings_VarietyMatters.stackByIngredients, "VM_Settings_StackByIngredientsTooltip".Translate(), 0f, 1f);

			string label = "VM_Settings_NumIngredientsLabel".Translate();
			string text = ModSettings_VarietyMatters.numIngredients.ToString();
			Rect numIngRect = listing_Standard.GetRect(24f, 1f);
			this.LabeledIntEntry(numIngRect, label, ref ModSettings_VarietyMatters.numIngredients, ref text, 1, 1, 1, 10);
			TooltipHandler.TipRegion(numIngRect, "VM_Settings_NumIngredientsTooltip".Translate());

			string cookingMemLabel = "VM_Settings_CookingMemoryDepthLabel".Translate();
			string cookingMemText = ModSettings_VarietyMatters.cookingMemoryDepth.ToString();
			Rect cookingMemRect = listing_Standard.GetRect(24f, 1f);
			this.LabeledIntEntry(cookingMemRect, cookingMemLabel, ref ModSettings_VarietyMatters.cookingMemoryDepth, ref cookingMemText, 1, 5, 1, 100);
			TooltipHandler.TipRegion(cookingMemRect, "VM_Settings_CookingMemoryDepthTooltip".Translate());

            listing_Standard.GapLine(12f);
			bool flag5 = listing_Standard.ButtonTextLabeled("VM_Settings_ExpectationHeader".Translate(), "VM_Settings_ResetButton".Translate(), 0, null, null);
			if (flag5)
			{
				ModSettings_VarietyMatters.extremelyLowVariety = 4;
				ModSettings_VarietyMatters.veryLowVariety = 5;
				ModSettings_VarietyMatters.lowVariety = 6;
				ModSettings_VarietyMatters.moderateVariety = 8;
				ModSettings_VarietyMatters.highVariety = 10;
				ModSettings_VarietyMatters.skyHighVariety = 12;
				ModSettings_VarietyMatters.nobleVariety = 15;
				ModSettings_VarietyMatters.lowExpectationFloor = 3;
				DebouncedSettingsHandler.QueueRecalculation();
                ModSettings_VarietyMatters.slaveExpectedVarietyPercentage = 3;
                ModSettings_VarietyMatters.slavesHaveVarietyNeed = false;
                ModSettings_VarietyMatters.prisonersHaveVarietyNeed = false;
            }
            listing_Standard.Gap(8f);
			// US-026 dual-purpose callout so players know these values now scale both memory
			// capacity AND palate threshold (baseline = clamp((value − 3)/25, 0, 0.50)).
			listing_Standard.Label("VM_Settings_ExpectationExplanation".Translate(), -1f);
			listing_Standard.Gap(4f);
			string label2 = "VM_Settings_ExtremelyLowLabel".Translate();
			string text2 = ModSettings_VarietyMatters.extremelyLowVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label2, ref ModSettings_VarietyMatters.extremelyLowVariety, ref text2, 1, 5, 1, 50);
			string label3 = "VM_Settings_VeryLowLabel".Translate();
			string text3 = ModSettings_VarietyMatters.veryLowVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label3, ref ModSettings_VarietyMatters.veryLowVariety, ref text3, 1, 5, 1, 50);
			string label4 = "VM_Settings_LowLabel".Translate();
			string text4 = ModSettings_VarietyMatters.lowVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label4, ref ModSettings_VarietyMatters.lowVariety, ref text4, 1, 5, 1, 50);
			string label5 = "VM_Settings_ModerateLabel".Translate();
			string text5 = ModSettings_VarietyMatters.moderateVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label5, ref ModSettings_VarietyMatters.moderateVariety, ref text5, 1, 5, 1, 50);
			string label6 = "VM_Settings_HighLabel".Translate();
			string text6 = ModSettings_VarietyMatters.highVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label6, ref ModSettings_VarietyMatters.highVariety, ref text6, 1, 5, 1, 50);
			string label7 = "VM_Settings_SkyHighLabel".Translate();
			string text7 = ModSettings_VarietyMatters.skyHighVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label7, ref ModSettings_VarietyMatters.skyHighVariety, ref text7, 1, 5, 1, 50);
			string label8 = "VM_Settings_NobleLabel".Translate();
			string text8 = ModSettings_VarietyMatters.nobleVariety.ToString();
			this.LabeledIntEntry(listing_Standard.GetRect(24f, 1f), label8, ref ModSettings_VarietyMatters.nobleVariety, ref text8, 1, 5, 1, 50);
			int previousLowExpectationFloor = ModSettings_VarietyMatters.lowExpectationFloor;
			string label9 = "VM_Settings_LowExpectationFloorLabel".Translate();
			string text9 = ModSettings_VarietyMatters.lowExpectationFloor.ToString();
			Rect floorRect = listing_Standard.GetRect(24f, 1f);
			this.LabeledIntEntry(floorRect, label9, ref ModSettings_VarietyMatters.lowExpectationFloor, ref text9, 1, 5, 1, 50);
			TooltipHandler.TipRegion(floorRect, "VM_Settings_LowExpectationFloorTooltip".Translate());
			if (ModSettings_VarietyMatters.lowExpectationFloor != previousLowExpectationFloor)
			{
				DebouncedSettingsHandler.QueueRecalculation();
			}


            string labelSlavesHaveVarietyNeed = "VM_Settings_SlavesHaveVarietyLabel".Translate();
			listing_Standard.CheckboxLabeled(labelSlavesHaveVarietyNeed, ref ModSettings_VarietyMatters.slavesHaveVarietyNeed, null, 0f, 1f);

			if (ModSettings_VarietyMatters.slavesHaveVarietyNeed)
			{
				string labelDivideSlaveVariety = "VM_Settings_SlaveExpectedVarietyLabel".Translate();
				string textDivideSlaveVariety = ModSettings_VarietyMatters.slaveExpectedVarietyPercentage.ToString();
				Rect slaveRect = listing_Standard.GetRect(24f, 1f);
				this.LabeledIntEntry(slaveRect, labelDivideSlaveVariety, ref ModSettings_VarietyMatters.slaveExpectedVarietyPercentage, ref textDivideSlaveVariety, 1, 5, 1, 100);
				TooltipHandler.TipRegion(slaveRect, "VM_Settings_SlaveExpectedVarietyTooltip".Translate());
			}

            string labelPrisonersHaveVarietyNeed = "VM_Settings_PrisonersHaveVarietyLabel".Translate();
            listing_Standard.CheckboxLabeled(labelPrisonersHaveVarietyNeed, ref ModSettings_VarietyMatters.prisonersHaveVarietyNeed, null, 0f, 1f);

            if (ModSettings_VarietyMatters.prisonersHaveVarietyNeed)
            {
                string labelDividePrisonerVariety = "VM_Settings_PrisonerExpectedVarietyLabel".Translate();
                string textDividePrisonerVariety = ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage.ToString();
                Rect prisonerRect = listing_Standard.GetRect(24f, 1f);
                this.LabeledIntEntry(prisonerRect, labelDividePrisonerVariety, ref ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage, ref textDividePrisonerVariety, 1, 5, 1, 100);
                TooltipHandler.TipRegion(prisonerRect, "VM_Settings_PrisonerExpectedVarietyTooltip".Translate());
            }

            listing_Standard.GapLine(12f);
			listing_Standard.Label("VM_Settings_AdjustmentFactorsHeader".Translate(), -1f);
            listing_Standard.CheckboxLabeled("VM_Settings_HalveVarietyMoodLabel".Translate(), ref ModSettings_VarietyMatters.halveVarietyMoodImpact, "VM_Settings_HalveVarietyMoodTooltip".Translate(), 0f, 1f);
            listing_Standard.CheckboxLabeled("VM_Settings_MoreVarietyMemoryLabel".Translate(), ref ModSettings_VarietyMatters.moreVarietyMemory, "VM_Settings_MoreVarietyMemoryTooltip".Translate(), 0f, 1f);
            // Capture measured content height so next frame's scrollbar matches the real layout.
            // +24f padding keeps the last row from flushing to the exact bottom.
            _lastLeftContentHeight = listing_Standard.CurHeight + 24f;
            listing_Standard.End();
            Widgets.EndScrollView();

            Rect rect2 = new Rect(50f + (inRect.width * 0.5f), 0f, inRect.width * 0.4f, 50f);
			listing_Standard.Begin(rect2);
			bool resetRacesSettings = listing_Standard.ButtonTextLabeled("VM_Settings_RaceSectionHeader".Translate(), "VM_Settings_ResetRacesButton".Translate(), 0, null, null);
			if (resetRacesSettings || ModSettings_VarietyMatters.raceVariety == null)
			{
				ModSettings_VarietyMatters.raceVariety?.Clear();
				ModSettings_VarietyMatters.GenerateRaces();
			}
			listing_Standard.End();

			if (ModSettings_VarietyMatters.raceVariety != null)
			{
				Rect rect3 = new Rect(50f + (inRect.width * 0.5f), 50f, inRect.width * 0.4f, inRect.height - 10f);
				Rect rect4 = new Rect(0f, 0f, rect3.width - 30f, ModSettings_VarietyMatters.raceVariety.Count * 24f);
				Widgets.BeginScrollView(rect3, ref rightScrollPosition, rect4, true);
				listing_Standard.Begin(rect4);

				foreach (var raceSettings in ModSettings_VarietyMatters.raceVariety.Values.OrderBy(x => x.name))
				{
					if (raceSettings == null)
					{
						Log.Warning("raceSetting is null");
					}

					if (raceSettings.name == null)
					{
						Log.Warning("raceSetting.name is null");
					}

					bool value = raceSettings.isVarietyEnabled;
					string modName = raceSettings.modName;
					listing_Standard.CheckboxLabeled(GenText.CapitalizeFirst(raceSettings.name), ref value, "VM_Settings_FromMod".Translate(modName), 0f, 1f);
					if (raceSettings.isVarietyEnabled != value)
					{
						raceSettings.isVarietyEnabled = value;
						ModSettings_VarietyMatters.raceVarietyVersion++;
					}
				}

				listing_Standard.End();
				Widgets.EndScrollView();
			}

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
			return "VM_Settings_Category".Translate();
		}

		// Token: 0x04000023 RID: 35
		private static Vector2 rightScrollPosition = Vector2.zero;

        private static Vector2 leftScrollPosition = Vector2.zero;

        // Measured content height of the left settings column. Seeded generously so the first
        // frame renders all default rows without clipping; refreshed each frame from the real
        // listing height so conditional sections (slave/prisoner sub-sliders) resize the
        // scrollbar correctly as they toggle on/off.
        private static float _lastLeftContentHeight = 900f;

    }
}
