namespace VarietyMatters
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using RimWorld;
    using UnityEngine;
    using VarietyMatters.New;
    using Verse;

    public class Window_VarietyDetails : Window
    {
        // Layout constants
        private const float PortraitSize = 96f;
        private const float HeaderHeight = 120f;
        private const float AggregateHeight = 90f;
        private const float MealRowHeight = 76f;
        private const float ForgottenRowHeight = 38f;
        private const float MealIconBig = 64f;
        private const float ForgottenMiniIcon = 28f;
        private const float IngredientIcon = 28f;
        private const float IngredientGap = 4f;
        private const float StatusWidth = 220f;
        private const float SectionGap = 10f;
        private const float Padding = 10f;

        private static readonly Color HasVarietyColor = new Color(0.35f, 0.85f, 0.35f);
        private static readonly Color NoVarietyColor = new Color(0.85f, 0.35f, 0.35f);
        private static readonly Color ForgottenColor = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color BorderBg = new Color(0f, 0f, 0f, 0f);
        private static readonly Color SectionBg = new Color(0.13f, 0.13f, 0.13f, 0.5f);

        private readonly Pawn pawn;
        private Vector2 _scrollPosition;

        // Reusable scratch — class scope to avoid per-frame allocs even though the window isn't a hot path.
        private readonly List<Need_FoodVariety.MealRowInfo> _rows = new List<Need_FoodVariety.MealRowInfo>();
        private readonly Dictionary<string, int> _repeatCounts = new Dictionary<string, int>();
        private readonly Dictionary<ThingDef, int> _ingredientCounts = new Dictionary<ThingDef, int>();
        private readonly Dictionary<string, ThingDef> _keyToDef = new Dictionary<string, ThingDef>();

        // Aggregate stats cache — rebuilt on tracker version change only.
        private int _lastAggregateVersion = -1;
        private readonly List<ThingDef> _repeatChampDefs = new List<ThingDef>();
        private int _repeatChampCount;
        private readonly List<ThingDef> _mostVariedIngredientDefs = new List<ThingDef>();
        private int _mostVariedIngredientCount;
        private int _forgottenCount;
        // Overlap-mode aggregate scratch (per CLAUDE.md §1). Rebuilt with other aggregates.
        // `_tiredOfDefs` — ingredient defs appearing in the active decay window (the current overlap set).
        private readonly Dictionary<ThingDef, int> _recentIngCountsScratch = new Dictionary<ThingDef, int>();
        private readonly List<ThingDef> _tiredOfDefs = new List<ThingDef>();
        // Reused per-hover tooltip builder. Cleared at the top of `BuildOverlapTooltip`.
        private readonly StringBuilder _tooltipBuilder = new StringBuilder();
        // Scratch for per-ingredient "meals ago" lookup in BuildOverlapTooltip.
        // Consumer: BuildOverlapTooltip (via DietTracker.GetMealsAgoForOverlapDefs).
        private readonly Dictionary<ThingDef, int> _mealsAgoScratch = new Dictionary<ThingDef, int>();

        public override Vector2 InitialSize => new Vector2(640f, 620f);

        public Window_VarietyDetails(Pawn pawn)
        {
            this.pawn = pawn;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = true;
            this.closeOnClickedOutside = true;
            this.preventCameraMotion = false;
            this.forcePause = false;
            this.absorbInputAroundWindow = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Refresh scratch from the current tracker state.
            this.RebuildRows();

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            float y = inRect.y;

            // Header: portrait + pawn name + stats.
            Rect headerRect = new Rect(inRect.x, y, inRect.width, HeaderHeight);
            this.DrawHeader(headerRect);
            y += HeaderHeight + SectionGap;

            // Aggregate stats panel. Legacy mode fits 2 rows + Forgotten count in AggregateHeight.
            // Overlap mode fits a subheader + 1 row + Forgotten count in the same space — the
            // subheader displaces one of the rows, no extra height needed.
            Rect aggRect = new Rect(inRect.x, y, inRect.width, AggregateHeight);
            this.DrawAggregate(aggRect);
            y += AggregateHeight + SectionGap;

            // Scrollable meal list.
            Rect listRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            this.DrawMealList(listRect);
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, SectionBg);
            Rect inner = rect.ContractedBy(Padding);

            // Portrait.
            Rect portraitRect = new Rect(inner.x, inner.y, PortraitSize, PortraitSize);
            Widgets.DrawBoxSolidWithOutline(portraitRect, new Color(0f, 0f, 0f, 0.4f), new Color(0.2f, 0.2f, 0.2f), 1);
            Texture portrait = PortraitsCache.Get(this.pawn, new Vector2(PortraitSize - 2f, PortraitSize - 2f), Rot4.South, default, 1f);
            GUI.DrawTexture(portraitRect.ContractedBy(1f), portrait);

            // Text area to the right of portrait.
            float textX = portraitRect.xMax + Padding;
            float textWidth = inner.xMax - textX;
            Text.Font = GameFont.Medium;
            Rect nameRect = new Rect(textX, inner.y, textWidth, 32f);
            Widgets.Label(nameRect, this.pawn.LabelShortCap);
            Text.Font = GameFont.Small;

            // Stats rows. "Score" is the actual mood driver under ByIngredientOverlap mode
            // (sum of per-meal varietyScore over the memory window). Integer "Recent" key-count
            // is no longer displayed — it was orthogonal to the mood calc post-US-023.
            int expected = VarietyExpectation.GetVarietyExpectation(this.pawn);
            float score = 0f;
            if (VarietyRecord.TryGetVarietyRecord(this.pawn, out DietTracker tracker) && tracker != null)
            {
                score = tracker.TotalVarietyScore;
            }
            int memCapacity = (int)(ModSettings_VarietyMatters.memoryMultiplier * expected);

            float statsY = inner.y + 34f;
            Rect statRect = new Rect(textX, statsY, textWidth, 24f);
            Widgets.Label(statRect, "VM_OverlayStats".Translate(expected, score.ToString("F1"), memCapacity));

            Need_FoodVariety need = this.pawn.needs?.TryGetNeed<Need_FoodVariety>();
            if (need != null)
            {
                statRect.y += 24f;
                Widgets.Label(statRect, string.Format("{0} {1}", need.LabelCap, (need.CurLevelPercentage).ToStringPercent()));
            }

            // US-035 favourites row. Kill-switch hides the row entirely. Otherwise draws
            // "Favourites:" + per-slot uiIcon + ingredient label (inline). Empty state shows a
            // dim "—" so the player sees the feature is active but no favourite has crystallized yet.
            if (ModSettings_VarietyMatters.favouritesEnabled)
            {
                statRect.y += 24f;
                this.DrawFavouritesRow(statRect);
            }
        }

        private void DrawFavouritesRow(Rect rect)
        {
            // Mirror Need_FoodVariety gating: if the pawn's variety need is disabled
            // (Ascetic trait, VM_NoVarietyNeed gene, slave/prisoner/faction/race), hide
            // favourites UI. Prevents stale data from being shown after mid-save trait/gene
            // changes even though the underlying record is preserved on disk.
            Need_FoodVariety need = this.pawn.needs?.TryGetNeed<Need_FoodVariety>();
            if (need != null && need.GetDisabledReason() != DisabledReason.None)
            {
                return;
            }

            FavouriteFoodRecord comp = Current.Game?.GetComponent<FavouriteFoodRecord>();
            PawnFavData data = comp?.TryGet(this.pawn);

            const float iconSize = 18f;
            const float gapSmall = 4f;
            const float gapSlot = 10f;

            string prefix = "VM_Fav_LinePrefix".Translate();
            float labelW = Text.CalcSize(prefix).x;
            Widgets.Label(new Rect(rect.x, rect.y, labelW, rect.height), prefix);
            float x = rect.x + labelW + gapSmall;

            bool drewAny = false;
            if (data != null)
            {
                DrawFavSlotInline(ref x, rect.y, rect.xMax, iconSize, gapSmall, gapSlot, data.meatSlot, ref drewAny);
                DrawFavSlotInline(ref x, rect.y, rect.xMax, iconSize, gapSmall, gapSlot, data.vegSlot, ref drewAny);
                DrawFavSlotInline(ref x, rect.y, rect.xMax, iconSize, gapSmall, gapSlot, data.wildSlot, ref drewAny);
            }
            if (!drewAny)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                Widgets.Label(new Rect(x, rect.y, rect.xMax - x, rect.height), "VM_Fav_None".Translate());
                GUI.color = Color.white;
            }
        }

        private static void DrawFavSlotInline(ref float x, float y, float xMax,
            float iconSize, float gapSmall, float gapSlot, SlotState slot, ref bool drewAny)
        {
            // Returns void so the caller never confuses "overflow skip" with "drew content".
            // `drewAny` flips true only when we actually rendered the slot's icon+label.
            if (slot?.favourite == null) return;
            if (x + iconSize > xMax) return;
            Rect iconRect = new Rect(x, y + (24f - iconSize) * 0.5f, iconSize, iconSize);
            if (slot.favourite.uiIcon != null)
            {
                Widgets.DrawTextureFitted(iconRect, slot.favourite.uiIcon, 1f);
            }
            TooltipIfOver(iconRect, slot.favourite.LabelCap);
            x += iconSize + gapSmall;
            string label = slot.favourite.LabelCap;
            float labelW = Mathf.Min(Text.CalcSize(label).x, Mathf.Max(0f, xMax - x));
            Widgets.Label(new Rect(x, y, labelW, 24f), label);
            x += labelW + gapSlot;
            drewAny = true;
        }

        private void DrawAggregate(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, SectionBg);
            Rect inner = rect.ContractedBy(Padding);
            float lineHeight = 26f;
            const float labelWidth = 270f;
            float y = inner.y;

            bool overlapMode = ModSettings_VarietyMatters.foodTrackingType == FoodTrackingType.ByIngredientOverlap;

            if (overlapMode)
            {
                // Count-based memory subheader — queue size is per-tier (memoryMultiplier ×
                // varietyExpectation). Shows the actual meal count currently held so "Recently
                // consumed ingredients" reads against a concrete scope.
                int memCapacity = (int)(ModSettings_VarietyMatters.memoryMultiplier
                    * VarietyExpectation.GetVarietyExpectation(this.pawn));
                Widgets.Label(new Rect(inner.x, y, inner.xMax - inner.x, lineHeight),
                    "VM_Aggregate_MemoryMeals".Translate(memCapacity));
                y += lineHeight + 2f;

                Widgets.Label(new Rect(inner.x, y, labelWidth, lineHeight), "VM_Aggregate_TiredOf".Translate());
                Rect valueRect = new Rect(inner.x + labelWidth, y - 2f, inner.xMax - inner.x - labelWidth, IngredientIcon);
                DrawIngredientIconList(valueRect, this._tiredOfDefs);
                y += lineHeight + 4f;
            }
            else
            {
                Widgets.Label(new Rect(inner.x, y, labelWidth, lineHeight), "VM_Aggregate_RepeatChamp".Translate());
                Rect valueRect = new Rect(inner.x + labelWidth, y - 2f, inner.xMax - inner.x - labelWidth, IngredientIcon);
                DrawAggregateIcons(valueRect, this._repeatChampDefs, this._repeatChampCount);
                y += lineHeight + 4f;

                Widgets.Label(new Rect(inner.x, y, labelWidth, lineHeight), "VM_Aggregate_MostVariedIngredient".Translate());
                valueRect = new Rect(inner.x + labelWidth, y - 2f, inner.xMax - inner.x - labelWidth, IngredientIcon);
                DrawAggregateIcons(valueRect, this._mostVariedIngredientDefs, this._mostVariedIngredientCount);
                y += lineHeight + 4f;
            }

            // Forgotten count.
            Rect fgRect = new Rect(inner.x, y, inner.width, lineHeight);
            Widgets.Label(fgRect, "VM_Aggregate_Forgotten".Translate(this._forgottenCount, this._rows.Count));
        }

        // Plain icon list (no trailing "×N"). Used for overlap-mode tired-of / try-feeding.
        private static void DrawIngredientIconList(Rect rect, List<ThingDef> defs)
        {
            if (defs == null || defs.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rect, "VM_Aggregate_None".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            float x = rect.x;
            float iy = rect.y;
            int maxFit = Math.Max(1, (int)((rect.width + IngredientGap) / (IngredientIcon + IngredientGap)));
            int drawn = Math.Min(defs.Count, maxFit);
            for (int i = 0; i < drawn; i++)
            {
                ThingDef def = defs[i];
                if (def == null) continue;
                Rect iconRect = new Rect(x, iy, IngredientIcon, IngredientIcon);
                if (def.uiIcon != null)
                {
                    Widgets.DrawTextureFitted(iconRect, def.uiIcon, 1f);
                }
                TooltipIfOver(iconRect, def.LabelCap);
                x += IngredientIcon + IngredientGap;
            }
            if (defs.Count > drawn)
            {
                Rect moreRect = new Rect(x, iy, IngredientIcon, IngredientIcon);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                Widgets.Label(moreRect, "+" + (defs.Count - drawn));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private static void DrawAggregateIcons(Rect rect, List<ThingDef> defs, int count)
        {
            if (defs == null || defs.Count == 0 || count < 2)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rect, "—");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            float x = rect.x;
            float iy = rect.y;
            int maxFit = Math.Max(1, (int)((rect.width + IngredientGap) / (IngredientIcon + IngredientGap)));
            // Reserve space for the trailing "×N" label (approx 40f).
            maxFit = Math.Max(1, maxFit - 1);
            int drawn = Math.Min(defs.Count, maxFit);
            for (int i = 0; i < drawn; i++)
            {
                ThingDef def = defs[i];
                if (def == null) continue;
                Rect iconRect = new Rect(x, iy, IngredientIcon, IngredientIcon);
                if (def.uiIcon != null)
                {
                    Widgets.DrawTextureFitted(iconRect, def.uiIcon, 1f);
                }
                TooltipIfOver(iconRect, def.LabelCap);
                x += IngredientIcon + IngredientGap;
            }
            // Overflow indicator if ties exceed row width.
            if (defs.Count > drawn)
            {
                Rect moreRect = new Rect(x, iy, IngredientIcon, IngredientIcon);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                Widgets.Label(moreRect, "+" + (defs.Count - drawn));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                x += IngredientIcon + IngredientGap;
            }
            Rect countRect = new Rect(x + 4f, iy + 2f, rect.xMax - x, rect.height);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(countRect, "×" + count.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawMealList(Rect rect)
        {
            if (this._rows.Count == 0)
            {
                Widgets.Label(rect, "VM_DetailsWindow_NoMeals".Translate());
                return;
            }

            // Pre-compute total content height, collapsing consecutive forgotten rows into one compact row.
            float contentHeight = 0f;
            int i = 0;
            while (i < this._rows.Count)
            {
                if (this._rows[i].IsForgotten)
                {
                    int j = i;
                    while (j < this._rows.Count && this._rows[j].IsForgotten) j++;
                    contentHeight += ForgottenRowHeight + 4f;
                    i = j;
                }
                else
                {
                    contentHeight += MealRowHeight + 4f;
                    i++;
                }
            }

            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, contentHeight);
            Widgets.BeginScrollView(rect, ref this._scrollPosition, viewRect);
            float y = 0f;
            i = 0;
            bool overlapMode = ModSettings_VarietyMatters.foodTrackingType == FoodTrackingType.ByIngredientOverlap;
            DietTracker tracker = null;
            float pawnBaseline = 0f;
            if (overlapMode)
            {
                VarietyRecord.TryGetVarietyRecord(this.pawn, out tracker);
                // Compute the pawn's palate baseline once per frame so each meal-row badge can
                // display the *effective* mood contribution (post-curve + post-bonus) instead of
                // the raw stored ingredient score — they diverge for high-tier pawns.
                pawnBaseline = VarietyAdjuster.BaselineForTierPublic(this.pawn);
            }
            while (i < this._rows.Count)
            {
                if (this._rows[i].IsForgotten)
                {
                    int j = i;
                    while (j < this._rows.Count && this._rows[j].IsForgotten) j++;
                    int count = j - i;
                    DrawForgottenGroup(new Rect(0f, y, viewRect.width, ForgottenRowHeight), count);
                    y += ForgottenRowHeight + 4f;
                    i = j;
                }
                else
                {
                    this.DrawMealRow(new Rect(0f, y, viewRect.width, MealRowHeight), this._rows[i], overlapMode, tracker, pawnBaseline);
                    y += MealRowHeight + 4f;
                    i++;
                }
            }
            Widgets.EndScrollView();
        }

        private static void DrawForgottenGroup(Rect rect, int count)
        {
            Widgets.DrawBoxSolid(rect, SectionBg);
            Rect inner = rect.ContractedBy(6f);
            Rect iconRect = new Rect(inner.x, inner.y + (inner.height - ForgottenMiniIcon) * 0.5f, ForgottenMiniIcon, ForgottenMiniIcon);
            Widgets.DrawBoxSolidWithOutline(iconRect, BorderBg, ForgottenColor, 1);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ForgottenColor;
            Widgets.Label(iconRect, "?");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipIfOver(iconRect, "VM_ForgottenMealTip".Translate());

            Rect labelRect = new Rect(iconRect.xMax + Padding, inner.y, inner.xMax - iconRect.xMax - Padding, inner.height);
            GUI.color = ForgottenColor;
            Text.Anchor = TextAnchor.MiddleLeft;
            string label = count == 1
                ? "VM_Status_Forgotten".Translate().ToString()
                : "VM_Status_ForgottenCount".Translate(count).ToString();
            Widgets.Label(labelRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void DrawMealRow(Rect rect, Need_FoodVariety.MealRowInfo row, bool overlapMode, DietTracker tracker, float pawnBaseline)
        {
            Widgets.DrawBoxSolid(rect, SectionBg);
            Rect inner = rect.ContractedBy(6f);

            // Meal icon (left).
            Rect mealRect = new Rect(inner.x, inner.y, MealIconBig, MealIconBig);
            Widgets.DrawBoxSolidWithOutline(mealRect, BorderBg, row.BorderColor, 2);
            Rect mealInner = mealRect.ContractedBy(3f);
            if (row.IsForgotten)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Medium;
                GUI.color = row.BorderColor;
                Widgets.Label(mealInner, "VM_Overlap_Glyph_Forgotten".Translate());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipIfOver(mealRect, "VM_ForgottenMealTip".Translate());
            }
            else if (row.MealDef != null)
            {
                if (row.MealDef.uiIcon != null)
                {
                    Widgets.DrawTextureFitted(mealInner, row.MealDef.uiIcon, 1f);
                }
                Need_FoodVariety.DrawStatusGlyph(mealRect, row.Status, row.BorderColor);
                if (overlapMode && tracker != null && row.Source != null && Mouse.IsOver(mealRect))
                {
                    TooltipHandler.TipRegion(mealRect, this.BuildOverlapTooltip(row, tracker));
                }
                else
                {
                    TooltipIfOver(mealRect, row.MealDef.LabelCap);
                }
            }

            // Variety status label + meal name (center). Wider so long meal labels fit on one line.
            Rect statusRect = new Rect(mealRect.xMax + Padding, inner.y + 4f, StatusWidth, 24f);
            GUI.color = row.BorderColor;
            string statusText = GetStatusLabel(row);
            if (!row.IsForgotten && row.MealDef != null)
            {
                statusText = statusText + " · " + row.MealDef.LabelCap;
            }
            Widgets.Label(statusRect, statusText.Truncate(statusRect.width));
            GUI.color = Color.white;

            // Age indicator below status. Greyed out. Plural-aware.
            if (!row.IsForgotten && row.AteAtTick >= 0)
            {
                Rect ageRect = new Rect(mealRect.xMax + Padding, statusRect.yMax + 2f, StatusWidth, 20f);
                GUI.color = new Color(1f, 1f, 1f, 0.55f);
                Widgets.Label(ageRect, FormatAteAgo(row.AteAtTick));
                GUI.color = Color.white;
            }

            // Overlap-mode score badge. Shows the *effective* mood contribution — raw ingredient
            // score pushed through the pawn's tier baseline curve plus the post-curve meal-def
            // bonus — which is what the pawn actually feels. Raw score alone (ingredient Jaccard)
            // misrepresents reality for high-tier pawns (noble at raw 0.60 may only contribute
            // 0.15 to mood). Suppressed for NoVariety rows since those don't contribute at all
            // (status label already says "(raw food)" etc.).
            if (overlapMode && !row.IsForgotten && row.Status == Need_FoodVariety.MealRowStatus.HasVariety)
            {
                bool mealDefFresh = tracker != null
                    && row.MealDef != null
                    && tracker.IsMealDefFreshInWindow(row.Source);
                int complexity = VarietyAdjuster.GetRecipeSlotComplexity(row.MealDef, row.Ingredients);
                float effective = VarietyAdjuster.ComputeEffectiveScore(
                    row.VarietyScore, pawnBaseline, mealDefFresh, complexity);
                Rect badgeRect = new Rect(mealRect.xMax + Padding, statusRect.yMax + 22f, 60f, 22f);
                int pct = Math.Max(0, Math.Min(100, (int)Math.Round(effective * 100f)));
                GUI.color = ColorForScore(effective);
                Text.Font = GameFont.Small;
                Widgets.Label(badgeRect, "VM_Details_ScoreBadge".Translate(pct));
                GUI.color = Color.white;
                TooltipIfOver(badgeRect, "VM_Details_ScoreTooltipNoIngredients".Translate(pct));
            }

            // Ingredient icons (right of status). Filling row from status.xMax to inner.xMax.
            float ingStartX = statusRect.xMax + Padding;
            float ingY = inner.y + (MealIconBig - IngredientIcon) * 0.5f;
            float availWidth = inner.xMax - ingStartX;
            int maxSlots = Math.Max(0, (int)((availWidth + IngredientGap) / (IngredientIcon + IngredientGap)));
            if (!row.IsForgotten && row.Ingredients != null && row.Ingredients.Count > 0)
            {
                int count = Math.Min(row.Ingredients.Count, maxSlots);
                float x = ingStartX;
                for (int i = 0; i < count; i++)
                {
                    ThingDef ing = row.Ingredients[i];
                    if (ing == null) { x += IngredientIcon + IngredientGap; continue; }
                    Rect ingRect = new Rect(x, ingY, IngredientIcon, IngredientIcon);
                    if (ing.uiIcon != null)
                    {
                        Widgets.DrawTextureFitted(ingRect, ing.uiIcon, 1f);
                    }
                    TooltipIfOver(ingRect, ing.LabelCap);
                    x += IngredientIcon + IngredientGap;
                }
            }
        }

        private static string FormatAteAgo(int ateAtTick)
        {
            if (ateAtTick < 0) return "VM_MealAgo_Unknown".Translate();
            int nowTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            int diff = Math.Max(0, nowTick - ateAtTick);
            // RimWorld in-game time: 2500 ticks/hour, 60000 ticks/day.
            if (diff < 2500)
            {
                int m = Math.Max(1, diff / 60);
                return m == 1 ? (string)"VM_MealAgo_Minute_One".Translate() : "VM_MealAgo_Minutes_Many".Translate(m);
            }
            if (diff < 60000)
            {
                int h = Math.Max(1, diff / 2500);
                return h == 1 ? (string)"VM_MealAgo_Hour_One".Translate() : "VM_MealAgo_Hours_Many".Translate(h);
            }
            int d = Math.Max(1, diff / 60000);
            return d == 1 ? (string)"VM_MealAgo_Day_One".Translate() : "VM_MealAgo_Days_Many".Translate(d);
        }

        private static Color ColorForScore(float score)
        {
            // Green high -> red low, interp through yellow. Grayscale-distinguishable via luminance.
            float s = Mathf.Clamp01(score);
            return Color.Lerp(new Color(0.85f, 0.35f, 0.35f), new Color(0.35f, 0.85f, 0.35f), s);
        }

        private string BuildOverlapTooltip(Need_FoodVariety.MealRowInfo row, DietTracker tracker)
        {
            // Under US-026 semantic: row.VarietyScore = ingredient-only Jaccard (0-1). The
            // pawn's actual mood contribution is ComputeEffectiveScore(score, baseline, fresh,
            // complexity) — includes soft-floor, baseline curve, and post-curve bonus. Show both
            // so the tooltip matches reality: "ingredient X%, plus meal-type bonus Y%, effective
            // mood contribution Z%".
            int ingredientPct = Math.Max(0, Math.Min(100, (int)Math.Round(row.VarietyScore * 100f)));
            StringBuilder sb = this._tooltipBuilder;
            sb.Clear();
            if (row.MealDef != null)
            {
                sb.AppendLine(row.MealDef.LabelCap);
            }

            var (overlap, fresh) = tracker.GetOverlapBreakdown(row.Source);
            bool hasIngredients = (row.Ingredients != null && row.Ingredients.Count > 0);
            if (!hasIngredients)
            {
                sb.AppendLine("VM_Details_ScoreTooltipNoIngredients".Translate(ingredientPct));
                sb.AppendLine();
                sb.Append("VM_Details_EmptyIngredientsFallback".Translate(row.MealDef?.LabelCap ?? ""));
                return sb.ToString();
            }

            int totalIngredients = overlap.Count + fresh.Count;
            int freshCount = fresh.Count;

            // Only HasVariety rows earn the meal-def bonus + contribute to mood. NoVariety rows
            // (raw, kibble, insect, etc.) are filtered upstream in VarietyAdjuster.
            bool rowCountsAsVariety = row.Status == Need_FoodVariety.MealRowStatus.HasVariety;
            bool mealDefFresh = rowCountsAsVariety
                && row.MealDef != null
                && tracker.IsMealDefFreshInWindow(row.Source);
            int mealDefBonusPct = 0;
            int complexity = 1;
            if (rowCountsAsVariety)
            {
                complexity = VarietyAdjuster.GetRecipeSlotComplexity(row.MealDef, row.Ingredients);
                if (mealDefFresh)
                {
                    mealDefBonusPct = (int)Math.Round(DietTracker.GetMealDefBonus(complexity) * 100f);
                }
            }

            // Effective contribution = soft-floor / baseline-curve of ingredient score + post-
            // curve bonus, clamped. This is what the pawn's mood actually receives from this
            // meal. Computed identically for both bonus-on and bonus-off rows so the displayed
            // number always matches the mood aggregation math in VarietyAdjuster.
            float pawnBaseline = VarietyAdjuster.BaselineForTierPublic(this.pawn);
            float effective = rowCountsAsVariety
                ? VarietyAdjuster.ComputeEffectiveScore(row.VarietyScore, pawnBaseline, mealDefFresh, complexity)
                : 0f;
            int effectivePct = Math.Max(0, Math.Min(100, (int)Math.Round(effective * 100f)));

            if (mealDefBonusPct > 0)
            {
                sb.AppendLine("VM_Details_ScoreTooltipWithBonus".Translate(effectivePct, ingredientPct, mealDefBonusPct));
            }
            else
            {
                sb.AppendLine("VM_Details_ScoreTooltip".Translate(effectivePct, ingredientPct, freshCount, totalIngredients));
            }

            tracker.GetMealsAgoForOverlapDefs(row.Source, overlap, this._mealsAgoScratch);

            sb.AppendLine();
            sb.AppendLine("VM_Overlap_IngredientsHeader".Translate());
            foreach (ThingDef d in overlap)
            {
                if (d == null) continue;
                this._mealsAgoScratch.TryGetValue(d, out int dist);
                string agoStr = dist == 1
                    ? (string)"VM_Overlap_MealsAgo_One".Translate()
                    : "VM_Overlap_MealsAgo_Many".Translate(dist > 0 ? dist : 1);
                sb.AppendLine("VM_Overlap_Repeated".Translate(d.LabelCap, agoStr));
            }
            foreach (ThingDef d in fresh)
            {
                if (d == null) continue;
                sb.AppendLine("VM_Overlap_New".Translate(d.LabelCap));
            }

            // US-025 meal-def bonus line. When bonus applied, show complexity-scaled % with a
            // "(capped)" note if we hit MealDefBonusCap. When the meal def was repeated, emit a
            // muted "no bonus" line so the player knows the system considered it. Stored
            // Under US-026 semantic, row.VarietyScore is ingredient-only; the bonus lives in the
            // effective-score term added at mood-tick. These tooltip lines are purely informational.
            if (row.MealDef != null && mealDefFresh)
            {
                float bonus = DietTracker.GetMealDefBonus(complexity);
                // "Capped" note only when modded recipes exceed the top of the step table
                // (DietTracker.MaxBonusSlots). 5-slot lavish sits exactly at the max without
                // being "clamped" — don't show capped for the canonical lavish case.
                bool capped = complexity > DietTracker.MaxBonusSlots;
                int bonusPct = (int)Math.Round(bonus * 100f);
                sb.AppendLine();
                string key = capped ? "VM_Overlap_MealDefNewBonusCapped" : "VM_Overlap_MealDefNewBonus";
                sb.Append(key.Translate(bonusPct, complexity));
            }
            else if (row.MealDef != null && rowCountsAsVariety)
            {
                // Meal type was in the recent-meals window — no bonus, but show the line so the
                // player knows the system considered it (otherwise absence looks like a bug).
                // Skipped for NoVariety rows: the meal doesn't contribute to mood at all, so a
                // "no bonus" note would be redundant alongside the reason label already shown.
                sb.AppendLine();
                sb.Append("VM_Overlap_MealDefRepeated".Translate());
            }

            return sb.ToString();
        }

        private static string GetStatusLabel(Need_FoodVariety.MealRowInfo row)
        {
            switch (row.Status)
            {
                case Need_FoodVariety.MealRowStatus.HasVariety: return "VM_Status_HasVariety".Translate();
                case Need_FoodVariety.MealRowStatus.Forgotten: return "VM_Status_Forgotten".Translate();
                default:
                    // Show the specific reason inline when the tracker has one (raw meat, insect,
                    // etc.). Falls through to plain "- Variety" when no reason is attached (e.g.
                    // repeat of a meal key that already registered).
                    string reasonLabel = GetNoVarietyReasonLabel(row.NoVarietyReason);
                    return reasonLabel != null
                        ? (string)"VM_Status_NoVarietyWithReason".Translate(reasonLabel)
                        : (string)"VM_Status_NoVariety".Translate();
            }
        }

        private static string GetNoVarietyReasonLabel(NoVarietyReason? reason)
        {
            if (reason == null || reason == NoVarietyReason.NA) return null;
            switch (reason.Value)
            {
                case NoVarietyReason.Rotten:                      return "VM_NoVarietyReason_Rotten".Translate();
                case NoVarietyReason.RawOrRawlikeFood:            return "VM_NoVarietyReason_Raw".Translate();
                case NoVarietyReason.InsectMeat:                  return "VM_NoVarietyReason_Insect".Translate();
                case NoVarietyReason.HumanLikeMeat:               return "VM_NoVarietyReason_Humanlike".Translate();
                case NoVarietyReason.Fungus:                      return "VM_NoVarietyReason_Fungus".Translate();
                case NoVarietyReason.HasChemicals:                return "VM_NoVarietyReason_Chemical".Translate();
                case NoVarietyReason.UnacceptableByVegetarians:   return "VM_NoVarietyReason_Vegetarian".Translate();
                case NoVarietyReason.UnacceptableByCarnivores:    return "VM_NoVarietyReason_Carnivore".Translate();
                case NoVarietyReason.IsOrHasVeneratedAnimalMeat:  return "VM_NoVarietyReason_Venerated".Translate();
                case NoVarietyReason.DisgustingMeal:              return "VM_NoVarietyReason_Disgusting".Translate();
                default: return null;
            }
        }

        private static void TooltipIfOver(Rect rect, string tip)
        {
            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, tip);
            }
        }

        private void RebuildRows()
        {
            // Pull rows from the Need's own scratch (keeps the variety-status computation in one place).
            Need_FoodVariety need = this.pawn.needs?.TryGetNeed<Need_FoodVariety>();
            if (need == null)
            {
                this._rows.Clear();
                return;
            }
            need.ProvideMealRowsSnapshot(this._rows);
            this.RebuildAggregatesIfNeeded();
        }

        private void RebuildAggregatesIfNeeded()
        {
            if (!VarietyRecord.TryGetVarietyRecord(this.pawn, out DietTracker tracker) || tracker == null)
            {
                this._repeatChampDefs.Clear();
                this._repeatChampCount = 0;
                this._mostVariedIngredientDefs.Clear();
                this._mostVariedIngredientCount = 0;
                this._tiredOfDefs.Clear();
                this._forgottenCount = 0;
                return;
            }
            int version = tracker.Version;
            if (version == this._lastAggregateVersion)
            {
                return;
            }

            this._repeatCounts.Clear();
            this._ingredientCounts.Clear();
            this._keyToDef.Clear();
            this._recentIngCountsScratch.Clear();
            this._tiredOfDefs.Clear();
            this._forgottenCount = 0;

            for (int i = 0; i < this._rows.Count; i++)
            {
                Need_FoodVariety.MealRowInfo r = this._rows[i];
                if (r.IsForgotten || r.MealDef == null)
                {
                    this._forgottenCount++;
                    continue;
                }
                string key = r.MealDef.defName;
                this._repeatCounts.TryGetValue(key, out int repCount);
                this._repeatCounts[key] = repCount + 1;
                this._keyToDef[key] = r.MealDef;

                if (r.Ingredients == null) continue;
                for (int j = 0; j < r.Ingredients.Count; j++)
                {
                    ThingDef ing = r.Ingredients[j];
                    if (ing == null) continue;
                    this._ingredientCounts.TryGetValue(ing, out int ingCount);
                    this._ingredientCounts[ing] = ingCount + 1;
                }
            }

            // Find max, then collect ALL defs tied at that count.
            this._repeatChampDefs.Clear();
            this._repeatChampCount = 0;
            foreach (var kv in this._repeatCounts)
            {
                if (kv.Value > this._repeatChampCount) this._repeatChampCount = kv.Value;
            }
            if (this._repeatChampCount >= 2)
            {
                foreach (var kv in this._repeatCounts)
                {
                    if (kv.Value == this._repeatChampCount)
                    {
                        this._repeatChampDefs.Add(this._keyToDef[kv.Key]);
                    }
                }
            }

            this._mostVariedIngredientDefs.Clear();
            this._mostVariedIngredientCount = 0;
            foreach (var kv in this._ingredientCounts)
            {
                if (kv.Value > this._mostVariedIngredientCount) this._mostVariedIngredientCount = kv.Value;
            }
            if (this._mostVariedIngredientCount >= 2)
            {
                foreach (var kv in this._ingredientCounts)
                {
                    if (kv.Value == this._mostVariedIngredientCount)
                    {
                        this._mostVariedIngredientDefs.Add(kv.Key);
                    }
                }
            }

            // Overlap-mode aggregate: defs present in any meal in the count-bounded memory queue
            // (= current overlap set). Drives the "Recently consumed ingredients:" row.
            foreach (var foodByPawn in tracker.EatenFoodSourcesByOrder)
            {
                EatenFoodSource src = foodByPawn.Source;
                if (src == null || src.IsForgotten) continue;

                List<ThingDef> defs = src.IngredientsDefs;
                if (defs != null && defs.Count > 0)
                {
                    for (int idx = 0; idx < defs.Count; idx++)
                    {
                        ThingDef d = defs[idx];
                        if (d == null) continue;
                        this._recentIngCountsScratch.TryGetValue(d, out int c);
                        this._recentIngCountsScratch[d] = c + 1;
                    }
                }
                else if (src.ThingDef != null)
                {
                    this._recentIngCountsScratch.TryGetValue(src.ThingDef, out int c);
                    this._recentIngCountsScratch[src.ThingDef] = c + 1;
                }
            }
            foreach (var kv in this._recentIngCountsScratch)
            {
                this._tiredOfDefs.Add(kv.Key);
            }

            this._lastAggregateVersion = version;
        }
    }
}
