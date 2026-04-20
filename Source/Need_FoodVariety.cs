namespace VarietyMatters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using RimWorld;
    using UnityEngine;
    using VarietyMatters.New;
    using Verse;
    using static VarietyMatters.New.DietTracker;

    // Token: 0x0200000C RID: 12
    public class Need_FoodVariety : Need
	{
		private int _lastTipVersion = -1;
		private bool _lastTipClusterSimilarMeals;
		private FoodTrackingType _lastTipFoodTrackingType;
		private string _cachedFoodSourcesDesc;
		private readonly List<(EatenFoodSource, FoodVarietyInfo, NoVarietyReason?)> _foodsVarietyInfoScratch = new List<(EatenFoodSource, FoodVarietyInfo, NoVarietyReason?)>();
		private readonly Dictionary<int, int> _totalAppearancesScratch = new Dictionary<int, int>();
		private readonly Dictionary<int, int> _appearancesSoFarScratch = new Dictionary<int, int>();
		private readonly StringBuilder _foodSourcesDescBuilder = new StringBuilder();

		private static HashSet<string> _enabledRaceKeys = new HashSet<string>();
		private static int _raceKeysVersion = -1;

		// Hover-overlay scratch (US-019 / F-019).
		// _mealRowsScratch: per-meal info for the hover overlay. Rebuilt only on (DietTracker.Version, memoryMultiplier) change.
		// _appearancesSoFarRowsScratch: per-meal-key appearances-so-far counter, reused each rebuild.
		private readonly List<MealRowInfo> _mealRowsScratch = new List<MealRowInfo>();
		private readonly Dictionary<string, int> _appearancesSoFarRowsScratch = new Dictionary<string, int>();
		private int _lastIconsVersion = -1;
		private float _lastIconsMemoryMult = -1f;
		private int _iconOverlayHash;
		private Action _drawHoverOverlayAction;

		private float _overlayWidth = 320f;
		private float _overlayHeight = 120f;
		private const float OverlayPadding = 6f;
		private const float MealIconSize = 32f;
		private const float IngredientIconSize = 20f;
		private const float RowHeight = 32f;
		private const float RowGap = 4f;
		private const float IngredientGap = 2f;
		private const float MealIngredientGap = 6f;

		public static readonly Color HasVarietyBorder = new Color(0.35f, 0.85f, 0.35f);
		public static readonly Color NoVarietyBorder = new Color(0.85f, 0.35f, 0.35f);
		public static readonly Color ForgottenBorder = new Color(0.55f, 0.55f, 0.55f);
		private static readonly Color RowBgTransparent = new Color(0f, 0f, 0f, 0f);

		public enum MealRowStatus { HasVariety, NoVariety, Forgotten }

		public struct MealRowInfo
		{
			public ThingDef MealDef;            // null for forgotten
			public List<ThingDef> Ingredients;  // null/empty for forgotten
			public MealRowStatus Status;
			public int AteAtTick;               // -1 for forgotten / old saves
			// Live tracker ref for overlap-breakdown tooltip. Null for forgotten rows and
			// stale once DietTracker._version changes -- UX must rebuild on version bump.
			public EatenFoodSource Source;
			// Per-meal Jaccard score in [0,1]. 1f for non-overlap modes / forgotten (default).
			public float VarietyScore;
			// Only populated when Status == NoVariety and the tracker has an explicit reason
			// (raw-meat eaten, insect-meat, rotten, venerated, etc. — see VarietyProvider).
			// Consumers can show this inline so players know WHY a meal didn't count.
			public NoVarietyReason? NoVarietyReason;
			public bool IsForgotten => this.Status == MealRowStatus.Forgotten;
			public Color BorderColor
			{
				get
				{
					switch (this.Status)
					{
						case MealRowStatus.HasVariety: return HasVarietyBorder;
						case MealRowStatus.Forgotten: return ForgottenBorder;
						default: return NoVarietyBorder;
					}
				}
			}
		}

		public void ProvideMealRowsSnapshot(List<MealRowInfo> dest)
		{
			this.RebuildMealRowsIfNeeded();
			dest.Clear();
			for (int i = 0; i < this._mealRowsScratch.Count; i++)
			{
				dest.Add(this._mealRowsScratch[i]);
			}
		}

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000018 RID: 24 RVA: 0x00002F8C File Offset: 0x0000118C
		public MenuVarietyLevel CurCategory
        {
            get
            {
                if (this.Disabled || this.IsFrozen)
                {
                    return MenuVarietyLevel.NA;
                }

                var levelByPercentage = this.CurLevel * 2;

                if (levelByPercentage <= 0.1538f) // 15.38% or less
                {
                    return MenuVarietyLevel.Barren;
                }
                if (levelByPercentage <= 0.3077f) // Up to 30.77%
                {
                    return MenuVarietyLevel.Empty;
                }
                if (levelByPercentage <= 0.4615f) // Up to 46.15%
                {
                    return MenuVarietyLevel.Poor;
                }
                if (levelByPercentage <= 0.6154f) // Up to 61.54%
                {
                    return MenuVarietyLevel.Scarce;
                }
                if (levelByPercentage <= 0.7692f) // Up to 76.92%
                {
                    return MenuVarietyLevel.Limited;
                }
                if (levelByPercentage <= 0.9231f) // Up to 92.31%
                {
                    return MenuVarietyLevel.BelowAverage;
                }
                if (levelByPercentage <= 1.0769f) // Up to 107.69%
                {
                    return MenuVarietyLevel.Average;
                }
                if (levelByPercentage <= 1.2308f) // Up to 123.08%
                {
                    return MenuVarietyLevel.AboveAverage;
                }
                if (levelByPercentage <= 1.3846f) // Up to 138.46%
                {
                    return MenuVarietyLevel.Good;
                }
                if (levelByPercentage <= 1.5385f) // Up to 153.85%
                {
                    return MenuVarietyLevel.Great;
                }
                if (levelByPercentage <= 1.6923f) // Up to 169.23%
                {
                    return MenuVarietyLevel.Excellent;
                }
                if (levelByPercentage <= 1.8462f) // Up to 184.62%
                {
                    return MenuVarietyLevel.Exceptional;
                }

                // Above 184.62%
                return MenuVarietyLevel.Overwhelming;
            }
        }

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000019 RID: 25 RVA: 0x0000303D File Offset: 0x0000123D
		public override int GUIChangeArrow
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x0600001A RID: 26 RVA: 0x00003040 File Offset: 0x00001240
		public override bool ShowOnNeedList
		{
			get
			{
				return true;
			}
		}

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x0600001B RID: 27 RVA: 0x0000304C File Offset: 0x0000124C
		public bool Disabled => GetDisabledReason() != DisabledReason.None;

		public DisabledReason GetDisabledReason()
		{
			if (this.pawn.Dead)
			{
				return DisabledReason.DeadOrMissingFaculties;
			}
			if (this.pawn.needs?.food == null || this.pawn.needs?.mood == null)
			{
				return DisabledReason.DeadOrMissingFaculties;
			}

			if (!ModSettings_VarietyMatters.slavesHaveVarietyNeed && this.pawn.IsSlave)
			{
				return DisabledReason.SlaveNotTracked;
			}

			if (!ModSettings_VarietyMatters.prisonersHaveVarietyNeed && this.pawn.IsPrisoner)
			{
				return DisabledReason.PrisonerNotTracked;
			}

			if (!this.pawn.Faction?.IsPlayer == true && !ModSettings_VarietyMatters.nonControlledPawnsHaveVarietyNeed)
			{
				return DisabledReason.NonPlayerNotTracked;
			}

			if (!TraitDef_ModExtension.NeedsVariety(this.pawn))
			{
				return DisabledReason.TraitDisabled;
			}

			int version = ModSettings_VarietyMatters.raceVarietyVersion;
			if (version != _raceKeysVersion)
			{
				_enabledRaceKeys.Clear();
				var raceVariety = ModSettings_VarietyMatters.raceVariety;
				if (raceVariety != null)
				{
					foreach (var kv in raceVariety)
					{
						if (kv.Value != null && kv.Value.isVarietyEnabled)
						{
							_enabledRaceKeys.Add(kv.Key);
						}
					}
				}
				_raceKeysVersion = version;
			}

			string raceKey = ModSettings_VarietyMatters.GetRaceKey(this.pawn.def);
			if (!_enabledRaceKeys.Contains(raceKey))
			{
				return DisabledReason.RaceNotTracked;
			}

			return DisabledReason.None;
		}

		private static string GetDisabledReasonLabel(DisabledReason reason)
		{
			switch (reason)
			{
				case DisabledReason.DeadOrMissingFaculties: return "VM_DisabledReason_DeadOrMissingFaculties".Translate();
				case DisabledReason.SlaveNotTracked: return "VM_DisabledReason_SlaveNotTracked".Translate();
				case DisabledReason.PrisonerNotTracked: return "VM_DisabledReason_PrisonerNotTracked".Translate();
				case DisabledReason.NonPlayerNotTracked: return "VM_DisabledReason_NonPlayerNotTracked".Translate();
				case DisabledReason.TraitDisabled: return "VM_DisabledReason_TraitDisabled".Translate();
				case DisabledReason.RaceNotTracked: return "VM_DisabledReason_RaceNotTracked".Translate();
				default: return "VM_DisabledReason_Unknown".Translate();
			}
		}

		// Token: 0x0600001C RID: 28 RVA: 0x000030EC File Offset: 0x000012EC
		public Need_FoodVariety(Pawn pawn) : base(pawn)
		{
            this.threshPercents = new List<float>
            {
                0.0833f,
                0.1666f,
                0.2500f,
                0.3333f,
                0.4166f,
                0.5000f,
                0.5833f,
                0.6666f,
                0.7500f,
                0.8333f,
                0.9166f
            };
        }

		// Token: 0x0600001D RID: 29 RVA: 0x00003151 File Offset: 0x00001351
		public override void SetInitialLevel()
		{
			this.CurLevel = 0.5f;
		}

		// Token: 0x0600001E RID: 30 RVA: 0x00003160 File Offset: 0x00001360
		public override void NeedInterval()
		{
			if (this.Disabled)
			{
				this.CurLevel = 0.5f;
                return;
			}
		}

		public override void DrawOnGUI(Rect rect, int maxThresholdMarkers = int.MaxValue, float customMargin = -1f, bool drawArrows = true, bool doTooltip = true, Rect? rectForTooltip = null, bool drawLabel = true)
		{
			base.DrawOnGUI(rect, maxThresholdMarkers, customMargin, drawArrows, false, rectForTooltip, drawLabel);

			// Info button (top-right of bar) — click to open details window. Placed inside rect so it doesn't
			// collide with adjacent need rows (NeedsCardUtility.DoNeeds has no vertical slack).
			Rect infoRect = new Rect(rect.xMax - 20f, rect.y + 2f, 16f, 16f);
			if (infoRect.width > 0f && infoRect.height > 0f && rect.width >= 140f)
			{
				if (Widgets.ButtonImage(infoRect, TexButton.Info))
				{
					if (!Find.WindowStack.IsOpen<Window_VarietyDetails>())
					{
						Find.WindowStack.Add(new Window_VarietyDetails(this.pawn));
					}
				}
				if (Mouse.IsOver(infoRect))
				{
					TooltipHandler.TipRegion(infoRect, "VM_Details_OpenTooltip".Translate());
				}
			}

			Rect tipRect = rectForTooltip ?? rect;
			// Suppress overlay when the cursor is over the info button to avoid overlap / double-hover.
			if (!doTooltip || !Mouse.IsOver(tipRect) || Mouse.IsOver(infoRect))
			{
				return;
			}

			if (this._iconOverlayHash == 0)
			{
				this._iconOverlayHash = unchecked((this.pawn.thingIDNumber * 397) ^ "variety_icons".GetHashCode());
				if (this._iconOverlayHash == 0) this._iconOverlayHash = 1;
			}
			if (this._drawHoverOverlayAction == null)
			{
				this._drawHoverOverlayAction = this.DrawHoverOverlay;
			}

			this._overlayWidth = Math.Min(320f, UI.screenWidth * 0.3f);

			// Size overlay from measured content: header text + per-meal icon rows + padding.
			Text.Font = GameFont.Small;
			float innerWidth = this._overlayWidth - 2f * OverlayPadding;
			string header = this.BuildOverlayHeader();
			float headerHeight = Text.CalcHeight(header, innerWidth);

			RebuildMealRowsIfNeeded();
			float rowsHeight = this._mealRowsScratch.Count * (RowHeight + RowGap);
			if (this._mealRowsScratch.Count > 0)
			{
				rowsHeight += 4f;   // small gap between header and first row
			}

			this._overlayHeight = 2f * OverlayPadding + headerHeight + rowsHeight;

			Vector2 mouse = Event.current.mousePosition;
			Rect overlayRect = new Rect(mouse.x + 12f, mouse.y + 12f, this._overlayWidth, this._overlayHeight);
			if (overlayRect.xMax > UI.screenWidth) overlayRect.x = UI.screenWidth - overlayRect.width;
			if (overlayRect.yMax > UI.screenHeight) overlayRect.y = UI.screenHeight - overlayRect.height;
			if (overlayRect.x < 0f) overlayRect.x = 0f;
			if (overlayRect.y < 0f) overlayRect.y = 0f;

			Find.WindowStack.ImmediateWindow(this._iconOverlayHash, overlayRect, WindowLayer.Super, this._drawHoverOverlayAction, doBackground: true);
		}

		private string BuildOverlayHeader()
		{
			// For disabled pawns, keep the full GetTipString (includes "Disabled: <reason>" prepend).
			// For active pawns, use the base Need tip (percentage + description) and leave per-meal narrative to the icon rows.
			DisabledReason reason = this.GetDisabledReason();
			if (reason != DisabledReason.None)
			{
				return this.GetTipString();
			}
			string baseTip = base.GetTipString();
			int expected = VarietyExpectation.GetVarietyExpectation(this.pawn);
			int actual = 0;
			if (VarietyRecord.TryGetVarietyRecord(this.pawn, out DietTracker tracker) && tracker != null)
			{
				actual = tracker.TotalVariety;
			}
			int memCapacity = (int)(ModSettings_VarietyMatters.memoryMultiplier * expected);
			string stats = "VM_OverlayStats".Translate(expected, actual, memCapacity);
			return baseTip + "\n\n" + stats;
		}

		private void RebuildMealRowsIfNeeded()
		{
			if (!VarietyRecord.TryGetVarietyRecord(this.pawn, out DietTracker tracker) || tracker == null)
			{
				this._mealRowsScratch.Clear();
				this._lastIconsVersion = -1;
				this._lastIconsMemoryMult = -1f;
				return;
			}
			int version = tracker.Version;
			float memMult = ModSettings_VarietyMatters.memoryMultiplier;
			if (version == this._lastIconsVersion && memMult == this._lastIconsMemoryMult && this._mealRowsScratch.Count > 0)
			{
				return;
			}

			this._mealRowsScratch.Clear();
			this._appearancesSoFarRowsScratch.Clear();

			foreach (var tuple in tracker.GetFoodVarietyInfoForPawnInIngestionOrder())
			{
				EatenFoodSource src = tuple.Item1;
				FoodVarietyInfo info = tuple.Item2;
				NoVarietyReason? noReason = tuple.Item3;

				MealRowInfo row = default;
				if (src == null || src.IsForgotten || src.ThingDef == null)
				{
					row.MealDef = null;
					row.Ingredients = null;
					row.Status = MealRowStatus.Forgotten;
					row.AteAtTick = -1;
					row.Source = null;
					row.VarietyScore = 1f;
					this._mealRowsScratch.Add(row);
					continue;
				}

				row.MealDef = src.ThingDef;
				row.Ingredients = src.IngredientsDefs;
				row.AteAtTick = src.AteAtTick;
				row.Source = src;
				row.VarietyScore = src.VarietyScore;

				string key = src.GetFoodSourceKey();
				this._appearancesSoFarRowsScratch.TryGetValue(key, out int seenSoFar);
				this._appearancesSoFarRowsScratch[key] = seenSoFar + 1;

				// Has-variety = first appearance in iteration AND there's a FoodVarietyInfo entry AND no explicit no-reason.
				bool hasVariety = info != null && noReason == null && seenSoFar == 0;
				row.Status = hasVariety ? MealRowStatus.HasVariety : MealRowStatus.NoVariety;
				row.NoVarietyReason = noReason;

				this._mealRowsScratch.Add(row);
			}

			// Display newest-first: forward iteration above was needed to compute has-variety status
			// (first chronological occurrence = variety). Reverse after so the overlay reads
			// most-recent → oldest, matching the text GetTipString convention.
			this._mealRowsScratch.Reverse();

			this._lastIconsVersion = version;
			this._lastIconsMemoryMult = memMult;
		}

		private void DrawHoverOverlay()
		{
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			Rect inner = new Rect(OverlayPadding, OverlayPadding, this._overlayWidth - 2f * OverlayPadding, this._overlayHeight - 2f * OverlayPadding);

			string header = this.BuildOverlayHeader();
			float headerHeight = Text.CalcHeight(header, inner.width);
			Rect headerRect = new Rect(inner.x, inner.y, inner.width, headerHeight);
			Widgets.Label(headerRect, header);

			if (this._mealRowsScratch.Count == 0)
			{
				return;
			}

			float y = inner.y + headerHeight + 4f;
			for (int i = 0; i < this._mealRowsScratch.Count; i++)
			{
				DrawMealRow(new Rect(inner.x, y, inner.width, RowHeight), this._mealRowsScratch[i]);
				y += RowHeight + RowGap;
			}
		}

		// Shape-distinguishable status glyph. Remains legible in grayscale (colorblind-safe).
		public static void DrawStatusGlyph(Rect mealSlot, MealRowStatus status, Color color)
		{
			string glyph;
			switch (status)
			{
				case MealRowStatus.HasVariety: glyph = "VM_Overlap_Glyph_Fresh".Translate(); break;
				case MealRowStatus.Forgotten: glyph = "VM_Overlap_Glyph_Forgotten".Translate(); break;
				default: glyph = "VM_Overlap_Glyph_Repeat".Translate(); break;
			}
			Rect badge = new Rect(mealSlot.x + 1f, mealSlot.y + 1f, 11f, 11f);
			Widgets.DrawBoxSolid(badge, new Color(0f, 0f, 0f, 0.55f));
			Text.Anchor = TextAnchor.MiddleCenter;
			GameFont prev = Text.Font;
			Text.Font = GameFont.Tiny;
			GUI.color = color;
			Widgets.Label(badge, glyph);
			GUI.color = Color.white;
			Text.Font = prev;
			Text.Anchor = TextAnchor.UpperLeft;
		}

		private static void DrawMealRow(Rect rect, MealRowInfo row)
		{
			// Meal icon slot (left).
			Rect mealSlot = new Rect(rect.x, rect.y, MealIconSize, MealIconSize);
			Widgets.DrawBoxSolidWithOutline(mealSlot, RowBgTransparent, row.BorderColor, 2);
			Rect mealInner = mealSlot.ContractedBy(3f);
			if (row.IsForgotten)
			{
				Text.Anchor = TextAnchor.MiddleCenter;
				GUI.color = row.BorderColor;
				Widgets.Label(mealInner, "VM_Overlap_Glyph_Forgotten".Translate());
				GUI.color = Color.white;
				Text.Anchor = TextAnchor.UpperLeft;
				if (Mouse.IsOver(mealSlot))
				{
					TooltipHandler.TipRegion(mealSlot, "VM_ForgottenMealTip".Translate());
				}
				return;
			}
			if (row.MealDef != null && row.MealDef.uiIcon != null)
			{
				Widgets.DrawTextureFitted(mealInner, row.MealDef.uiIcon, 1f);
			}
			// Shape glyph badge (top-left corner) — colorblind-distinguishable: ◆ fresh vs ■ repeat.
			DrawStatusGlyph(mealSlot, row.Status, row.BorderColor);
			if (Mouse.IsOver(mealSlot) && row.MealDef != null)
			{
				TooltipHandler.TipRegion(mealSlot, row.MealDef.LabelCap);
			}

			// Ingredient icons (right).
			if (row.Ingredients == null || row.Ingredients.Count == 0)
			{
				return;
			}
			float x = rect.x + MealIconSize + MealIngredientGap;
			float iy = rect.y + (MealIconSize - IngredientIconSize) * 0.5f;
			float availWidth = rect.xMax - x;
			int maxSlots = Math.Max(0, (int)((availWidth + IngredientGap) / (IngredientIconSize + IngredientGap)));
			int count = Math.Min(row.Ingredients.Count, maxSlots);
			for (int i = 0; i < count; i++)
			{
				ThingDef ing = row.Ingredients[i];
				if (ing == null) continue;
				Rect ingRect = new Rect(x, iy, IngredientIconSize, IngredientIconSize);
				if (ing.uiIcon != null)
				{
					Widgets.DrawTextureFitted(ingRect, ing.uiIcon, 1f);
				}
				if (Mouse.IsOver(ingRect))
				{
					TooltipHandler.TipRegion(ingRect, ing.LabelCap);
				}
				x += IngredientIconSize + IngredientGap;
			}
		}

		// Token: 0x0600001F RID: 31 RVA: 0x00003188 File Offset: 0x00001388
		public override string GetTipString()
		{
			string tipString = base.GetTipString();

			DisabledReason disabledReason = GetDisabledReason();
			if (disabledReason != DisabledReason.None)
			{
				return "VM_DisabledPrefix".Translate() + GetDisabledReasonLabel(disabledReason) + "\n\n" + tipString;
			}

            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(this.pawn);
            string varietesCountExpectedText = varietyExpectation.ToString();
			string actualVarietiesCountText = "0";
			string mostRecentFoodSourceText = "VM_FoodVarietyTip_NoMemory".Translate();
			if (VarietyRecord.TryGetVarietyRecord(this.pawn, out DietTracker dietTracker)
				&& dietTracker != null
				&& dietTracker.MostRecentEatenFoodSource != null)
			{
				actualVarietiesCountText = dietTracker.TotalVariety.ToString();
                mostRecentFoodSourceText = this.GetEatenFoodSourcesDescription(dietTracker);
            }

            float memoryMult = ModSettings_VarietyMatters.memoryMultiplier;

            string mealMemoryString = ((int)(memoryMult * varietyExpectation)).ToString();

            return tipString + "VM_FoodVarietyTip_Main".Translate(
                varietesCountExpectedText,
                actualVarietiesCountText,
                mealMemoryString,
                mostRecentFoodSourceText);
		}

        private int GetEatenFoodSourceCountKey(EatenFoodSource eatenFoodSource, FoodVarietyInfo foodVarietyInfo)
        {
            return (eatenFoodSource.GetFoodSourceKey(), foodVarietyInfo != null ? 1 : 0).GetHashCode();
        }

        private string GetEatenFoodSourcesDescription(DietTracker dietTracker)
        {
            int version = dietTracker.Version;
            bool clusterSimilarMeals = ModSettings_VarietyMatters.clusterSimilarMealsTogether;
            FoodTrackingType foodTrackingType = ModSettings_VarietyMatters.foodTrackingType;
            if (version == this._lastTipVersion
                && clusterSimilarMeals == this._lastTipClusterSimilarMeals
                && foodTrackingType == this._lastTipFoodTrackingType
                && this._cachedFoodSourcesDesc != null)
            {
                return this._cachedFoodSourcesDesc;
            }

            this._foodsVarietyInfoScratch.Clear();
            this._totalAppearancesScratch.Clear();
            this._appearancesSoFarScratch.Clear();

            foreach (var tuple in dietTracker.GetFoodVarietyInfoForPawnInIngestionOrder())
            {
                this._foodsVarietyInfoScratch.Add(tuple);
            }

            var foodsVarietyInfoInIngestionOrder = this._foodsVarietyInfoScratch;
            var totalAppearancesOfFoodSources = this._totalAppearancesScratch;

            foreach (var (eatenFoodSource, foodVarietyInfo, _) in foodsVarietyInfoInIngestionOrder)
            {
                var key = GetEatenFoodSourceCountKey(eatenFoodSource, foodVarietyInfo);
                if (!totalAppearancesOfFoodSources.ContainsKey(key))
                {
                    totalAppearancesOfFoodSources[key] = 0;
                }
                totalAppearancesOfFoodSources[key]++;
            }

            var stringBuilder = this._foodSourcesDescBuilder;
            stringBuilder.Clear();
            stringBuilder.Append("VM_FoodVarietyTip_MostRecent".Translate());
            var appearancesOfFoodSourcesSoFar = this._appearancesSoFarScratch;
            int maxViewableRecentMeals = 5;

            for (int i = foodsVarietyInfoInIngestionOrder.Count - 1; i >= 0 && (foodsVarietyInfoInIngestionOrder.Count - 1 - i) < maxViewableRecentMeals; i--)
            {
                var (eatenFoodSource, foodVarietyInfo, noVarietyReason) = foodsVarietyInfoInIngestionOrder[i];

                if (foodVarietyInfo == null && noVarietyReason == null)
                {
                    Log.Warning("Found eaten food source with null variety info and null no variety reason");
                    continue;
                }

                stringBuilder.AppendLine();

                string foodKey = eatenFoodSource.GetFoodSourceKey();
                int countKey = GetEatenFoodSourceCountKey(eatenFoodSource, foodVarietyInfo);

                if (!appearancesOfFoodSourcesSoFar.ContainsKey(countKey))
                {
                    appearancesOfFoodSourcesSoFar[countKey] = 0;
                }
                appearancesOfFoodSourcesSoFar[countKey]++;

                int timesAlreadyIngestedSoFar = appearancesOfFoodSourcesSoFar[countKey];
                string foodInfo = GenText.CapitalizeFirst(eatenFoodSource.ToString());
                int seed = (foodKey, timesAlreadyIngestedSoFar).GetHashCode();

                string varietyInfo = foodVarietyInfo != null
                    ? (eatenFoodSource.IsForgotten ? GetVarietyInfoForForgotten(seed) : GetVarietyInfoBasedOnIngestionCount(timesAlreadyIngestedSoFar, seed))
                    : GetVarietyInfoBasedOnNoVarietyReason(noVarietyReason.Value, seed);

                stringBuilder.Append(" ◊  ").Append(foodInfo);
                stringBuilder.AppendLine();
                stringBuilder.Append("✧ ").AppendLine(varietyInfo);

                if (foodVarietyInfo != null && !eatenFoodSource.IsForgotten && foodVarietyInfo.CountInMemory > 1)
                {
                    stringBuilder.Append("☆ ").Append("VM_FoodVarietyTip_AppearancePrefix".Translate()).Append(timesAlreadyIngestedSoFar).Append("VM_FoodVarietyTip_AppearanceOutOf".Translate()).Append(foodVarietyInfo.CountInMemory);

                    // Single tracking mode — the mode-label suffix is the same regardless of the
                    // cluster-similar-meals toggle (it describes the scoring method, not the UI
                    // grouping).
                    stringBuilder.Append("VM_FoodVarietyTip_ByIngredientOverlap".Translate());
                    stringBuilder.AppendLine();
                }
            }

            this._cachedFoodSourcesDesc = stringBuilder.ToString();
            this._lastTipVersion = version;
            this._lastTipClusterSimilarMeals = clusterSimilarMeals;
            this._lastTipFoodTrackingType = foodTrackingType;
            return this._cachedFoodSourcesDesc;
        }

        private string GetVarietyInfoForForgotten(int seed)
        {
            return this.RandChooseForgottenFoodStrings(seed);
        }

        private string GetVarietyInfoBasedOnIngestionCount(int timesAlreadyIngestedSoFar, int seed)
        {
            if (timesAlreadyIngestedSoFar > 1)
            {
                return this.RandChooseNoVarietyStrings(seed);
            }
            else
            {
                return this.RandChooseHasVarietyStrings(seed);
            }
        }

        private string RandChooseForgottenFoodStrings(int seed)
        {
            // Keys map 1:1 to pre-extraction pool order so the seed picks identically post-Translate.
            return this.RandChoose(
                seed,
                "VM_FoodVariety_Forgotten_00",
                "VM_FoodVariety_Forgotten_01",
                "VM_FoodVariety_Forgotten_02",
                "VM_FoodVariety_Forgotten_03",
                "VM_FoodVariety_Forgotten_04",
                "VM_FoodVariety_Forgotten_05",
                "VM_FoodVariety_Forgotten_06",
                "VM_FoodVariety_Forgotten_07",
                "VM_FoodVariety_Forgotten_08",
                "VM_FoodVariety_Forgotten_09",
                "VM_FoodVariety_Forgotten_10",
                "VM_FoodVariety_Forgotten_11",
                "VM_FoodVariety_Forgotten_12",
                "VM_FoodVariety_Forgotten_13",
                "VM_FoodVariety_Forgotten_14",
                "VM_FoodVariety_Forgotten_15",
                "VM_FoodVariety_Forgotten_16",
                "VM_FoodVariety_Forgotten_17",
                "VM_FoodVariety_Forgotten_18",
                "VM_FoodVariety_Forgotten_19"
            ).Translate();
        }

        private string RandChooseNoVarietyStrings(int seed)
        {
            // Keys map 1:1 to pre-extraction pool order so the seed picks identically post-Translate.
            return this.RandChoose(
                seed,
                "VM_FoodVariety_NoVariety_00",
                "VM_FoodVariety_NoVariety_01",
                "VM_FoodVariety_NoVariety_02",
                "VM_FoodVariety_NoVariety_03",
                "VM_FoodVariety_NoVariety_04",
                "VM_FoodVariety_NoVariety_05",
                "VM_FoodVariety_NoVariety_06",
                "VM_FoodVariety_NoVariety_07",
                "VM_FoodVariety_NoVariety_08",
                "VM_FoodVariety_NoVariety_09",
                "VM_FoodVariety_NoVariety_10",
                "VM_FoodVariety_NoVariety_11",
                "VM_FoodVariety_NoVariety_12",
                "VM_FoodVariety_NoVariety_13",
                "VM_FoodVariety_NoVariety_14",
                "VM_FoodVariety_NoVariety_15",
                "VM_FoodVariety_NoVariety_16",
                "VM_FoodVariety_NoVariety_17",
                "VM_FoodVariety_NoVariety_18",
                "VM_FoodVariety_NoVariety_19"
            ).Translate();
        }

        private string RandChooseHasVarietyStrings(int seed)
        {
            // Keys map 1:1 to pre-extraction pool order so the seed picks identically post-Translate.
            return this.RandChoose(
                seed,
                "VM_FoodVariety_HasVariety_00",
                "VM_FoodVariety_HasVariety_01",
                "VM_FoodVariety_HasVariety_02",
                "VM_FoodVariety_HasVariety_03",
                "VM_FoodVariety_HasVariety_04",
                "VM_FoodVariety_HasVariety_05",
                "VM_FoodVariety_HasVariety_06",
                "VM_FoodVariety_HasVariety_07",
                "VM_FoodVariety_HasVariety_08",
                "VM_FoodVariety_HasVariety_09",
                "VM_FoodVariety_HasVariety_10",
                "VM_FoodVariety_HasVariety_11",
                "VM_FoodVariety_HasVariety_12",
                "VM_FoodVariety_HasVariety_13",
                "VM_FoodVariety_HasVariety_14",
                "VM_FoodVariety_HasVariety_15",
                "VM_FoodVariety_HasVariety_16",
                "VM_FoodVariety_HasVariety_17",
                "VM_FoodVariety_HasVariety_18",
                "VM_FoodVariety_HasVariety_19"
            ).Translate();
        }

        private string GetVarietyInfoBasedOnNoVarietyReason(NoVarietyReason noVarietyReason, int seed)
        {
            switch (noVarietyReason)
            {
                case NoVarietyReason.Rotten:
                    return this.RandChooseRottenStrings(seed);
                case NoVarietyReason.HumanLikeMeat:
                    return this.RandChooseHumanLikeMeatStrings(seed);
                case NoVarietyReason.InsectMeat:
                    return this.RandChooseInsectMeatStrings(seed);
                case NoVarietyReason.RawOrRawlikeFood:
                    return this.RandChooseRawOrRawlikeFoodStrings(seed);
                case NoVarietyReason.Fungus:
                    return this.RandChooseFungusStrings(seed);
                case NoVarietyReason.HasChemicals:
                    return this.RandChooseChemicalStrings(seed);
                case NoVarietyReason.UnacceptableByVegetarians:
                    return this.RandChooseUnacceptableByVegetariansStrings(seed);
                case NoVarietyReason.UnacceptableByCarnivores:
                    return this.RandChooseUnacceptableByCarnivoresStrings(seed);
                case NoVarietyReason.IsOrHasVeneratedAnimalMeat:
                    return this.RandChooseIsOrHasVeneratedAnimalMeatStrings(seed);
                case NoVarietyReason.DisgustingMeal:
                    return this.RandChooseDisgustingMealStrings(seed);
                default:
                    throw new NotImplementedException($"No handling logic implemented in switch for NoVarietyReason value: {noVarietyReason}.");
            }
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseRottenStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) This food was rotten or spoiled, I wanted to puke.",
                "(No Variety) Spoiled again? The smell alone is a turn-off.",
                "(No Variety) Rotten food is a no-go. It's completely inedible.");
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseDisgustingMealStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) That was revolting. I hope I never have to eat something like that again.",
                "(No Variety) Absolutely disgusting! It's hard to believe that's considered food.",
                "(No Variety) I've eaten some bad meals, but that one takes the cake for being the worst.",
                "(No Variety) The mere thought of that meal makes my stomach turn.",
                "(No Variety) That was horrendous. I'd rather go hungry than eat that again.",
                "(No Variety) What was that? It tasted like a mix of bad decisions and regret.",
                "(No Variety) Eating that was a challenge. It's the opposite of a culinary delight.",
                "(No Variety) If there's a contest for the worst meal ever, that one's a strong contender.",
                "(No Variety) Never again. That meal was a nightmare on a plate.",
                "(No Variety) That dish was an assault on my taste buds. Truly appalling.",
                "(No Variety) How can something edible be that repulsive? I'm baffled and nauseated.",
                "(No Variety) It's like someone cooked up disappointment and served it with a side of misery.",
                "(No Variety) I didn't think food could be that bad. I was wrong.",
                "(No Variety) That's one meal I wish I could erase from my memory – and my taste buds.",
                "(No Variety) It's hard to describe how bad that was. 'Disgusting' is an understatement.",
                "(No Variety) Whoever thought that was a good idea for a meal was sadly mistaken.",
                "(No Variety) I'm not sure what that was, but it's a culinary abomination.",
                "(No Variety) That meal was a gastronomic disaster. Just terrible.",
                "(No Variety) I've had some bad meals, but that one is going straight to the hall of shame.",
                "(No Variety) A truly vile concoction. It's a mystery how it can even be classified as food."
            );
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseHumanLikeMeatStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Eating human-like meat? That's a line I can't cross. It's horrifying.",
                "(No Variety) Human-like meat on the plate? That's beyond my limits.",
                "(No Variety) This is too much. Eating something so human-like is disturbing.");
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseInsectMeatStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Insect meat? It might be nutritious, but it's not my idea of variety.",
                "(No Variety) Bugs in the diet? That's not the variety I had in mind.",
                "(No Variety) Eating insects doesn't quite meet my expectations for diverse cuisine.");
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseRawOrRawlikeFoodStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Raw or almost raw... this isn't variety, it's a survival test.",
                "(No Variety) Nearly raw food again? Where's the joy in eating?",
                "(No Variety) This is too raw for my liking. Craving for something more cooked.");
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseFungusStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Just fungus? It's monotonous and lacks the essence of a varied meal.",
                "(No Variety) Fungus all the time? That's not exactly a feast of variety.",
                "(No Variety) A diet heavy in fungus isn't what I'd call varied. It's pretty one-note.");
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseChemicalStrings(int seed)
        {
            return this.RandChoose(seed,
                "(No Variety) Drugs in my food? That's a health hazard I'm not willing to take.",
                "(No Variety) I try to keep it natural. These drugs just don't sit right with me.",
                "(No Variety) Eating this feels like a science experiment gone wrong. No, thank you.",
                "(No Variety) My body isn't a lab for drug testing. I'll pass on this.",
                "(No Variety) Drugs in my meal? That's a line I won't cross for the sake of variety.",
                "(No Variety) I prefer my food free of additives. Drugs are a big no-no for me.",
                "(No Variety) It's hard to enjoy a meal when you know it's laced with drugs."
            );
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseUnacceptableByVegetariansStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) As a vegetarian, this just doesn't work for me. It's all wrong for variety.",
                "(No Variety) This isn't vegetarian-friendly at all. Can't consider it varied.",
                "(No Variety) Eating this would go against my vegetarian principles. It lacks variety.");
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseUnacceptableByCarnivoresStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) For a carnivore like me, this isn't variety, it's a plant-based challenge.",
                "(No Variety) Where's the meat? This isn't the kind of variety a carnivore needs.",
                "(No Variety) All plants and no meat? That's not what a carnivore's diet should be.");
        }

        // why-not-translated: NoVarietyReason sub-pools deferred to US-018b to cap initial Keyed count <= 200
        private string RandChooseIsOrHasVeneratedAnimalMeatStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Venerated animal meat is off-limits. This isn't variety, it's disrespect.",
                "(No Variety) Eating meat from a venerated animal? That's not something I can do.",
                "(No Variety) This includes meat from a revered animal. Can't partake in it.");
        }

        private string RandChoose(int seed, params string[] choices)
        {
            System.Random rand = new System.Random(seed);

            int caseSelector = rand.Next(0, choices.Length);

            return choices[caseSelector].ToString();
        }
    }
}
