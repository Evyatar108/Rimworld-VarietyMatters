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
        // US-028 / US-029 preset profile. Covers 21 gameplay-affecting fields a difficulty
        // preset is allowed to set: 7 memory values + 7 palate thresholds + 7 other toggles /
        // sliders. When adding a new gameplay-affecting setting to ModSettings, update ALL of:
        // this struct, the three Profile instances, ApplyProfile, MatchesProfile, and (if
        // defaults change) ModSettings field initializers. The `moreVarietyMemory` toggle is
        // DEPRECATED and no longer part of the profile.
        private struct Profile
        {
            public int extremelyLowMemory, veryLowMemory, lowMemory,
                       moderateMemory, highMemory, skyHighMemory, nobleMemory;
            public float extremelyLowPalate, veryLowPalate, lowPalate,
                         moderatePalate, highPalate, skyHighPalate, noblePalate;
            public int lowExpectationFloor;
            public int maxNegativeMood, maxPositiveMood;
            public int mealBonus1Slot, mealBonus2Slot, mealBonus3Slot, mealBonus4Slot, mealBonus5Slot;
            public bool slavesHaveVarietyNeed, prisonersHaveVarietyNeed;
            public int slaveExpectedVarietyPercentage, prisonerExpectedVarietyPercentage;
        }

        private static readonly Profile Easy = new Profile
        {
            extremelyLowMemory = 7, veryLowMemory = 9, lowMemory = 11,
            moderateMemory = 14, highMemory = 18, skyHighMemory = 22, nobleMemory = 27,
            extremelyLowPalate = 0.02f, veryLowPalate = 0.04f, lowPalate = 0.08f,
            moderatePalate = 0.14f, highPalate = 0.20f, skyHighPalate = 0.28f, noblePalate = 0.36f,
            lowExpectationFloor = 3,
            maxNegativeMood = -8, maxPositiveMood = 12,
            mealBonus1Slot = 0, mealBonus2Slot = 10, mealBonus3Slot = 20,
            mealBonus4Slot = 30, mealBonus5Slot = 40,
            slavesHaveVarietyNeed = true, prisonersHaveVarietyNeed = true,
            slaveExpectedVarietyPercentage = 50, prisonerExpectedVarietyPercentage = 40,
        };

        // Default_ MUST match ModSettings_VarietyMatters field initializers exactly. If a field
        // default changes in ModSettings, update the matching slot here or the "Active: Default"
        // highlight will be wrong for fresh installs.
        private static readonly Profile Default_ = new Profile
        {
            extremelyLowMemory = 9, veryLowMemory = 11, lowMemory = 14,
            moderateMemory = 18, highMemory = 23, skyHighMemory = 27, nobleMemory = 34,
            extremelyLowPalate = 0.04f, veryLowPalate = 0.08f, lowPalate = 0.12f,
            moderatePalate = 0.20f, highPalate = 0.28f, skyHighPalate = 0.36f, noblePalate = 0.48f,
            lowExpectationFloor = 3,
            maxNegativeMood = -12, maxPositiveMood = 12,
            mealBonus1Slot = 0, mealBonus2Slot = 5, mealBonus3Slot = 12,
            mealBonus4Slot = 20, mealBonus5Slot = 30,
            slavesHaveVarietyNeed = true, prisonersHaveVarietyNeed = true,
            slaveExpectedVarietyPercentage = 65, prisonerExpectedVarietyPercentage = 50,
        };

        private static readonly Profile Hardcore = new Profile
        {
            extremelyLowMemory = 11, veryLowMemory = 14, lowMemory = 18,
            moderateMemory = 23, highMemory = 27, skyHighMemory = 34, nobleMemory = 40,
            extremelyLowPalate = 0.06f, veryLowPalate = 0.12f, lowPalate = 0.18f,
            moderatePalate = 0.26f, highPalate = 0.34f, skyHighPalate = 0.42f, noblePalate = 0.50f,
            lowExpectationFloor = 4,
            maxNegativeMood = -14, maxPositiveMood = 12,
            mealBonus1Slot = 0, mealBonus2Slot = 3, mealBonus3Slot = 8,
            mealBonus4Slot = 14, mealBonus5Slot = 22,
            slavesHaveVarietyNeed = true, prisonersHaveVarietyNeed = true,
            slaveExpectedVarietyPercentage = 80, prisonerExpectedVarietyPercentage = 65,
        };

        // Transient UI state — not Scribed (per-session section expand/collapse).
        private static readonly Dictionary<string, bool> _sectionExpanded = new Dictionary<string, bool>
        {
            { "basics", false },
            { "difficulty", false },
            { "expectations", false },
            { "bonus", false },
            { "cook", false },
            { "scoring", false },
        };

        // IntEntry edit buffers. RimWorld's TextFieldNumeric needs stable ref string storage.
        // Nulled on preset apply / tier reset so next draw reseeds from the current value.
        // US-029: 7 memory + 7 palate buffers for per-tier direct-value tuning.
        private static string _numIngredientsBuf;
        private static string _cookingMemoryBuf;
        private static string _floorBuf;
        private static string _slaveBuf;
        private static string _prisonerBuf;
        private static string _exLowMemBuf, _vLowMemBuf, _lowMemBuf, _modMemBuf,
                              _highMemBuf, _skyHiMemBuf, _nobleMemBuf;
        private static string _exLowPalBuf, _vLowPalBuf, _lowPalBuf, _modPalBuf,
                              _highPalBuf, _skyHiPalBuf, _noblePalBuf;
        private static string _maxNegMoodBuf;
        private static string _maxPosMoodBuf;
        private static string _bonus1Buf, _bonus2Buf, _bonus3Buf, _bonus4Buf, _bonus5Buf;

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

            // Scroll viewport sized with a hard floor of 1500f so any single-section expansion
            // fits on the SAME frame the user clicks. _lastLeftContentHeight updates only after
            // End() runs — too late for the current frame's BeginScrollView. Using the measured
            // value alone clips the Pawn Expectations body (~280px) plus Cook + Scoring headers
            // below it until the next redraw, which reads as "content disappeared".
            float contentHeight1 = Mathf.Max(_lastLeftContentHeight, 1500f);
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
            if (DrawSectionHeader(listing_Standard, "bonus", "VM_Settings_Section_MealBonus"))
            {
                DrawMealBonusSection(listing_Standard);
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
            // Floor at 1500f keeps parity with the same-frame floor above — the scrollbar's
            // usable range won't shrink below the single-section expansion threshold.
            _lastLeftContentHeight = Mathf.Max(listing_Standard.CurHeight + 24f, 1500f);
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
            // US-031 direct mood-range endpoints. Two independent sliders replace the old
            // single % scalar so negative and positive mood swings can be tuned asymmetrically.
            int prevNeg = ModSettings_VarietyMatters.maxNegativeMood;
            if (_maxNegMoodBuf == null) _maxNegMoodBuf = prevNeg.ToString();
            Rect negRect = ls.GetRect(24f, 1f);
            this.LabeledIntEntry(negRect, "VM_Settings_MoodMaxNegLabel".Translate(),
                ref ModSettings_VarietyMatters.maxNegativeMood, ref _maxNegMoodBuf, 1, 2, -20, 0);
            TooltipHandler.TipRegion(negRect, "VM_Settings_MoodMaxNegTooltip".Translate());
            if (prevNeg != ModSettings_VarietyMatters.maxNegativeMood)
            {
                QueueRecalculationForGameplayAffectingChange();
            }

            int prevPos = ModSettings_VarietyMatters.maxPositiveMood;
            if (_maxPosMoodBuf == null) _maxPosMoodBuf = prevPos.ToString();
            Rect posRect = ls.GetRect(24f, 1f);
            this.LabeledIntEntry(posRect, "VM_Settings_MoodMaxPosLabel".Translate(),
                ref ModSettings_VarietyMatters.maxPositiveMood, ref _maxPosMoodBuf, 1, 2, 0, 20);
            TooltipHandler.TipRegion(posRect, "VM_Settings_MoodMaxPosTooltip".Translate());
            if (prevPos != ModSettings_VarietyMatters.maxPositiveMood)
            {
                QueueRecalculationForGameplayAffectingChange();
            }

            // US-029 dropped the moreVarietyMemory checkbox — player now sets per-tier memory
            // values directly in the Pawn Expectations section. The field stays Scribed in
            // ModSettings for save-compat but no UI reads/writes it.

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
            // Local reset button: resets tier memory + palate fields to Default_ profile values.
            // Does NOT reset Difficulty-section fields — those belong to the preset strip.
            Rect resetRow = ls.GetRect(26f, 1f);
            if (Widgets.ButtonText(new Rect(resetRow.xMax - 80f, resetRow.y, 72f, resetRow.height),
                "VM_Settings_ResetButton".Translate()))
            {
                ApplyTierFromProfile(Default_);
            }

            ls.Label("VM_Settings_ExpectationExplanation".Translate(), -1f);
            ls.Gap(4f);

            // 7 tiers × (memory + palate) = 14 rows. Grouped by tier.
            DrawTierBlock(ls, "VM_Settings_ExtremelyLowLabel",
                ref ModSettings_VarietyMatters.extremelyLowMemory, ref _exLowMemBuf,
                ref ModSettings_VarietyMatters.extremelyLowPalate, ref _exLowPalBuf);
            DrawTierBlock(ls, "VM_Settings_VeryLowLabel",
                ref ModSettings_VarietyMatters.veryLowMemory, ref _vLowMemBuf,
                ref ModSettings_VarietyMatters.veryLowPalate, ref _vLowPalBuf);
            DrawTierBlock(ls, "VM_Settings_LowLabel",
                ref ModSettings_VarietyMatters.lowMemory, ref _lowMemBuf,
                ref ModSettings_VarietyMatters.lowPalate, ref _lowPalBuf);
            DrawTierBlock(ls, "VM_Settings_ModerateLabel",
                ref ModSettings_VarietyMatters.moderateMemory, ref _modMemBuf,
                ref ModSettings_VarietyMatters.moderatePalate, ref _modPalBuf);
            DrawTierBlock(ls, "VM_Settings_HighLabel",
                ref ModSettings_VarietyMatters.highMemory, ref _highMemBuf,
                ref ModSettings_VarietyMatters.highPalate, ref _highPalBuf);
            DrawTierBlock(ls, "VM_Settings_SkyHighLabel",
                ref ModSettings_VarietyMatters.skyHighMemory, ref _skyHiMemBuf,
                ref ModSettings_VarietyMatters.skyHighPalate, ref _skyHiPalBuf);
            DrawTierBlock(ls, "VM_Settings_NobleLabel",
                ref ModSettings_VarietyMatters.nobleMemory, ref _nobleMemBuf,
                ref ModSettings_VarietyMatters.noblePalate, ref _noblePalBuf);

            // Monotonic cascade on memory values only (palate stays player-chosen — there's no
            // strict "higher tier must be pickier" invariant; unusual configs are fine).
            EnforceMemoryMonotonicity();

            ls.Gap(8f);
        }

        // One tier, two rows: memory IntEntry + palate IntEntry (as 0-50 percent). Label shows
        // tier name once above both to keep the visual pair legible.
        private void DrawTierBlock(Listing_Standard ls, string tierLabelKey,
            ref int memValue, ref string memBuf,
            ref float palateValue, ref string palBuf)
        {
            // Tier name header (non-editable, just a label row above the two sliders)
            Color prevColor = GUI.color;
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            ls.Label(tierLabelKey.Translate(), -1f);
            GUI.color = prevColor;

            // Memory row
            if (memBuf == null) memBuf = memValue.ToString();
            int prevMem = memValue;
            Rect memRect = ls.GetRect(24f, 1f);
            this.LabeledIntEntry(memRect,
                "VM_Settings_Tier_MemorySub".Translate(), ref memValue, ref memBuf, 1, 5, 1, 200);
            TooltipHandler.TipRegion(memRect, "VM_Settings_Tier_MemoryTooltip".Translate());
            if (prevMem != memValue)
            {
                QueueRecalculationForGameplayAffectingChange();
            }

            // Palate row — stored as float 0.0-0.50; displayed as int 0-50 percent.
            int palatePct = (int)System.Math.Round(palateValue * 100f);
            if (palBuf == null) palBuf = palatePct.ToString();
            int prevPct = palatePct;
            Rect palRect = ls.GetRect(24f, 1f);
            this.LabeledIntEntry(palRect,
                "VM_Settings_Tier_PalateSub".Translate(), ref palatePct, ref palBuf, 1, 5, 0, 50);
            TooltipHandler.TipRegion(palRect, "VM_Settings_Tier_PalateTooltip".Translate());
            if (prevPct != palatePct)
            {
                palateValue = Mathf.Clamp(palatePct / 100f, 0f, 0.50f);
                palBuf = null; // force reseed so UI stays in sync with clamped float
                QueueRecalculationForGameplayAffectingChange();
            }
            ls.Gap(4f);
        }

        private static void EnforceMemoryMonotonicity()
        {
            bool changed = false;
            if (ModSettings_VarietyMatters.veryLowMemory < ModSettings_VarietyMatters.extremelyLowMemory)
            { ModSettings_VarietyMatters.veryLowMemory = ModSettings_VarietyMatters.extremelyLowMemory; _vLowMemBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.lowMemory < ModSettings_VarietyMatters.veryLowMemory)
            { ModSettings_VarietyMatters.lowMemory = ModSettings_VarietyMatters.veryLowMemory; _lowMemBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.moderateMemory < ModSettings_VarietyMatters.lowMemory)
            { ModSettings_VarietyMatters.moderateMemory = ModSettings_VarietyMatters.lowMemory; _modMemBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.highMemory < ModSettings_VarietyMatters.moderateMemory)
            { ModSettings_VarietyMatters.highMemory = ModSettings_VarietyMatters.moderateMemory; _highMemBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.skyHighMemory < ModSettings_VarietyMatters.highMemory)
            { ModSettings_VarietyMatters.skyHighMemory = ModSettings_VarietyMatters.highMemory; _skyHiMemBuf = null; changed = true; }
            if (ModSettings_VarietyMatters.nobleMemory < ModSettings_VarietyMatters.skyHighMemory)
            { ModSettings_VarietyMatters.nobleMemory = ModSettings_VarietyMatters.skyHighMemory; _nobleMemBuf = null; changed = true; }
            if (changed)
            {
                QueueRecalculationForGameplayAffectingChange();
            }
        }

        // Writes the tier memory + palate fields from a profile (14 values). Used by the local
        // tier Reset button. Does NOT touch Difficulty fields (preset strip handles those).
        private static void ApplyTierFromProfile(Profile p)
        {
            ModSettings_VarietyMatters.extremelyLowMemory = p.extremelyLowMemory;
            ModSettings_VarietyMatters.veryLowMemory = p.veryLowMemory;
            ModSettings_VarietyMatters.lowMemory = p.lowMemory;
            ModSettings_VarietyMatters.moderateMemory = p.moderateMemory;
            ModSettings_VarietyMatters.highMemory = p.highMemory;
            ModSettings_VarietyMatters.skyHighMemory = p.skyHighMemory;
            ModSettings_VarietyMatters.nobleMemory = p.nobleMemory;
            ModSettings_VarietyMatters.extremelyLowPalate = p.extremelyLowPalate;
            ModSettings_VarietyMatters.veryLowPalate = p.veryLowPalate;
            ModSettings_VarietyMatters.lowPalate = p.lowPalate;
            ModSettings_VarietyMatters.moderatePalate = p.moderatePalate;
            ModSettings_VarietyMatters.highPalate = p.highPalate;
            ModSettings_VarietyMatters.skyHighPalate = p.skyHighPalate;
            ModSettings_VarietyMatters.noblePalate = p.noblePalate;
            _exLowMemBuf = _vLowMemBuf = _lowMemBuf = _modMemBuf = _highMemBuf = _skyHiMemBuf = _nobleMemBuf = null;
            _exLowPalBuf = _vLowPalBuf = _lowPalBuf = _modPalBuf = _highPalBuf = _skyHiPalBuf = _noblePalBuf = null;
            QueueRecalculationForGameplayAffectingChange();
        }

        private void DrawMealBonusSection(Listing_Standard ls)
        {
            ls.Label("VM_Settings_MealBonus_Body".Translate(), -1f);
            ls.Gap(4f);
            DrawBonusEntry(ls, "VM_Settings_MealBonus_1Slot",
                ref ModSettings_VarietyMatters.mealBonus1Slot, ref _bonus1Buf);
            DrawBonusEntry(ls, "VM_Settings_MealBonus_2Slot",
                ref ModSettings_VarietyMatters.mealBonus2Slot, ref _bonus2Buf);
            DrawBonusEntry(ls, "VM_Settings_MealBonus_3Slot",
                ref ModSettings_VarietyMatters.mealBonus3Slot, ref _bonus3Buf);
            DrawBonusEntry(ls, "VM_Settings_MealBonus_4Slot",
                ref ModSettings_VarietyMatters.mealBonus4Slot, ref _bonus4Buf);
            DrawBonusEntry(ls, "VM_Settings_MealBonus_5Slot",
                ref ModSettings_VarietyMatters.mealBonus5Slot, ref _bonus5Buf);
            ls.Gap(8f);
        }

        private void DrawBonusEntry(Listing_Standard ls, string labelKey, ref int value, ref string buf)
        {
            if (buf == null) buf = value.ToString();
            int prev = value;
            Rect r = ls.GetRect(24f, 1f);
            this.LabeledIntEntry(r, labelKey.Translate(), ref value, ref buf, 1, 5, 0, 100);
            if (prev != value)
            {
                // Meal-def bonus affects future ingest-time computations; no need to recalc
                // stored-mood. But for consistency with other live-visible settings, queue one
                // so the tooltip bonus preview updates immediately.
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
            // Tier memory + palate (14 fields)
            ModSettings_VarietyMatters.extremelyLowMemory = p.extremelyLowMemory;
            ModSettings_VarietyMatters.veryLowMemory = p.veryLowMemory;
            ModSettings_VarietyMatters.lowMemory = p.lowMemory;
            ModSettings_VarietyMatters.moderateMemory = p.moderateMemory;
            ModSettings_VarietyMatters.highMemory = p.highMemory;
            ModSettings_VarietyMatters.skyHighMemory = p.skyHighMemory;
            ModSettings_VarietyMatters.nobleMemory = p.nobleMemory;
            ModSettings_VarietyMatters.extremelyLowPalate = p.extremelyLowPalate;
            ModSettings_VarietyMatters.veryLowPalate = p.veryLowPalate;
            ModSettings_VarietyMatters.lowPalate = p.lowPalate;
            ModSettings_VarietyMatters.moderatePalate = p.moderatePalate;
            ModSettings_VarietyMatters.highPalate = p.highPalate;
            ModSettings_VarietyMatters.skyHighPalate = p.skyHighPalate;
            ModSettings_VarietyMatters.noblePalate = p.noblePalate;
            // Difficulty-section fields + US-031 mood-range endpoints + 5 bonus
            ModSettings_VarietyMatters.lowExpectationFloor = p.lowExpectationFloor;
            ModSettings_VarietyMatters.maxNegativeMood = p.maxNegativeMood;
            ModSettings_VarietyMatters.maxPositiveMood = p.maxPositiveMood;
            ModSettings_VarietyMatters.mealBonus1Slot = p.mealBonus1Slot;
            ModSettings_VarietyMatters.mealBonus2Slot = p.mealBonus2Slot;
            ModSettings_VarietyMatters.mealBonus3Slot = p.mealBonus3Slot;
            ModSettings_VarietyMatters.mealBonus4Slot = p.mealBonus4Slot;
            ModSettings_VarietyMatters.mealBonus5Slot = p.mealBonus5Slot;
            ModSettings_VarietyMatters.slavesHaveVarietyNeed = p.slavesHaveVarietyNeed;
            ModSettings_VarietyMatters.prisonersHaveVarietyNeed = p.prisonersHaveVarietyNeed;
            ModSettings_VarietyMatters.slaveExpectedVarietyPercentage = p.slaveExpectedVarietyPercentage;
            ModSettings_VarietyMatters.prisonerExpectedVarietyPercentage = p.prisonerExpectedVarietyPercentage;
            // Invalidate all IntEntry buffers so the displayed numbers match the new values.
            _numIngredientsBuf = null;
            _cookingMemoryBuf = null;
            _floorBuf = null;
            _slaveBuf = null;
            _prisonerBuf = null;
            _maxNegMoodBuf = null;
            _maxPosMoodBuf = null;
            _bonus1Buf = _bonus2Buf = _bonus3Buf = _bonus4Buf = _bonus5Buf = null;
            _exLowMemBuf = _vLowMemBuf = _lowMemBuf = _modMemBuf =
                _highMemBuf = _skyHiMemBuf = _nobleMemBuf = null;
            _exLowPalBuf = _vLowPalBuf = _lowPalBuf = _modPalBuf =
                _highPalBuf = _skyHiPalBuf = _noblePalBuf = null;
            QueueRecalculationForGameplayAffectingChange();
        }

        private static bool MatchesProfile(Profile p)
        {
            return ModSettings_VarietyMatters.extremelyLowMemory == p.extremelyLowMemory
                && ModSettings_VarietyMatters.veryLowMemory == p.veryLowMemory
                && ModSettings_VarietyMatters.lowMemory == p.lowMemory
                && ModSettings_VarietyMatters.moderateMemory == p.moderateMemory
                && ModSettings_VarietyMatters.highMemory == p.highMemory
                && ModSettings_VarietyMatters.skyHighMemory == p.skyHighMemory
                && ModSettings_VarietyMatters.nobleMemory == p.nobleMemory
                && Mathf.Approximately(ModSettings_VarietyMatters.extremelyLowPalate, p.extremelyLowPalate)
                && Mathf.Approximately(ModSettings_VarietyMatters.veryLowPalate, p.veryLowPalate)
                && Mathf.Approximately(ModSettings_VarietyMatters.lowPalate, p.lowPalate)
                && Mathf.Approximately(ModSettings_VarietyMatters.moderatePalate, p.moderatePalate)
                && Mathf.Approximately(ModSettings_VarietyMatters.highPalate, p.highPalate)
                && Mathf.Approximately(ModSettings_VarietyMatters.skyHighPalate, p.skyHighPalate)
                && Mathf.Approximately(ModSettings_VarietyMatters.noblePalate, p.noblePalate)
                && ModSettings_VarietyMatters.lowExpectationFloor == p.lowExpectationFloor
                && ModSettings_VarietyMatters.maxNegativeMood == p.maxNegativeMood
                && ModSettings_VarietyMatters.maxPositiveMood == p.maxPositiveMood
                && ModSettings_VarietyMatters.mealBonus1Slot == p.mealBonus1Slot
                && ModSettings_VarietyMatters.mealBonus2Slot == p.mealBonus2Slot
                && ModSettings_VarietyMatters.mealBonus3Slot == p.mealBonus3Slot
                && ModSettings_VarietyMatters.mealBonus4Slot == p.mealBonus4Slot
                && ModSettings_VarietyMatters.mealBonus5Slot == p.mealBonus5Slot
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
