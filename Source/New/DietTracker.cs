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
        private Queue<EatenFoodByPawn> foodSourcesByOrderV2;

        private List<EatenFoodSource> tempList;
        private List<EatenFoodByPawn> tempListV2;

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
            this.totalVariety = 0;

            this.AddForgottenFoods();
        }

        public Pawn Pawn => this.pawn;

        public int TotalVariety => this.totalVariety;

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
        }

        public void AddFoodSource(EatenFoodSource eatenFoodSource)
        {
            var eatenFoodbyPawn = new EatenFoodByPawn(this.pawn, eatenFoodSource);
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

            this.foodsVarietyInfos.Clear();
            this.KeysOfFoodSourcesWithVariety.Clear();

            foreach (EatenFoodByPawn foodSource in this.foodSourcesByOrderV2)
            {
                if (foodSource.Source.ThingLabel == null && !foodSource.Source.IsForgotten)
                {
                    Log.Warning("DietTracker:: Found food source with null thing label while recounting");
                    continue;
                }

                this.AddFoodSourceToCount(foodSource);
            }
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

            bool hasVarietyValueForPawn = VarietyProvider.DoesFoodHasVarietyValueForPawn(this.Pawn, eatenFood, out NoVarietyReason noVarietyReason);

            if (!hasVarietyValueForPawn && noVarietyReason != NoVarietyReason.NA)
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

        private void RemoveOldestFoodSourceFromMemory()
        {
            EatenFoodByPawn oldestEatenFood = this.foodSourcesByOrderV2.Dequeue();
            string oldestFoodSourceKey = oldestEatenFood.Source.GetFoodSourceKey();
            if (this.foodsVarietyInfos.TryGetValue(oldestFoodSourceKey, out FoodVarietyInfo foodSourceInfoForPawn))
            {
                foodSourceInfoForPawn.CountInMemory--;
                foodSourceInfoForPawn.FoodSources.Remove(oldestEatenFood.Source);
                if (foodSourceInfoForPawn.CountInMemory == 0)
                {
                    this.foodsVarietyInfos.Remove(oldestFoodSourceKey);
                    this.totalVariety--;
                    this.KeysOfFoodSourcesWithVariety.Remove(oldestFoodSourceKey);
                }
                else if (foodSourceInfoForPawn.FoodSources[0].IsForgotten)
                {
                    this.totalVariety--;
                }
            }
            else 
            {
                this.foodsNoVarietyReasons.Remove(oldestEatenFood);
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
                    if (tempList != null)
                    {
                        this.foodSourcesByOrderV2 = new Queue<EatenFoodByPawn>(tempList.Where(x => IsEatenFoodSourceValid(x)).Select(x => new EatenFoodByPawn(this.pawn, x)));
                    }

                    if (tempListV2 != null)
                    {
                        this.foodSourcesByOrderV2 = new Queue<EatenFoodByPawn>(tempListV2.Where(x => IsEatenFoodSourceByPawnValid(x)));
                    }

                    this.foodsVarietyInfos = new Dictionary<string, FoodVarietyInfo>(this.maxEatenFoodSourceInMemory + 1);
                    this.foodsNoVarietyReasons = new Dictionary<EatenFoodByPawn, NoVarietyReason>(this.maxEatenFoodSourceInMemory + 1);
                    this.KeysOfFoodSourcesWithVariety = new HashSet<string>();

                    this.ReCount();
                }
            }
        }

        private bool IsEatenFoodSourceByPawnValid(EatenFoodByPawn eatenFoodSourceByPawn)
        {
            if (eatenFoodSourceByPawn == null)
            {
                Log.Warning("DietTracker:: Loaded a null eaten food source by pawn.");
                return false;
            }

            if (eatenFoodSourceByPawn.Pawn == null)
            {
                Log.Warning($"DietTracker:: Loaded eaten food source by pawn with null pawn.");
                return false;
            }

            return IsEatenFoodSourceValid(eatenFoodSourceByPawn.Source);
        }

        private bool IsEatenFoodSourceValid(EatenFoodSource eatenFoodSource)
        {
            if (eatenFoodSource == null)
            {
                Log.Warning("DietTracker:: Loaded a null eaten food source.");
                return false;
            }

            if (eatenFoodSource.IsForgotten)
            {
                return true;
            }

            if (eatenFoodSource.ThingLabel == null)
            {
                Log.Warning("DietTracker:: Loaded a eaten food source with a null thing label");
                return false;
            }

            if (eatenFoodSource.ThingDef == null)
            {
                Log.Warning("DietTracker:: Loaded a eaten food source with a null thing def");
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
