namespace VarietyMatters
{
    using System;
    using System.Collections.Generic;
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
        private const float MealIconBig = 64f;
        private const float IngredientIcon = 28f;
        private const float IngredientGap = 4f;
        private const float StatusWidth = 110f;
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
        private ThingDef _repeatChampDef;
        private int _repeatChampCount;
        private ThingDef _mostVariedIngredientDef;
        private int _mostVariedIngredientCount;
        private int _forgottenCount;

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
                Widgets.Label(statRect, string.Format("{0} {1}", "Variety".Translate().CapitalizeFirst(), (need.CurLevelPercentage).ToStringPercent()));
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
            float y = inner.y;

            // Most repeated meal.
            Rect repeatRect = new Rect(inner.x, y, inner.width, lineHeight);
            Widgets.Label(new Rect(repeatRect.x, repeatRect.y, 220f, lineHeight), "VM_Aggregate_RepeatChamp".Translate());
            if (this._repeatChampDef != null && this._repeatChampCount >= 2)
            {
                Rect iconRect = new Rect(repeatRect.x + 220f, repeatRect.y - 2f, IngredientIcon, IngredientIcon);
                if (this._repeatChampDef.uiIcon != null)
                {
                    Widgets.DrawTextureFitted(iconRect, this._repeatChampDef.uiIcon, 1f);
                }
                TooltipIfOver(iconRect, this._repeatChampDef.LabelCap);
                Widgets.Label(new Rect(iconRect.xMax + 6f, repeatRect.y, inner.xMax - iconRect.xMax - 6f, lineHeight), "x" + this._repeatChampCount.ToString());
            }
            else
            {
                Widgets.Label(new Rect(repeatRect.x + 220f, repeatRect.y, inner.xMax - repeatRect.x - 220f, lineHeight), "—");
            }
            y += lineHeight + 4f;

            // Most-varied ingredient.
            Rect ingRect = new Rect(inner.x, y, inner.width, lineHeight);
            Widgets.Label(new Rect(ingRect.x, ingRect.y, 220f, lineHeight), "VM_Aggregate_MostVariedIngredient".Translate());
            if (this._mostVariedIngredientDef != null && this._mostVariedIngredientCount >= 2)
            {
                Rect iconRect = new Rect(ingRect.x + 220f, ingRect.y - 2f, IngredientIcon, IngredientIcon);
                if (this._mostVariedIngredientDef.uiIcon != null)
                {
                    Widgets.DrawTextureFitted(iconRect, this._mostVariedIngredientDef.uiIcon, 1f);
                }
                TooltipIfOver(iconRect, this._mostVariedIngredientDef.LabelCap);
                Widgets.Label(new Rect(iconRect.xMax + 6f, ingRect.y, inner.xMax - iconRect.xMax - 6f, lineHeight), "x" + this._mostVariedIngredientCount.ToString());
            }
            else
            {
                Widgets.Label(new Rect(ingRect.x + 220f, ingRect.y, inner.xMax - ingRect.x - 220f, lineHeight), "—");
            }
            y += lineHeight + 4f;

            // Forgotten.
            Rect fgRect = new Rect(inner.x, y, inner.width, lineHeight);
            Widgets.Label(fgRect, "VM_Aggregate_Forgotten".Translate(this._forgottenCount, this._rows.Count));
        }

        private void DrawMealList(Rect rect)
        {
            if (this._rows.Count == 0)
            {
                Widgets.Label(rect, "VM_DetailsWindow_NoMeals".Translate());
                return;
            }

            float contentHeight = this._rows.Count * (MealRowHeight + 4f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, contentHeight);
            Widgets.BeginScrollView(rect, ref this._scrollPosition, viewRect);
            float y = 0f;
            for (int i = 0; i < this._rows.Count; i++)
            {
                DrawMealRow(new Rect(0f, y, viewRect.width, MealRowHeight), this._rows[i]);
                y += MealRowHeight + 4f;
            }
            Widgets.EndScrollView();
        }

        private static void DrawMealRow(Rect rect, Need_FoodVariety.MealRowInfo row)
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
                Widgets.Label(mealInner, "?");
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
                TooltipIfOver(mealRect, row.MealDef.LabelCap);
            }

            // Variety status label (center).
            Rect statusRect = new Rect(mealRect.xMax + Padding, inner.y + 4f, StatusWidth, 24f);
            GUI.color = row.BorderColor;
            Widgets.Label(statusRect, GetStatusLabel(row));
            GUI.color = Color.white;

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
                this._repeatChampDef = null;
                this._repeatChampCount = 0;
                this._mostVariedIngredientDef = null;
                this._mostVariedIngredientCount = 0;
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

            // Find most-repeated meal.
            this._repeatChampDef = null;
            this._repeatChampCount = 0;
            foreach (var kv in this._repeatCounts)
            {
                if (kv.Value > this._repeatChampCount)
                {
                    this._repeatChampCount = kv.Value;
                    this._repeatChampDef = this._keyToDef[kv.Key];
                }
            }

            // Find most-used ingredient.
            this._mostVariedIngredientDef = null;
            this._mostVariedIngredientCount = 0;
            foreach (var kv in this._ingredientCounts)
            {
                if (kv.Value > this._mostVariedIngredientCount)
                {
                    this._mostVariedIngredientCount = kv.Value;
                    this._mostVariedIngredientDef = kv.Key;
                }
            }

            this._lastAggregateVersion = version;
        }
    }
}
