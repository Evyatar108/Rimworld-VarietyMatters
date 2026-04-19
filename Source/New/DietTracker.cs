namespace VarietyMatters.New
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        // Decay window (~2 in-game days). Meals older than this are dropped from the overlap set
        // when scoring a new meal under ByIngredientOverlap.
        private const int VarietyDecayTicks = 120000;

        // Shared scratch for overlap computation. Cleared on method entry of each consumer
        // (ComputeVarietyScore, GetOverlapBreakdown). Never held across frames.
        private static readonly HashSet<ThingDef> _recentIngSetScratch = new HashSet<ThingDef>();

        // Breakdown cache. Keyed on (meal ref, _version) so any tracker mutation that bumps
        // _version invalidates it. UX consumers receive live refs; treat as read-only.
        private EatenFoodSource _breakdownCacheMeal;
        private int _breakdownCacheVersion = -1;
        private readonly HashSet<ThingDef> _breakdownOverlapScratch = new HashSet<ThingDef>();
        private readonly HashSet<ThingDef> _breakdownFreshScratch = new HashSet<ThingDef>();

        public int Version => this._version;

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

        private static int GetMaxEatenFoodSourceInMemory(Pawn pawn)
        {
            if (pawn == null)
            {
                throw new ArgumentNullException(nameof(pawn));
            }

            float memoryMultiplier = ModSettings_VarietyMatters.memoryMultiplier;
            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(pawn);
            return (int)(varietyExpectation * memoryMultiplier);
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
        // recent_ingredient_set = union of ingredient defs from queued meals strictly prior to
        // `target` in time, within VarietyDecayTicks. Anchoring on target.AteAtTick (rather than
        // the current tick) keeps the result consistent whether we are scoring at ingest (target
        // not yet queued) or rebuilding in ReCount (target is already in the queue, and later
        // meals must not leak into an older meal's historical score).
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

            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past.Source;
                if (src == null) continue;
                if (ReferenceEquals(src, target)) continue;
                if (src.IsForgotten) continue;
                int t = src.AteAtTick;
                if (t < 0) continue;
                if (t >= anchorTick) continue;
                if (anchorTick - t > VarietyDecayTicks) continue;

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

            return 1f - (float)overlap / total;
        }

        // Read-only overlap partition for UX. Returns shared scratch sets -- treat as read-only;
        // any mutation to the tracker bumps _version and invalidates the cache on next call.
        public (HashSet<ThingDef> overlap, HashSet<ThingDef> fresh) GetOverlapBreakdown(EatenFoodSource meal)
        {
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
            foreach (EatenFoodByPawn past in this.foodSourcesByOrderV2)
            {
                EatenFoodSource src = past.Source;
                if (src == null) continue;
                if (ReferenceEquals(src, meal)) continue;
                if (src.IsForgotten) continue;
                int t = src.AteAtTick;
                if (t < 0) continue;
                if (t >= anchorTick) continue;
                if (anchorTick - t > VarietyDecayTicks) continue;

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
