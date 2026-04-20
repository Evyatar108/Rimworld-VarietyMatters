namespace VarietyMatters
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using HarmonyLib;
    using RimWorld;
    using UnityEngine;
    using VarietyMatters.New;
    using VarietyMatters.New.Performance;
    using Verse;
    using Verse.Sound;

    internal class Mod_VarietyMatters : Mod
    {
        // US-028 preset profile. Covers the 14 gameplay-affecting fields a difficulty preset
        // is allowed to set. When adding a new gameplay-affecting setting to ModSettings,
        // update ALL of: this struct, the three Profile instances, ApplyProfile, MatchesProfile,
        // and (if defaults change) ModSettings field initializers.
        private struct Profile
        {
            public int extremelyLowVariety;
            public int veryLowVariety;
            public int lowVariety;
            public int moderateVariety;
            public int highVariety;
            public int skyHighVariety;
            public int nobleVariety;
            public int lowExpectationFloor;
            public bool halveVarietyMoodImpact;
            public bool moreVarietyMemory;
            public bool slavesHaveVarietyNeed;
            public bool prisonersHaveVarietyNeed;
            public int slaveExpectedVarietyPercentage;
            public int prisonerExpectedVarietyPercentage;
        }

        private static readonly Profile Easy = new Profile
        {
            extremelyLowVariety = 3, veryLowVariety = 4, lowVariety = 5,
            moderateVariety = 6, highVariety = 8, skyHighVariety = 10,
            nobleVariety = 12, lowExpectationFloor = 3,
            halveVarietyMoodImpact = true, moreVarietyMemory = true,
            slavesHaveVarietyNeed = true, prisonersHaveVarietyNeed = true,
            slaveExpectedVarietyPercentage = 50, prisonerExpectedVarietyPercentage = 40,
        };

        // Default_ MUST match ModSettings_VarietyMatters field initializers exactly. If a field
        // default changes in ModSettings, update the matching slot here or the "Active: Default"
        // highlight will be wrong for fresh installs.
        private static readonly Profile Default_ = new Profile
        {
            extremelyLowVariety = 4, veryLowVariety = 5, lowVariety = 6,
            moderateVariety = 8, highVariety = 10, skyHighVariety = 12,
            nobleVariety = 15, lowExpectationFloor = 3,
            halveVarietyMoodImpact = false, moreVarietyMemory = false,
            slavesHaveVarietyNeed = true, prisonersHaveVarietyNeed = true,
            slaveExpectedVarietyPercentage = 65, prisonerExpectedVarietyPercentage = 50,
        };

        private static readonly Profile Hardcore = new Profile
        {
            extremelyLowVariety = 5, veryLowVariety = 6, lowVariety = 8,
            moderateVariety = 10, highVariety = 12, skyHighVariety = 15,
            nobleVariety = 18, lowExpectationFloor = 4,
            halveVarietyMoodImpact = false, moreVarietyMemory = false,
            slavesHaveVarietyNeed = true, prisonersHaveVarietyNeed = true,
            slaveExpectedVarietyPercentage = 80, prisonerExpectedVarietyPercentage = 65,
        };

        // Transient UI state — not Scribed (per-session section expand/collapse).
        private static readonly Dictionary<string, bool> _sectionExpanded = new Dictionary<string, bool>
        {
            { "basics", false },
            { "difficulty", false },
            { "expectations", false },
            { "cook", false },
            { "scoring", false },
        };

        // IntEntry edit buffers. RimWorld's TextFieldNumeric needs stable ref string storage.
        // Nulled on preset apply / tier reset so next draw reseeds from the current value.
        private static string _numIngredientsBuf;
        private static string _cookingMemoryBuf;
        private static string _floorBuf;
        private static string _slaveBuf;
        private static string _prisonerBuf;
        private static string _exLowBuf;
        private static string _vLowBuf;
        private static string _lowBuf;
        private static string _modBuf;
        private static string _highBuf;
        private static string _skyHiBuf;
        private static string _nobleBuf;

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

        public override void WriteSettings()
        {
            base.WriteSettings();
        }

        public override string SettingsCategory()
        {
            return "VM_Settings_Category".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new Listing_Standard();

            float contentHeight1 = Mathf.Max(_lastLeftContentHeight, 200f);
            Rect rectScroll = new Rect(5f, 35f, inRect.width * 0.54f, inRect.height);
            Rect viewRect1 = new Rect(0f, 0f, rectScroll.width - 16f, contentHeight1);
            Widgets.BeginScrollView(rectScroll, ref leftScrollPosition, viewRect1);

            Rect rect = new Rect(10f, 0f, inRect.width * 0.5f, contentHeight1);
            listing_Standard.Begin(rect);

            DrawPresetStrip(listing_Standard);
            listing_Standard.GapLine(8f);

            if (DrawSectionHeader(listing_Standard, "basics", "VM_Settings_Section_Basics"))
            {
                DrawBasicsSection(listing_Standard);
            }
            if (DrawSectionHeader(listing_Standard, "difficulty", "VM_Settings_Section_Difficulty"))
            {
                DrawDifficultySection(listing_Standard);
            }
            if (DrawSectionHeader(listing_Standard, "expectations", "VM_Settings_Section_PawnExpectations"))
            {
                DrawExpectationsSection(listing_Standard);
            }
            if (DrawSectionHeader(listing_Standard, "cook", "VM_Settings_Section_CookBehavior"))
            {
                DrawCookSection(listing_Standard);
            }
            if (DrawSectionHeader(listing_Standard, "scoring", "VM_Settings_Section_Scoring"))
            {
                DrawScoringSection(listing_Standard);
            }

            // Capture measured content height so next frame's scrollbar matches the real layout.
            _lastLeftContentHeight = listing_Standard.CurHeight + 24f;
            listing_Standard.End();
            Widgets.EndScrollView();

            DrawRacePanel(inRect, listing_Standard);

            base.DoSettingsWindowContents(inRect);
        }

        private void DrawPresetStrip(Listing_Standard ls)
        {
            string active = DetectActivePreset();
            ls.Label("VM_Settings_Presets_Header".Translate(), -1f);

            Rect row = ls.GetRect(32f, 1f);
            float gap = 8f;
            float btnW = (row.width - 2f * gap) / 3f;

            Rect easyRect = new Rect(row.x, row.y, btnW, row.height);
            Rect defaultRect = new Rect(row.x + btnW + gap, row.y, btnW, row.height);
            Rect hardcoreRect = new Rect(row.x + 2f * (btnW + gap), row.y, btnW, row.height);

            DrawPresetButton(easyRect, "VM_Settings_Preset_Easy", Easy, active == "Easy", "VM_Settings_Preset_Easy_Tooltip");
            DrawPresetButton(defaultRect, "VM_Settings_Preset_Default", Default_, active == "Default", "VM_Settings_Preset_Default_Tooltip");
            DrawPresetButton(hardcoreRect, "VM_Settings_Preset_Hardcore", Hardcore, active == "Hardcore", "VM_Settings_Preset_Hardcore_Tooltip");

            ls.Gap(4f);
            string descKey;
            if (active == "Easy") descKey = "VM_Settings_Preset_Easy_Desc";
            else if (active == "Default") descKey = "VM_Settings_Preset_Default_Desc";
            else if (active == "Hardcore") descKey = "VM_Settings_Preset_Hardcore_Desc";
            else descKey = "VM_Settings_Preset_CustomDesc";

            Color prevColor = GUI.color;
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            ls.Label(descKey.Translate(), -1f);
            GUI.color = prevColor;
        }

        private void DrawPresetButton(Rect rect, string labelKey, Profile profile, bool active, string tooltipKey)
        {
            if (active)
            {
                Widgets.DrawHighlight(rect);
            }
            if (Widgets.ButtonText(rect, labelKey.Translate()))
            {
                ApplyProfile(profile);
                SoundStarter.PlayOneShotOnCamera(SoundDefOf.Checkbox_TurnedOn, null);
            }
            TooltipHandler.TipRegion(rect, tooltipKey.Translate());
        }

        // Returns the current expanded state and draws the clickable header row. Allocates 24f
        // of layout height via ls.GetRect so CurHeight advances correctly for scroll measurement.
        private bool DrawSectionHeader(Listing_Standard ls, string id, string labelKey)
        {
            bool expanded = _sectionExpanded.TryGetValue(id, out bool e) && e;
            Rect headerRect = ls.GetRect(26f, 1f);
            Widgets.DrawHighlightIfMouseover(headerRect);

            string arrow = expanded ? "v  " : ">  ";
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(headerRect.x + 4f, headerRect.y, headerRect.width - 8f, headerRect.height),
                arrow + (string)labelKey.Translate());
            Text.Anchor = prevAnchor;
            Text.Font = prevFont;

            if (Widgets.ButtonInvisible(headerRect, true))
            {
                expanded = !expanded;
                _sectionExpanded[id] = expanded;
                SoundStarter.PlayOneShotOnCamera(
                    expanded ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff, null);
                // Pre-inflate the content-height cache so the scroll viewport grows for the SAME
                // frame the newly-expanded section renders into. Without this, the first frame
                // after expansion clips the tail of the section (viewport still sized from the
                // previous frame's collapsed layout) and users perceive it as "nothing showed up"
                // until the next mouse-move triggers a redraw.
                if (expanded)
                {
                    _lastLeftContentHeight = Mathf.Max(_lastLeftContentHeight, 1500f);
                }
            }

            if (expanded)
            {
                ls.Gap(4f);
            }
            else
            {
                ls.Gap(2f);
            }
            return expanded;
        }

        private void DrawBasicsSection(Listing_Standard ls)
        {
            ls.CheckboxLabeled("VM_Settings_NonControlledPawnsLabel".Translate(),
                ref ModSettings_VarietyMatters.nonControlledPawnsHaveVarietyNeed,
                "VM_Settings_NonControlledPawnsTooltip".Translate(), 0f, 1f);
            ls.CheckboxLabeled("VM_Settings_FoodDrugsOnceLabel".Translate(),
                ref ModSettings_VarietyMatters.foodDrugsAreOnlyInMemoryOnce,
                "VM_Settings_FoodDrugsOnceTooltip".Translate(), 0f, 1f);
            ls.CheckboxLabeled("VM_Settings_PreferSpoilingLabel".Translate(),
                ref ModSettings_VarietyMatters.preferSpoiling,
                "VM_Settings_PreferSpoilingTooltip".Translate(), 0f, 1f);
            ls.Gap(8f);
        }

        private void DrawDifficultySection(Listing_Standard ls)
        {
            bool prevHalve = ModSettings_VarietyMatters.halveVarietyMoodImpact;
            ls.CheckboxLabeled("VM_Settings_HalveVarietyMoodLabel".Translate(),
                ref ModSettings_VarietyMatters.halveVarietyMoodImpact,
                "VM_Settings_HalveVarietyMoodTooltip".Translate(), 0f, 1f);
            if (prevHalve != ModSettings_VarietyMatters.halveVarietyMoodImpact)
            {
                QueueRecalculationForGameplayAffectingChange();
            }

            bool prevMem = ModSettings_VarietyMatters.moreVarietyMemory;
            ls.CheckboxLabeled("VM_Settings_MoreVarietyMemoryLabel".Translate(),
                ref ModSettings_VarietyMatters.moreVarietyMemory,
                "VM_Settings_MoreVarietyMemoryTooltip".Translate(), 0f, 1f);
            if (prevMem != ModSettings_VarietyMatters.moreVarietyMemory)
            {
                QueueRecalculationForGameplayAffectingChange();
            }

            int prevFloor = ModSettings_VarietyMatters.lowExpectationFloor;
            if (_floorBuf == null) _floorBuf = prevFloor.ToString();
            Rect floorRect = ls.GetRect(24f, 1f);
            this.LabeledIntEntry(floorRect, "VM_Settings_LowExpectationFloorLabel".Translate(),
                ref ModSettings_VarietyMatters.lowExpectationFloor, ref _floorBuf, 1, 5, 1, 50);
            TooltipHandler.TipRegion(floorRect, "VM_Settings_LowExpectationFloorTooltip".Translate());
            if (prevFloor != ModSettings_VarietyMatters.lowExpectationFloor)
            {
                QueueRecalculationForGameplayAffectingChange();
            }

            bool prevSlavesNeed = ModSettings_VarietyMatters.slavesHaveVarietyNeed;
            ls.CheckboxLabeled("VM_Settings_SlavesHaveVarietyLabel".Translate(),
                ref ModSettings_VarietyMatters.slavesHaveVarietyNeed, null, 0f, 1f);
            if (prevSlavesNeed != ModSettings_VarietyMatters.slavesHaveVarietyNeed)
            {
                QueueRecalculationForGameplayAffectingChange();
            }
            if (ModSettings_VarietyMatters.slavesHaveVarietyNeed)
            {
                int prev = ModSettings_VarietyMatters.slaveExpectedVarietyPercentage;
                if (_slaveBuf == null) _slaveBuf = prev.ToString();
                Rect r = ls.GetRect(24f, 1f);
                this.LabeledIntEntry(r, "VM_Settings_SlaveExpectedVarietyLabel".Translate(),
                    ref ModSettings_VarietyMatters.slaveExpectedVarietyPercentage, ref _slaveBuf, 1, 5, 1, 100);
                TooltipHandler.TipRegion(r, "VM_Settings_SlaveExpectedVarietyTooltip".Translate());
                if (prev != ModSettings_VarietyMatters.slaveExpectedVarietyPercentage)
                {
                    QueueRecalculationForGameplayAffectingChange();
                }
            }

            bool prevPrisNeed = ModSettings_VarietyMatters.prisonersHaveVarietyNeed;
            ls.CheckboxLabeled("VM_Settings_PrisonersHaveVarietyLabel".Translate(),
                ref ModSettings_VarietyMatters.prisonersHaveVarietyNeed, null, 0f, 1f);
            if (prevPrisNeed != ModSettings_VarietyMatters.prisonersHaveVarietyNeed)
            {
                QueueRecalculationForGameplayAffectingChange();
            }
            if (ModSettings_VarietyMatters.prisonersHaveVarietyNeed)
            {
                int prev = ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage;
                if (_prisonerBuf == null) _prisonerBuf = prev.ToString();
                Rect r = ls.GetRect(24f, 1f);
                this.LabeledIntEntry(r, "VM_Settings_PrisonerExpectedVarietyLabel".Translate(),
                    ref ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage, ref _prisonerBuf, 1, 5, 1, 100);
                TooltipHandler.TipRegion(r, "VM_Settings_PrisonerExpectedVarietyTooltip".Translate());
                if (prev != ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage)
                {
                    QueueRecalculationForGameplayAffectingChange();
                }
            }
            ls.Gap(8f);
        }

        private void DrawExpectationsSection(Listing_Standard ls)
        {
            // Local reset button: resets tier values + lowExpectationFloor to Default_ profile's tier-related fields.
            // Does NOT reset Difficulty-section fields — those belong to the preset strip.
            Rect resetRow = ls.GetRect(26f, 1f);
            if (Widgets.ButtonText(new Rect(resetRow.xMax - 80f, resetRow.y, 72f, resetRow.height),
                "VM_Settings_ResetButton".Translate()))
            {
                ModSettings_VarietyMatters.extremelyLowVariety = Default_.extremelyLowVariety;
                ModSettings_VarietyMatters.veryLowVariety = Default_.veryLowVariety;
                ModSettings_VarietyMatters.lowVariety = Default_.lowVariety;
                ModSettings_VarietyMatters.moderateVariety = Default_.moderateVariety;
                ModSettings_VarietyMatters.highVariety = Default_.highVariety;
                ModSettings_VarietyMatters.skyHighVariety = Default_.skyHighVariety;
                ModSettings_VarietyMatters.nobleVariety = Default_.nobleVariety;
                _exLowBuf = _vLowBuf = _lowBuf = _modBuf = _highBuf = _skyHiBuf = _nobleBuf = null;
                QueueRecalculationForGameplayAffectingChange();
            }

            ls.Label("VM_Settings_ExpectationExplanation".Translate(), -1f);
            ls.Gap(4f);

            DrawTierEntry(ls, "VM_Settings_ExtremelyLowLabel", ref ModSettings_VarietyMatters.extremelyLowVariety, ref _exLowBuf);
            DrawTierEntry(ls, "VM_Settings_VeryLowLabel", ref ModSettings_VarietyMatters.veryLowVariety, ref _vLowBuf);
            DrawTierEntry(ls, "VM_Settings_LowLabel", ref ModSettings_VarietyMatters.lowVariety, ref _lowBuf);
            DrawTierEntry(ls, "VM_Settings_ModerateLabel", ref ModSettings_VarietyMatters.moderateVariety, ref _modBuf);
            DrawTierEntry(ls, "VM_Settings_HighLabel", ref ModSettings_VarietyMatters.highVariety, ref _highBuf);
            DrawTierEntry(ls, "VM_Settings_SkyHighLabel", ref ModSettings_VarietyMatters.skyHighVariety, ref _skyHiBuf);
            DrawTierEntry(ls, "VM_Settings_NobleLabel", ref ModSettings_VarietyMatters.nobleVariety, ref _nobleBuf);

            // Monotonic cascade: each tier ≥ the previous one. Bumps later tiers up if the
            // player sets an earlier tier higher than its successor. Keeps the sequence
            // non-decreasing without trapping the player in a locked edit.
            EnforceTierMonotonicity();

            ls.Gap(8f);
        }

        private void DrawTierEntry(Listing_Standard ls, string labelKey, ref int value, ref string buf)
        {
            if (buf == null) buf = value.ToString();
            int prev = value;
            this.LabeledIntEntry(ls.GetRect(24f, 1f), labelKey.Translate(), ref value, ref buf, 1, 5, 1, 50);
            if (prev != value)
            {
                QueueRecalculationForGameplayAffectingChange();
            }
        }

        private static void EnforceTierMonotonicity()
        {
            bool changed = false;
            if (ModSettings_VarietyMatters.veryLowVariety < ModSettings_VarietyMatters.extremelyLowVariety)
            { ModSettings_VarietyMatters.veryLowVariety = ModSettings_VarietyMatters.extremelyLowVariety; _vLowBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.lowVariety < ModSettings_VarietyMatters.veryLowVariety)
            { ModSettings_VarietyMatters.lowVariety = ModSettings_VarietyMatters.veryLowVariety; _lowBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.moderateVariety < ModSettings_VarietyMatters.lowVariety)
            { ModSettings_VarietyMatters.moderateVariety = ModSettings_VarietyMatters.lowVariety; _modBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.highVariety < ModSettings_VarietyMatters.moderateVariety)
            { ModSettings_VarietyMatters.highVariety = ModSettings_VarietyMatters.moderateVariety; _highBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.skyHighVariety < ModSettings_VarietyMatters.highVariety)
            { ModSettings_VarietyMatters.skyHighVariety = ModSettings_VarietyMatters.highVariety; _skyHiBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.nobleVariety < ModSettings_VarietyMatters.skyHighVariety)
            { ModSettings_VarietyMatters.nobleVariety = ModSettings_VarietyMatters.skyHighVariety; _nobleBuf = null; changed = true; }
            if (changed)
            {
                QueueRecalculationForGameplayAffectingChange();
            }
        }

        private void DrawCookSection(Listing_Standard ls)
        {
            ls.CheckboxLabeled("VM_Settings_PreferVarietyLabel".Translate(),
                ref ModSettings_VarietyMatters.preferVariety,
                "VM_Settings_PreferVarietyTooltip".Translate(), 0f, 1f);
            ls.CheckboxLabeled("VM_Settings_StackByIngredientsLabel".Translate(),
                ref ModSettings_VarietyMatters.stackByIngredients,
                "VM_Settings_StackByIngredientsTooltip".Translate(), 0f, 1f);

            if (_numIngredientsBuf == null) _numIngredientsBuf = ModSettings_VarietyMatters.numIngredients.ToString();
            Rect numIngRect = ls.GetRect(24f, 1f);
            this.LabeledIntEntry(numIngRect, "VM_Settings_NumIngredientsLabel".Translate(),
                ref ModSettings_VarietyMatters.numIngredients, ref _numIngredientsBuf, 1, 1, 1, 10);
            TooltipHandler.TipRegion(numIngRect, "VM_Settings_NumIngredientsTooltip".Translate());

            if (_cookingMemoryBuf == null) _cookingMemoryBuf = ModSettings_VarietyMatters.cookingMemoryDepth.ToString();
            Rect cookingMemRect = ls.GetRect(24f, 1f);
            this.LabeledIntEntry(cookingMemRect, "VM_Settings_CookingMemoryDepthLabel".Translate(),
                ref ModSettings_VarietyMatters.cookingMemoryDepth, ref _cookingMemoryBuf, 1, 5, 1, 100);
            TooltipHandler.TipRegion(cookingMemRect, "VM_Settings_CookingMemoryDepthTooltip".Translate());

            ls.Gap(8f);
        }

        private void DrawScoringSection(Listing_Standard ls)
        {
            ls.Label("VM_Settings_Scoring_Body".Translate(), -1f);
            ls.Gap(8f);
        }

        private void DrawRacePanel(Rect inRect, Listing_Standard listing_Standard)
        {
            Rect rect2 = new Rect(50f + (inRect.width * 0.5f), 0f, inRect.width * 0.4f, 50f);
            listing_Standard.Begin(rect2);
            bool resetRacesSettings = listing_Standard.ButtonTextLabeled(
                "VM_Settings_RaceSectionHeader".Translate(),
                "VM_Settings_ResetRacesButton".Translate(), 0, null, null);
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
                    listing_Standard.CheckboxLabeled(GenText.CapitalizeFirst(raceSettings.name), ref value,
                        "VM_Settings_FromMod".Translate(modName), 0f, 1f);
                    if (raceSettings.isVarietyEnabled != value)
                    {
                        raceSettings.isVarietyEnabled = value;
                        ModSettings_VarietyMatters.raceVarietyVersion++;
                    }
                }

                listing_Standard.End();
                Widgets.EndScrollView();
            }
        }

        private static void ApplyProfile(Profile p)
        {
            ModSettings_VarietyMatters.extremelyLowVariety = p.extremelyLowVariety;
            ModSettings_VarietyMatters.veryLowVariety = p.veryLowVariety;
            ModSettings_VarietyMatters.lowVariety = p.lowVariety;
            ModSettings_VarietyMatters.moderateVariety = p.moderateVariety;
            ModSettings_VarietyMatters.highVariety = p.highVariety;
            ModSettings_VarietyMatters.skyHighVariety = p.skyHighVariety;
            ModSettings_VarietyMatters.nobleVariety = p.nobleVariety;
            ModSettings_VarietyMatters.lowExpectationFloor = p.lowExpectationFloor;
            ModSettings_VarietyMatters.halveVarietyMoodImpact = p.halveVarietyMoodImpact;
            ModSettings_VarietyMatters.moreVarietyMemory = p.moreVarietyMemory;
            ModSettings_VarietyMatters.slavesHaveVarietyNeed = p.slavesHaveVarietyNeed;
            ModSettings_VarietyMatters.prisonersHaveVarietyNeed = p.prisonersHaveVarietyNeed;
            ModSettings_VarietyMatters.slaveExpectedVarietyPercentage = p.slaveExpectedVarietyPercentage;
            ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage = p.prisonerExpectedVarietyPercentage;
            // Invalidate IntEntry buffers so the displayed numbers match the new values.
            _numIngredientsBuf = null;
            _cookingMemoryBuf = null;
            _floorBuf = null;
            _slaveBuf = null;
            _prisonerBuf = null;
            _exLowBuf = null; _vLowBuf = null; _lowBuf = null; _modBuf = null;
            _highBuf = null; _skyHiBuf = null; _nobleBuf = null;
            QueueRecalculationForGameplayAffectingChange();
        }

        private static bool MatchesProfile(Profile p)
        {
            return ModSettings_VarietyMatters.extremelyLowVariety == p.extremelyLowVariety
                && ModSettings_VarietyMatters.veryLowVariety == p.veryLowVariety
                && ModSettings_VarietyMatters.lowVariety == p.lowVariety
                && ModSettings_VarietyMatters.moderateVariety == p.moderateVariety
                && ModSettings_VarietyMatters.highVariety == p.highVariety
                && ModSettings_VarietyMatters.skyHighVariety == p.skyHighVariety
                && ModSettings_VarietyMatters.nobleVariety == p.nobleVariety
                && ModSettings_VarietyMatters.lowExpectationFloor == p.lowExpectationFloor
                && ModSettings_VarietyMatters.halveVarietyMoodImpact == p.halveVarietyMoodImpact
                && ModSettings_VarietyMatters.moreVarietyMemory == p.moreVarietyMemory
                && ModSettings_VarietyMatters.slavesHaveVarietyNeed == p.slavesHaveVarietyNeed
                && ModSettings_VarietyMatters.prisonersHaveVarietyNeed == p.prisonersHaveVarietyNeed
                && ModSettings_VarietyMatters.slaveExpectedVarietyPercentage == p.slaveExpectedVarietyPercentage
                && ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage == p.prisonerExpectedVarietyPercentage;
        }

        private static string DetectActivePreset()
        {
            if (MatchesProfile(Default_)) return "Default";
            if (MatchesProfile(Easy)) return "Easy";
            if (MatchesProfile(Hardcore)) return "Hardcore";
            return "Custom";
        }

        private static void QueueRecalculationForGameplayAffectingChange()
        {
            DebouncedSettingsHandler.QueueRecalculation();
        }

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

        private static Vector2 rightScrollPosition = Vector2.zero;
        private static Vector2 leftScrollPosition = Vector2.zero;

        // Measured content height of the left settings column. Refreshed each frame from the
        // real listing height. Seeded generously so the first frame after opening the settings
        // window doesn't clip if any section is already expanded. The per-toggle pre-inflate in
        // DrawSectionHeader handles later expansions.
        private static float _lastLeftContentHeight = 1500f;
    }
}
