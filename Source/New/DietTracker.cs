namespace VarietyMatters.New
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RimWorld;
    using UnityEngine;
    using Verse;

    public class DietTracker : IExposable
    {
        private Pawn pawn;
        private Dictionary<string, FoodVarietyInfo> foodsVarietyInfos;
        private Dictionary<EatenFoodByPawn, NoVarietyReason> foodsNoVarietyReasons;
        private int maxEatenFoodSourceInMemory;
        private int totalVariety;
        private float totalVarietyScore;
        private Queue<EatenFoodByPawn> foodSourcesByOrderV2;

        private List<EatenFoodSource> tempList;
        private List<EatenFoodByPawn> tempListV2;

        private int _version;

        private Dictionary<string, int> _countByKey;

        // Memory is count-based only: `foodSourcesByOrderV2` is capped at
        // `memoryMultiplier × varietyExpectation` meals by UpdateMaxFoodInMemory. All queued meals
        // contribute to overlap scoring; no time filter. Extremely Low (~6 meals) through Noble
        // (~33 meals) naturally get tier-differentiated memory depth.

        // Shared scratch for overlap computation. Cleared on method entry of each consumer
        // (ComputeVarietyScore, GetOverlapBreakdown). Never held across frames.
        private static readonly HashSet<ThingDef> _recentIngSetScratch = new HashSet<ThingDef>();

        // Previously held past-meal ThingDef set for the in-score meal-def-new bonus (dropped
        // in US-026 when the bonus moved to mood-tick read time via IsMealDefFreshInWindow).

        // US-025 meal-def-new bonus — step table keyed on recipe slot count. A meal whose ThingDef
        // has not been seen in the decay queue contributes an additive variety bonus sized by
        // recipe complexity: simple meals (1 slot) get nothing, vanilla fine/lavish (2 slots) earn
        // a small +0.05, modded 3+ slot recipes escalate up to +0.30 at 5+ slots. Step table (not
        // linear) so the player's mental model is "cook fancier recipes for more bonus" without
        // per-ingredient fiddliness. Index = min(slots, 5); a "capped" tooltip label fires only
        // when modded 6+ slot recipes are clamped.
        private static readonly float[] _mealDefBonusBySlot = { 0f, 0f, 0.05f, 0.12f, 0.20f, 0.30f };
        public const int MaxBonusSlots = 5;

        public static float GetMealDefBonus(int slots)
        {
            if (slots < 0) return 0f;
            int idx = slots >= _mealDefBonusBySlot.Length ? _mealDefBonusBySlot.Length - 1 : slots;
            return _mealDefBonusBySlot[idx];
        }

        // Breakdown cache. Keyed on (meal ref, _version) so any tracker mutation that bumps
        // _version invalidates it. UX consumers receive live refs; treat as read-only.
        private EatenFoodSource _breakdownCacheMeal;
        private int _breakdownCacheVersion = -1;
        private readonly HashSet<ThingDef> _breakdownOverlapScratch = new HashSet<ThingDef>();
        private readonly HashSet<ThingDef> _breakdownFreshScratch = new HashSet<ThingDef>();

        // Scratch for GetMealsAgoForOverlapDefs. Not on the hot path (tooltip only).
        // Consumers: GetMealsAgoForOverlapDefs.
        private readonly List<EatenFoodByPawn> _mealIndexScratch = new List<EatenFoodByPawn>();

        public int Version => this._version;

        // US-027 food-search + US-026 mood alignment: read-only probe for whether a stored meal
        // was flagged at ingest as "non-variety" (raw-like food, insect meat, venerated, etc.
        // — see VarietyProvider.DoesFoodHasVarietyValueForPawn). Consumers skip these when
        // aggregating mood so raw-meat eating neither lifts nor drags mood under overlap mode.
        public bool HasNoVarietyReason(EatenFoodByPawn eatenFood)
        {
            return eatenFood != null
                && this.foodsNoVarietyReasons != null
                && this.foodsNoVarietyReasons.ContainsKey(eatenFood);
        }

        public DietTracker()
        {
        }

        public DietTracker(Pawn pawn)
        {
            this.pawn = pawn;
            this.maxEatenFoodSourceInMemory = GetMaxEatenFoodSourceInMemory(pawn);

            this.foodsVarietyInfos = new Dictionary<string, FoodVarietyInfo>(this.maxEatenFoodSourceInMemory + 1);
            this.foodsNoVarietyReasons = new Dictionary<EatenFoodByPawn, NoVarietyReason>(this.maxEatenFoodSourceInMemory + 1);
            this.foodSourcesByOrderV2 = new Queue<EatenFoodByPawn>(this.maxEatenFoodSourceInMemory + 1);
            this.KeysOfFoodSourcesWithVariety = new HashSet<string>();
            this._countByKey = new Dictionary<string, int>(this.maxEatenFoodSourceInMemory + 1);
            this.totalVariety = 0;

            this.AddForgottenFoods();
        }

        public Pawn Pawn => this.pawn;

        public int TotalVariety => this.totalVariety;

        public float TotalVarietyScore => this.totalVarietyScore;

        public int MealsInMemory => this.foodSourcesByOrderV2.Count;

        public IEnumerable<EatenFoodByPawn> EatenFoodSourcesByOrder => this.foodSourcesByOrderV2;

        public EatenFoodByPawn mostRecentEatenFoodSourceV2;

        public EatenFoodByPawn MostRecentEatenFoodSource => this.mostRecentEatenFoodSourceV2;

        public HashSet<string> KeysOfFoodSourcesWithVariety { get; private set; }

        public IEnumerable<(EatenFoodSource, FoodVarietyInfo, NoVarietyReason?)> GetFoodVarietyInfoForPawnInIngestionOrder()
        {
            foreach (EatenFoodByPawn eatenFoodByPawn in this.foodSourcesByOrderV2)
            {
                if (this.foodsVarietyInfos.TryGetValue(eatenFoodByPawn.Source.GetFoodSourceKey(), out FoodVarietyInfo foodVarietyInfo))
                {
                    yield return (eatenFoodByPawn.Source,  foodVarietyInfo, null);
                }
                else if (this.foodsNoVarietyReasons.TryGetValue(eatenFoodByPawn, out NoVarietyReason noVarietyReason))
                {
                    yield return (eatenFoodByPawn.Source, null, noVarietyReason);
                }
            }
        }

        private void AddForgottenFoods()
        {
            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(this.pawn);

            for (int i = 0; i < varietyExpectation; i++)
            {
                var foodSource = FoodSourceFactory.GetForgottenFoodSource();

                this.AddFoodSourceToMemory(new EatenFoodByPawn(this.pawn, foodSource));
            }
        }

        // US-029 storage cap. Always store max(all tier memory settings) + ScoringWindow meals
        // regardless of the pawn's current tier. Rationale:
        //   1. Tier changes (wealth swings, royal title gain/loss, mid-game settings edits)
        //      don't lose history — queue stays at max, aggregation window shrinks to current
        //      tier's memory value instead.
        //   2. ReCount's oldest-meal warm-up bias (meal 1 scores against zero predecessors →
        //      defaults to 1.0) is absorbed inside the ScoringWindow buffer: the oldest 6 meals
        //      live in the buffer but never contribute to mood. Mood-visible window is clean.
        private static int GetMaxEatenFoodSourceInMemory(Pawn pawn)
        {
            if (pawn == null)
            {
                throw new ArgumentNullException(nameof(pawn));
            }

            return VarietyAdjuster.MaxMemoryAcrossTiers() + VarietyAdjuster.ScoringWindow;
        }

        public void UpdateMaxFoodInMemory()
        {
            this.maxEatenFoodSourceInMemory = GetMaxEatenFoodSourceInMemory(this.Pawn);

            while (this.foodSourcesByOrderV2.Count > this.maxEatenFoodSourceInMemory)
            {
                this.RemoveOldestFoodSourceFromMemory();
            }

            this._version++;
        }

        public void AddFoodSource(EatenFoodSource eatenFoodSource)
        {
            this._version++;

            if (ModSettings_VarietyMatters.foodTrackingType == FoodTrackingType.ByIngredientOverlap
                && !eatenFoodSource.IsForgotten)
            {
                eatenFoodSource.VarietyScore = this.ComputeVarietyScore(eatenFoodSource);
            }

            var eatenFoodbyPawn = new EatenFoodByPawn(this.pawn, eatenFoodSource);

            if (ModSettings_VarietyMatters.foodDrugsAreOnlyInMemoryOnce &&
                eatenFoodbyPawn.Source.DrugCategory != RimWorld.DrugCategory.None
                && this.KeysOfFoodSourcesWithVariety.Contains(eatenFoodbyPawn.Source.GetFoodSourceKey()))
            {
                this.RemoveFoodFromQueueAndCountBasedOnKey(eatenFoodbyPawn.Source.GetFoodSourceKey());
                this.AddFoodSourceToMemory(eatenFoodbyPawn);
                return;
            }

            this.AddFoodSourceToMemory(eatenFoodbyPawn);

            if (this.foodSourcesByOrderV2.Count > this.maxEatenFoodSourceInMemory)
            {
                this.RemoveOldestFoodSourceFromMemory();
            }

            if (!eatenFoodbyPawn.Source.IsForgotten
                && this.OldestFoodSourceIsForgotten())
            {
                this.RemoveOldestFoodSourceFromMemory();
            }
        }

        public EatenFoodByPawn OldestFoodSource()
        {
            return this.foodSourcesByOrderV2.Peek();
        }

        private bool OldestFoodSourceIsForgotten()
        {
            return this.foodSourcesByOrderV2.Peek().Source.IsForgotten;
        }

        public void ReCount()
        {
            this.totalVariety = 0;
            this.totalVarietyScore = 0f;

            this.foodsVarietyInfos.Clear();
            this.KeysOfFoodSourcesWithVariety.Clear();
            this._countByKey.Clear();

            bool overlapMode = ModSettings_VarietyMatters.foodTrackingType == FoodTrackingType.ByIngredientOverlap;

            foreach (EatenFoodByPawn foodSource in this.foodSourcesByOrderV2)
            {
                if (foodSource.Source.ThingLabel == null && !foodSource.Source.IsForgotten)
                {
                    Log.Warning("DietTracker:: Found food source with null thing label while recounting");
                    continue;
                }

                if (overlapMode && !foodSource.Source.IsForgotten)
                {
                    foodSource.Source.VarietyScore = this.ComputeVarietyScore(foodSource.Source);
                }

                this.AddFoodSourceToCount(foodSource);
            }

            this._version++;
        }

        private void AddFoodSourceToMemory(EatenFoodByPawn foodSource)
        {
            this.foodSourcesByOrderV2.Enqueue(foodSource);
            this.mostRecentEatenFoodSourceV2 = foodSource;

            this.AddFoodSourceToCount(foodSource);
        }

        private void AddFoodSourceToCount(EatenFoodByPawn eatenFood)
        {
            string foodSourceKey = eatenFood.Source.GetFoodSourceKey();

            this.totalVarietyScore += eatenFood.Source.VarietyScore;

            if (this._countByKey.TryGetValue(foodSourceKey, out int existingCount))
            {
                this._countByKey[foodSourceKey] = existingCount + 1;
            }
            else
            {
                this._countByKey[foodSourceKey] = 1;
            }

            bool hasVarietyValueForPawn = VarietyProvider.DoesFoodHasVarietyValueForPawn(this.Pawn, eatenFood, out NoVarietyReason noVarietyReason);

            if (!hasVarietyValueForPawn && noVarietyReason == NoVarietyReason.NA && Prefs.DevMode)
            {
                Log.Warning("DietTracker:: Variety reason mismatch");
            }

            if (!hasVarietyValueForPawn)
            {
                this.foodsNoVarietyReasons[eatenFood] = noVarietyReason;
                return;
            }

            if (this.foodsVarietyInfos.TryGetValue(foodSourceKey, out FoodVarietyInfo foodSourceInfoForPawn))
            {
                foodSourceInfoForPawn.CountInMemory++;
                foodSourceInfoForPawn.FoodSources.Add(eatenFood.Source);

                if (eatenFood.Source.IsForgotten)
                {
                    this.totalVariety++;
                }
            }
            else
            {
                this.foodsVarietyInfos[foodSourceKey] = new FoodVarietyInfo
                {
                    FoodSources = new List<EatenFoodSource> { eatenFood.Source },
                    CountInMemory = 1,
                };

                this.totalVariety++;
                this.KeysOfFoodSourcesWithVariety.Add(foodSourceKey);
            }
        }

        private void RemoveFoodFromQueueAndCountBasedOnKey(string key)
        {
            if (!this._countByKey.ContainsKey(key))
            {
                return;
            }

            int initialCount = this.foodSourcesByOrderV2.Count;
            for (int i = 0; i < initialCount; i++)
            {
                EatenFoodByPawn eatenFoodSource = this.foodSourcesByOrderV2.Dequeue();
                if (eatenFoodSource.Source.GetFoodSourceKey() == key)
                {
                    this.RemoveFoodFromCount(eatenFoodSource);
                }
                else
                {
                    this.foodSourcesByOrderV2.Enqueue(eatenFoodSource);
                }
            }
        }

        private void RemoveOldestFoodSourceFromMemory()
        {
            EatenFoodByPawn oldestEatenFood = this.foodSourcesByOrderV2.Dequeue();
            this.RemoveFoodFromCount(oldestEatenFood);
        }

        private void RemoveFoodFromCount(EatenFoodByPawn eatenFoodSource)
        {
            string key = eatenFoodSource.Source.GetFoodSourceKey();

            this.totalVarietyScore -= eatenFoodSource.Source.VarietyScore;

            if (this._countByKey.TryGetValue(key, out int existingCount))
            {
                if (existingCount <= 1)
                {
                    this._countByKey.Remove(key);
                }
                else
                {
                    this._countByKey[key] = existingCount - 1;
                }
            }

            if (this.foodsVarietyInfos.TryGetValue(key, out FoodVarietyInfo foodSourceInfoForPawn))
            {
                foodSourceInfoForPawn.CountInMemory--;
                foodSourceInfoForPawn.FoodSources.Remove(eatenFoodSource.Source);
                if (foodSourceInfoForPawn.CountInMemory == 0)
                {
                    this.foodsVarietyInfos.Remove(key);
                    this.totalVariety--;
                    this.KeysOfFoodSourcesWithVariety.Remove(key);
                }
                else if (foodSourceInfoForPawn.FoodSources[0].IsForgotten)
                {
                    this.totalVariety--;
                }
            }
            else
            {
                this.foodsNoVarietyReasons.Remove(eatenFoodSource);
            }
        }

        // Jaccard overlap: 1 - |target.ingredients ∩ recent_ingredient_set| / |target.ingredients|.
        // recent_ingredient_set = union of ingredient defs from the last `ScoringWindow` queued
        // meals strictly prior to `target` in tick order. Anchoring on target.AteAtTick (not the
        // current tick) keeps the result consistent whether scoring at ingest (target not yet
        // queued) or rebuilding in ReCount (target already queued — later meals must not leak).
        // ScoringWindow decouples scoring saturation from per-tier storage depth: storage queue
        // still tier-scaled (memoryMultiplier × tierSetting) for meal-def rotation + UI, but
        // overlap math uses a fixed small window.
        // Empty-ingredient meals (paste/kibble without CompIngredients) fall back to singleton
        // ThingDef of the meal.
        private float ComputeVarietyScore(EatenFoodSource target)
        {
            _recentIngSetScratch.Clear();

            int anchorTick = target.AteAtTick;
            if (anchorTick < 0)
            {
                anchorTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            }

            // Count eligible (pre-target, non-forgotten) meals so we can skip down to the last
            // ScoringWindow before the ingredient-overlap walk. The set is small (~10-40), so
            // two passes are negligible.
            int eligibleCount = 0;
            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past.Source;
                if (src == null || src.IsForgotten) continue;
                if (ReferenceEquals(src, target)) continue;
                int t = src.AteAtTick;
                if (t < 0 || t >= anchorTick) continue;
                eligibleCount++;
            }
            int skipCount = System.Math.Max(0, eligibleCount - VarietyAdjuster.ScoringWindow);
            int seen = 0;

            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past.Source;
                if (src == null) continue;
                if (ReferenceEquals(src, target)) continue;
                if (src.IsForgotten) continue;
                int t = src.AteAtTick;
                if (t < 0) continue;
                if (t >= anchorTick) continue;
                if (seen++ < skipCount) continue;

                List<ThingDef> pastDefs = src.IngredientsDefs;
                if (pastDefs != null && pastDefs.Count > 0)
                {
                    for (int i = 0; i < pastDefs.Count; i++)
                    {
                        ThingDef d = pastDefs[i];
                        if (d != null) _recentIngSetScratch.Add(d);
                    }
                }
                else if (src.ThingDef != null)
                {
                    _recentIngSetScratch.Add(src.ThingDef);
                }
            }

            List<ThingDef> inc = target.IngredientsDefs;
            int overlap;
            int total;
            if (inc != null && inc.Count > 0)
            {
                total = inc.Count;
                overlap = 0;
                for (int i = 0; i < inc.Count; i++)
                {
                    if (_recentIngSetScratch.Contains(inc[i])) overlap++;
                }
            }
            else if (target.ThingDef != null)
            {
                total = 1;
                overlap = _recentIngSetScratch.Contains(target.ThingDef) ? 1 : 0;
            }
            else
            {
                return 1f;
            }

            // Returns ingredient-only overlap score. The meal-def-new bonus (US-025) is applied
            // AT MOOD-TICK READ TIME (VarietyAdjuster) AFTER the baseline curve — so novelty cuts
            // through a noble's refined palate instead of getting dampened alongside the ingredient
            // signal. Bonus is recomputed from IsMealDefFreshInWindow; we don't store it here.
            return 1f - (float)overlap / total;
        }

        // US-025 / US-029 helper for UX tooltip + meal-def-new bonus gate. Returns whether the
        // meal's ThingDef is absent from the pawn's current tier memory window strictly prior to
        // `meal`. Scoped to the current tier's memory size (not the full storage queue, which
        // includes the +ScoringWindow archive that absorbs ReCount warm-up bias). Anchored on
        // meal.AteAtTick so the answer stays stable across ingest-time and ReCount rebuilds.
        public bool IsMealDefFreshInWindow(EatenFoodSource meal)
        {
            if (meal == null || meal.ThingDef == null) return false;

            int anchorTick = meal.AteAtTick;
            if (anchorTick < 0)
            {
                anchorTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            }

            int currentTierMemory = VarietyAdjuster.GetMemoryForTier(
                ExpectationsUtility.CurrentExpectationFor(this.pawn));

            // Count eligibles strictly prior to `meal`, then skip oldest to leave the last
            // currentTierMemory candidates. Two-pass mirrors ComputeVarietyScore for consistency.
            int eligibleTotal = 0;
            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource s = past.Source;
                if (s == null || s.IsForgotten) continue;
                if (ReferenceEquals(s, meal)) continue;
                int t = s.AteAtTick;
                if (t < 0 || t >= anchorTick) continue;
                eligibleTotal++;
            }
            int skipCount = System.Math.Max(0, eligibleTotal - currentTierMemory);
            int seen = 0;

            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past.Source;
                if (src == null) continue;
                if (ReferenceEquals(src, meal)) continue;
                if (src.IsForgotten) continue;
                int t = src.AteAtTick;
                if (t < 0) continue;
                if (t >= anchorTick) continue;
                if (seen++ < skipCount) continue;

                if (src.ThingDef == meal.ThingDef) return false;
            }
            return true;
        }

        // US-026 food-search alignment: estimate the raw ingredient-overlap score a candidate
        // Thing would receive if eaten *now*, without mutating tracker state. Used by
        // Patch_FoodUtility.FoodOptimality to steer pawn food choice toward meals that will score
        // well under the same formula the mood system uses, so eating preference and mood math
        // stay in sync.
        public float EstimateIngredientScoreForCandidate(Thing candidateThing)
        {
            if (candidateThing?.def == null) return 1f;

            // Scope to the last ScoringWindow eligible meals — matches the scoring math used
            // at ingest/ReCount so food-search preference aligns with mood contribution.
            _recentIngSetScratch.Clear();
            int eligibleCount = 0;
            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past?.Source;
                if (src == null || src.IsForgotten) continue;
                eligibleCount++;
            }
            int skipCount = System.Math.Max(0, eligibleCount - VarietyAdjuster.ScoringWindow);
            int seen = 0;

            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past?.Source;
                if (src == null || src.IsForgotten) continue;
                if (seen++ < skipCount) continue;

                List<ThingDef> pastDefs = src.IngredientsDefs;
                if (pastDefs != null && pastDefs.Count > 0)
                {
                    for (int i = 0; i < pastDefs.Count; i++)
                    {
                        ThingDef d = pastDefs[i];
                        if (d != null) _recentIngSetScratch.Add(d);
                    }
                }
                else if (src.ThingDef != null)
                {
                    _recentIngSetScratch.Add(src.ThingDef);
                }
            }

            // Read candidate ingredients from CompIngredients (what the meal is made of). Fall
            // back to the meal's own ThingDef when no CompIngredients is present (raw, paste,
            // kibble). Matches the empty-ingredient-singleton rule used at ingest.
            var comp = candidateThing.TryGetComp<CompIngredients>();
            List<ThingDef> inc = comp?.ingredients;
            int overlap, total;
            if (inc != null && inc.Count > 0)
            {
                total = inc.Count;
                overlap = 0;
                for (int i = 0; i < inc.Count; i++)
                {
                    if (_recentIngSetScratch.Contains(inc[i])) overlap++;
                }
            }
            else
            {
                total = 1;
                overlap = _recentIngSetScratch.Contains(candidateThing.def) ? 1 : 0;
            }
            return 1f - (float)overlap / total;
        }

        // US-026 food-search: is this candidate meal's ThingDef absent from the queue? Mirrors
        // IsMealDefFreshInWindow for a Thing (not an EatenFoodSource). Queue is count-bounded,
        // no time filter.
        public bool IsCandidateMealDefFresh(Thing candidateThing)
        {
            if (candidateThing?.def == null) return false;

            int currentTierMemory = VarietyAdjuster.GetMemoryForTier(
                ExpectationsUtility.CurrentExpectationFor(this.pawn));

            // Two-pass window: skip the oldest so only the current tier's memory range is
            // checked, matching IsMealDefFreshInWindow's semantics after US-029.
            int eligibleTotal = 0;
            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource s = past?.Source;
                if (s == null || s.IsForgotten) continue;
                eligibleTotal++;
            }
            int skipCount = System.Math.Max(0, eligibleTotal - currentTierMemory);
            int seen = 0;

            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past?.Source;
                if (src == null || src.IsForgotten) continue;
                if (seen++ < skipCount) continue;
                if (src.ThingDef == candidateThing.def) return false;
            }
            return true;
        }

        // Read-only overlap partition for UX. Returns shared scratch sets -- treat as read-only;
        // any mutation to the tracker bumps _version and invalidates the cache on next call.
        public (HashSet<ThingDef> overlap, HashSet<ThingDef> fresh) GetOverlapBreakdown(EatenFoodSource meal)
        {
            // Null meal returns empty scratch without touching cache fields; safe because the next
            // non-null call still sees the original _breakdownCacheMeal ref and rebuilds normally.
            if (meal == null)
            {
                this._breakdownOverlapScratch.Clear();
                this._breakdownFreshScratch.Clear();
                return (this._breakdownOverlapScratch, this._breakdownFreshScratch);
            }

            if (ReferenceEquals(meal, this._breakdownCacheMeal) && this._breakdownCacheVersion == this._version)
            {
                return (this._breakdownOverlapScratch, this._breakdownFreshScratch);
            }

            this._breakdownOverlapScratch.Clear();
            this._breakdownFreshScratch.Clear();

            _recentIngSetScratch.Clear();
            int anchorTick = meal.AteAtTick;
            if (anchorTick < 0)
            {
                anchorTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            }

            // Scope to ScoringWindow eligible meals — matches ComputeVarietyScore so tooltip
            // breakdown shows exactly the meals that drove the stored score.
            int eligibleCount = 0;
            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past.Source;
                if (src == null || src.IsForgotten) continue;
                if (ReferenceEquals(src, meal)) continue;
                int t = src.AteAtTick;
                if (t < 0 || t >= anchorTick) continue;
                eligibleCount++;
            }
            int skipCount = System.Math.Max(0, eligibleCount - VarietyAdjuster.ScoringWindow);
            int seen = 0;

            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past.Source;
                if (src == null) continue;
                if (ReferenceEquals(src, meal)) continue;
                if (src.IsForgotten) continue;
                int t = src.AteAtTick;
                if (t < 0) continue;
                if (t >= anchorTick) continue;
                if (seen++ < skipCount) continue;

                List<ThingDef> pastDefs = src.IngredientsDefs;
                if (pastDefs != null && pastDefs.Count > 0)
                {
                    for (int i = 0; i < pastDefs.Count; i++)
                    {
                        ThingDef d = pastDefs[i];
                        if (d != null) _recentIngSetScratch.Add(d);
                    }
                }
                else if (src.ThingDef != null)
                {
                    _recentIngSetScratch.Add(src.ThingDef);
                }
            }

            List<ThingDef> inc = meal.IngredientsDefs;
            if (inc != null && inc.Count > 0)
            {
                for (int i = 0; i < inc.Count; i++)
                {
                    ThingDef d = inc[i];
                    if (_recentIngSetScratch.Contains(d))
                    {
                        this._breakdownOverlapScratch.Add(d);
                    }
                    else
                    {
                        this._breakdownFreshScratch.Add(d);
                    }
                }
            }
            else if (meal.ThingDef != null)
            {
                if (_recentIngSetScratch.Contains(meal.ThingDef))
                {
                    this._breakdownOverlapScratch.Add(meal.ThingDef);
                }
                else
                {
                    this._breakdownFreshScratch.Add(meal.ThingDef);
                }
            }

            this._breakdownCacheMeal = meal;
            this._breakdownCacheVersion = this._version;
            return (this._breakdownOverlapScratch, this._breakdownFreshScratch);
        }

        // For each def in overlapDefs, writes the most-recent past-meal-index distance
        // (1 = immediately preceding meal, 2 = two meals back, etc.) into result.
        // Walks all queued meals before `meal` in order (no time filter — queue is count-bounded).
        // Clears result on entry. Tooltip-only path — not hot.
        public void GetMealsAgoForOverlapDefs(EatenFoodSource meal, HashSet<ThingDef> overlapDefs, Dictionary<ThingDef, int> result)
        {
            result.Clear();
            if (meal == null || overlapDefs == null || overlapDefs.Count == 0) return;

            this._mealIndexScratch.Clear();
            foreach (EatenFoodByPawn e in this.foodSourcesByOrderV2)
            {
                this._mealIndexScratch.Add(e);
            }

            int mealIdx = -1;
            for (int i = 0; i < this._mealIndexScratch.Count; i++)
            {
                if (ReferenceEquals(this._mealIndexScratch[i].Source, meal))
                {
                    mealIdx = i;
                    break;
                }
            }
            if (mealIdx < 0) return;

            int remaining = overlapDefs.Count;
            int distance = 0;
            for (int i = mealIdx - 1; i >= 0 && remaining > 0; i--)
            {
                EatenFoodSource src = this._mealIndexScratch[i].Source;
                if (src == null || src.IsForgotten) continue;
                distance++;
                List<ThingDef> defs = src.IngredientsDefs;
                if (defs != null && defs.Count > 0)
                {
                    for (int j = 0; j < defs.Count; j++)
                    {
                        ThingDef d = defs[j];
                        if (d != null && overlapDefs.Contains(d) && !result.ContainsKey(d))
                        {
                            result[d] = distance;
                            remaining--;
                        }
                    }
                }
                else if (src.ThingDef != null && overlapDefs.Contains(src.ThingDef) && !result.ContainsKey(src.ThingDef))
                {
                    result[src.ThingDef] = distance;
                    remaining--;
                }
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look<Pawn>(ref this.pawn, "pawn", saveDestroyedThings: true);
            EatenFoodSource tempMostRecentEatenFoodSource = null; ;
            Scribe_Values.Look<int>(ref this.maxEatenFoodSourceInMemory, "maxEatenFoodSourceInMemory");

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                tempListV2 = this.foodSourcesByOrderV2.ToList();
                Scribe_Collections.Look(ref tempListV2, "foodSourcesByOrderV2", LookMode.Deep);
                Scribe_Deep.Look<EatenFoodByPawn>(ref this.mostRecentEatenFoodSourceV2, "mostRecentEatenFoodSourceV2");
            }
            else
            {
                Scribe_References.Look<EatenFoodSource>(ref tempMostRecentEatenFoodSource, "mostRecentEatenFoodSource");
                Scribe_Deep.Look<EatenFoodByPawn>(ref this.mostRecentEatenFoodSourceV2, "mostRecentEatenFoodSourceV2");
                if (this.mostRecentEatenFoodSourceV2 == null && tempMostRecentEatenFoodSource != null)
                {
                    this.mostRecentEatenFoodSourceV2 = new EatenFoodByPawn(this.pawn, tempMostRecentEatenFoodSource);
                }

                Scribe_Collections.Look(ref tempList, "foodSourcesByOrder", LookMode.Reference);
                Scribe_Collections.Look(ref tempListV2, "foodSourcesByOrderV2", LookMode.Deep);

                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    int skippedCount = 0;

                    if (tempList != null)
                    {
                        this.foodSourcesByOrderV2 = new Queue<EatenFoodByPawn>(tempList.Count + 1);
                        for (int i = 0; i < tempList.Count; i++)
                        {
                            EatenFoodSource src = tempList[i];
                            if (IsEatenFoodSourceValid(src))
                            {
                                this.foodSourcesByOrderV2.Enqueue(new EatenFoodByPawn(this.pawn, src));
                            }
                            else
                            {
                                skippedCount++;
                            }
                        }
                    }

                    if (tempListV2 != null)
                    {
                        this.foodSourcesByOrderV2 = new Queue<EatenFoodByPawn>(tempListV2.Count + 1);
                        for (int i = 0; i < tempListV2.Count; i++)
                        {
                            EatenFoodByPawn src = tempListV2[i];
                            if (IsEatenFoodSourceByPawnValid(src))
                            {
                                this.foodSourcesByOrderV2.Enqueue(src);
                            }
                            else
                            {
                                skippedCount++;
                            }
                        }
                    }

                    if (skippedCount > 0)
                    {
                        Log.Warning($"VarietyMatters: ExposeData skipped {skippedCount} invalid food entries for {this.pawn?.LabelShort ?? "<null>"}");
                    }

                    this.foodsVarietyInfos = new Dictionary<string, FoodVarietyInfo>(this.maxEatenFoodSourceInMemory + 1);
                    this.foodsNoVarietyReasons = new Dictionary<EatenFoodByPawn, NoVarietyReason>(this.maxEatenFoodSourceInMemory + 1);
                    this.KeysOfFoodSourcesWithVariety = new HashSet<string>();
                    this._countByKey = new Dictionary<string, int>(this.maxEatenFoodSourceInMemory + 1);

                    this.ReCount();
                }
            }
        }

        private bool IsEatenFoodSourceByPawnValid(EatenFoodByPawn eatenFoodSourceByPawn)
        {
            if (eatenFoodSourceByPawn == null)
            {
                return false;
            }

            if (eatenFoodSourceByPawn.Pawn == null)
            {
                return false;
            }

            return IsEatenFoodSourceValid(eatenFoodSourceByPawn.Source);
        }

        private bool IsEatenFoodSourceValid(EatenFoodSource eatenFoodSource)
        {
            if (eatenFoodSource == null)
            {
                return false;
            }

            if (eatenFoodSource.IsForgotten)
            {
                return true;
            }

            if (eatenFoodSource.ThingLabel == null)
            {
                return false;
            }

            if (eatenFoodSource.ThingDef == null)
            {
                return false;
            }

            return true;
        }

        public class FoodVarietyInfo
        {
            public List<EatenFoodSource> FoodSources { get; set; }

            public int CountInMemory { get; set; }
        }

        public class FoodSourceNoVarietyForPawn
        {
            public NoVarietyReason NoVarietyReason { get; set; }
        }
    }
}
