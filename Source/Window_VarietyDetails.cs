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
        // `_tryFeedingDefs` — defs the pawn ate before the decay window but no longer in recent meals
        //   (stale-favourites heuristic; FoodSourceFactory.AllFoodDefs stockpile scan is out of scope).
        private readonly Dictionary<ThingDef, int> _recentIngCountsScratch = new Dictionary<ThingDef, int>();
        private readonly HashSet<ThingDef> _decayedIngSetScratch = new HashSet<ThingDef>();
        private readonly List<ThingDef> _tiredOfDefs = new List<ThingDef>();
        private readonly List<ThingDef> _tryFeedingDefs = new List<ThingDef>();
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

            // Aggregate stats panel.
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

            // Stats rows.
            int expected = VarietyExpectation.GetVarietyExpectation(this.pawn);
            int actual = 0;
            if (VarietyRecord.TryGetVarietyRecord(this.pawn, out DietTracker tracker) && tracker != null)
            {
                actual = tracker.TotalVariety;
            }
            int memCapacity = (int)(ModSettings_VarietyMatters.memoryMultiplier * expected);

            float statsY = inner.y + 34f;
            Rect statRect = new Rect(textX, statsY, textWidth, 24f);
            Widgets.Label(statRect, "VM_OverlayStats".Translate(expected, actual, memCapacity));

            Need_FoodVariety need = this.pawn.needs?.TryGetNeed<Need_FoodVariety>();
            if (need != null)
            {
                statRect.y += 24f;
                Widgets.Label(statRect, string.Format("{0} {1}", need.LabelCap, (need.CurLevelPercentage).ToStringPercent()));
            }

            // Favourites placeholder slot — reserved for §2a. Drawn as a dim "Favourites: —" line for now.
            statRect.y += 24f;
            GUI.color = new Color(1f, 1f, 1f, 0.45f);
            Widgets.Label(statRect, "VM_Favourites_Placeholder".Translate());
            GUI.color = Color.white;
        }

        private void DrawAggregate(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, SectionBg);
            Rect inner = rect.ContractedBy(Padding);
            float lineHeight = 26f;
            const float labelWidth = 220f;
            float y = inner.y;

            bool overlapMode = ModSettings_VarietyMatters.foodTrackingType == FoodTrackingType.ByIngredientOverlap;

            if (overlapMode)
            {
                Widgets.Label(new Rect(inner.x, y, labelWidth, lineHeight), "VM_Aggregate_TiredOf".Translate());
                Rect valueRect = new Rect(inner.x + labelWidth, y - 2f, inner.xMax - inner.x - labelWidth, IngredientIcon);
                DrawIngredientIconList(valueRect, this._tiredOfDefs);
                y += lineHeight + 4f;

                Widgets.Label(new Rect(inner.x, y, labelWidth, lineHeight), "VM_Aggregate_TryFeeding".Translate());
                valueRect = new Rect(inner.x + labelWidth, y - 2f, inner.xMax - inner.x - labelWidth, IngredientIcon);
                DrawIngredientIconList(valueRect, this._tryFeedingDefs);
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
            if (overlapMode)
            {
                VarietyRecord.TryGetVarietyRecord(this.pawn, out tracker);
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
                    this.DrawMealRow(new Rect(0f, y, viewRect.width, MealRowHeight), this._rows[i], overlapMode, tracker);
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

        private void DrawMealRow(Rect rect, Need_FoodVariety.MealRowInfo row, bool overlapMode, DietTracker tracker)
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

            // Overlap-mode score badge ("33%"). Right of the status+age block, before ingredient icons.
            if (overlapMode && !row.IsForgotten)
            {
                Rect badgeRect = new Rect(mealRect.xMax + Padding, statusRect.yMax + 22f, 60f, 22f);
                int pct = Math.Max(0, Math.Min(100, (int)Math.Round(row.VarietyScore * 100f)));
                GUI.color = ColorForScore(row.VarietyScore);
                Text.Font = GameFont.Small;
                Widgets.Label(badgeRect, "VM_Details_ScoreBadge".Translate(pct));
                GUI.color = Color.white;
                TooltipIfOver(badgeRect, "VM_Details_ScoreTooltip".Translate(pct));
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
            int pct = Math.Max(0, Math.Min(100, (int)Math.Round(row.VarietyScore * 100f)));
            StringBuilder sb = this._tooltipBuilder;
            sb.Clear();
            if (row.MealDef != null)
            {
                sb.AppendLine(row.MealDef.LabelCap);
            }
            sb.AppendLine("VM_Details_ScoreTooltip".Translate(pct));

            var (overlap, fresh) = tracker.GetOverlapBreakdown(row.Source);
            bool hasIngredients = (row.Ingredients != null && row.Ingredients.Count > 0);
            if (!hasIngredients)
            {
                sb.AppendLine();
                sb.Append("VM_Details_EmptyIngredientsFallback".Translate(row.MealDef?.LabelCap ?? ""));
                return sb.ToString();
            }

            tracker.GetMealsAgoForOverlapDefs(row.Source, overlap, this._mealsAgoScratch);

            sb.AppendLine();
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
            return sb.ToString();
        }

        private static string GetStatusLabel(Need_FoodVariety.MealRowInfo row)
        {
            switch (row.Status)
            {
                case Need_FoodVariety.MealRowStatus.HasVariety: return "VM_Status_HasVariety".Translate();
                case Need_FoodVariety.MealRowStatus.Forgotten: return "VM_Status_Forgotten".Translate();
                default: return "VM_Status_NoVariety".Translate();
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
                this._tryFeedingDefs.Clear();
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
            this._decayedIngSetScratch.Clear();
            this._tiredOfDefs.Clear();
            this._tryFeedingDefs.Clear();
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

            // Overlap-mode aggregates: split ingredients by decay window relative to current tick.
            // tiredOf = all defs present in any meal within the last `VarietyDecayTicks` (= current overlap set).
            // tryFeeding = defs the pawn ate outside the window but no longer inside it — stale favourites.
            int nowTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            foreach (var foodByPawn in tracker.EatenFoodSourcesByOrder)
            {
                EatenFoodSource src = foodByPawn.Source;
                if (src == null || src.IsForgotten) continue;
                int t = src.AteAtTick;
                bool recent = (t >= 0) && (nowTick - t <= DietTracker.VarietyDecayTicks);

                List<ThingDef> defs = src.IngredientsDefs;
                if (defs != null && defs.Count > 0)
                {
                    for (int idx = 0; idx < defs.Count; idx++)
                    {
                        ThingDef d = defs[idx];
                        if (d == null) continue;
                        if (recent)
                        {
                            this._recentIngCountsScratch.TryGetValue(d, out int c);
                            this._recentIngCountsScratch[d] = c + 1;
                        }
                        else
                        {
                            this._decayedIngSetScratch.Add(d);
                        }
                    }
                }
                else if (src.ThingDef != null)
                {
                    if (recent)
                    {
                        this._recentIngCountsScratch.TryGetValue(src.ThingDef, out int c);
                        this._recentIngCountsScratch[src.ThingDef] = c + 1;
                    }
                    else
                    {
                        this._decayedIngSetScratch.Add(src.ThingDef);
                    }
                }
            }
            foreach (var kv in this._recentIngCountsScratch)
            {
                this._tiredOfDefs.Add(kv.Key);
            }
            foreach (ThingDef d in this._decayedIngSetScratch)
            {
                if (!this._recentIngCountsScratch.ContainsKey(d)) this._tryFeedingDefs.Add(d);
            }

            this._lastAggregateVersion = version;
        }
    }
}
