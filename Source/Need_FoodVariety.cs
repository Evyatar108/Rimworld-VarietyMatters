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

		// Hover-overlay scratch (US-019).
		// _foodDefsScratch: cached icon sequence, rebuilt only on (DietTracker.Version, memoryMultiplier) change.
		private readonly List<ThingDef> _foodDefsScratch = new List<ThingDef>();
		private int _lastIconsVersion = -1;
		private float _lastIconsMemoryMult = -1f;
		private int _iconOverlayHash;
		private Action _drawHoverOverlayAction;

		private float _overlayWidth = 320f;
		private float _overlayHeight = 120f;
		private const float OverlayPadding = 6f;
		private const float TileMaxSize = 32f;

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

			Rect tipRect = rectForTooltip ?? rect;
			if (!doTooltip || !Mouse.IsOver(tipRect))
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

			// Measure content to size the overlay dynamically (prior fixed OverlayHeight clipped long tips and pushed icons off-screen).
			Text.Font = GameFont.Small;
			float innerWidth = this._overlayWidth - 2f * OverlayPadding;
			string tip = this.GetTipString();
			float textHeight = Text.CalcHeight(tip, innerWidth);
			float iconHeight = 0f;
			if (innerWidth >= 200f)
			{
				int iconCount = CountIconCandidates();
				if (iconCount > 0)
				{
					float tileWidth = Math.Min(innerWidth / iconCount, TileMaxSize);
					iconHeight = 6f + (tileWidth - 2f);
				}
			}
			this._overlayHeight = textHeight + iconHeight + 2f * OverlayPadding;

			Vector2 mouse = Event.current.mousePosition;
			Rect overlayRect = new Rect(mouse.x + 12f, mouse.y + 12f, this._overlayWidth, this._overlayHeight);
			if (overlayRect.xMax > UI.screenWidth) overlayRect.x = UI.screenWidth - overlayRect.width;
			if (overlayRect.yMax > UI.screenHeight) overlayRect.y = UI.screenHeight - overlayRect.height;
			if (overlayRect.x < 0f) overlayRect.x = 0f;
			if (overlayRect.y < 0f) overlayRect.y = 0f;

			Find.WindowStack.ImmediateWindow(this._iconOverlayHash, overlayRect, WindowLayer.Super, this._drawHoverOverlayAction, doBackground: true);
		}

		private int CountIconCandidates()
		{
			if (!VarietyRecord.TryGetVarietyRecord(this.pawn, out DietTracker tracker) || tracker == null)
			{
				return 0;
			}
			int version = tracker.Version;
			float memMult = ModSettings_VarietyMatters.memoryMultiplier;
			if (version != this._lastIconsVersion || memMult != this._lastIconsMemoryMult)
			{
				this._foodDefsScratch.Clear();
				foreach (var tuple in tracker.GetFoodVarietyInfoForPawnInIngestionOrder())
				{
					EatenFoodSource src = tuple.Item1;
					if (src == null || src.IsForgotten) continue;
					ThingDef def = src.ThingDef;
					if (def == null) continue;
					this._foodDefsScratch.Add(def);
				}
				this._lastIconsVersion = version;
				this._lastIconsMemoryMult = memMult;
			}
			return this._foodDefsScratch.Count;
		}

		private void DrawHoverOverlay()
		{
			Text.Font = GameFont.Small;
			Text.Anchor = TextAnchor.UpperLeft;
			Rect inner = new Rect(OverlayPadding, OverlayPadding, this._overlayWidth - 2f * OverlayPadding, this._overlayHeight - 2f * OverlayPadding);

			string tip = this.GetTipString();
			float textHeight = Text.CalcHeight(tip, inner.width);
			Rect textRect = new Rect(inner.x, inner.y, inner.width, textHeight);
			Widgets.Label(textRect, tip);

			if (inner.width < 200f)
			{
				return;
			}

			int count = CountIconCandidates();
			if (count == 0) return;

			float tileWidth = Math.Min(inner.width / count, TileMaxSize);
			float tileY = inner.y + textHeight + 6f;

			for (int i = 0; i < count; i++)
			{
				float x = inner.x + i * tileWidth;
				if (x + tileWidth > inner.xMax + 0.1f) break;
				Rect tile = new Rect(x, tileY, tileWidth - 2f, tileWidth - 2f);
				ThingDef def = this._foodDefsScratch[i];
				if (def.uiIcon != null)
				{
					Widgets.DrawTextureFitted(tile, def.uiIcon, 1f);
				}
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

                    if (ModSettings_VarietyMatters.clusterSimilarMealsTogether && eatenFoodSource.HasMealType)
                    {
                        switch (ModSettings_VarietyMatters.foodTrackingType)
                        {
                            case FoodTrackingType.ByMealNames:
                                stringBuilder.Append("VM_FoodVarietyTip_ByType".Translate());
                                break;
                            case FoodTrackingType.ByIngredientsCombination:
                                stringBuilder.Append("VM_FoodVarietyTip_ByIngredients".Translate());
                                break;
                            case FoodTrackingType.ByMealNamesAndIngredientsCombination:
                                stringBuilder.Append("VM_FoodVarietyTip_ByMealTypeAndIngredients".Translate());
                                break;
                        }
                    }
                    else
                    {
                        switch (ModSettings_VarietyMatters.foodTrackingType)
                        {
                            case FoodTrackingType.ByMealNames:
                                stringBuilder.Append("VM_FoodVarietyTip_ByMealName".Translate());
                                break;
                            case FoodTrackingType.ByIngredientsCombination:
                                stringBuilder.Append("VM_FoodVarietyTip_ByIngredients".Translate());
                                break;
                            case FoodTrackingType.ByMealNamesAndIngredientsCombination:
                                stringBuilder.Append("VM_FoodVarietyTip_ByMealNameAndIngredients".Translate());
                                break;
                        }
                    }
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
