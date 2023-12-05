namespace VarietyMatters.New
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Verse;

    public class DietTrackerV2 : IExposable
    {
        private Pawn pawn;
        private Dictionary<string, FoodSourceInfoForPawn> foodSourcesInfoForPawn;
        private int maxEatenFoodSourceInMemory;
        private int totalVariety;
        private Queue<EatenFoodSource> foodSourcesByOrder;

        public DietTracker()
        {
        }

        public DietTracker(Pawn pawn)
        {
            this.pawn = pawn;
            this.maxEatenFoodSourceInMemory = GetMaxEatenFoodSourceInMemory(pawn);

            this.foodSourcesInfoForPawn = new Dictionary<string, FoodSourceInfoForPawn>(this.maxEatenFoodSourceInMemory + 1);
            this.foodSourcesByOrder = new Queue<EatenFoodSource>(this.maxEatenFoodSourceInMemory + 1);
            this.totalVariety = 0;

            this.AddForgottenFoods();
        }

        public Pawn Pawn => this.pawn;

        public int TotalVariety => this.totalVariety;

        public IEnumerable<EatenFoodSource> EatenFoodSourcesByOrder => this.foodSourcesByOrder;

        public EatenFoodSource mostRecentEatenFoodSource;
        public EatenFoodSource MostRecentEatenFoodSource => this.mostRecentEatenFoodSource;

        public IEnumerable<(EatenFoodSource, FoodSourceInfoForPawn)> GetEatenFoodSourcesInfoForPawnInOrderOfIngestion()
        {
            return this.foodSourcesByOrder.Select(x => (x, this.foodSourcesInfoForPawn[x.GetFoodSourceKey()]));
        }

        private void AddForgottenFoods()
        {
            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(this.pawn);

            for (int i = 0; i < varietyExpectation; i++)
            {
                var foodSource = FoodSourceFactory.GetForgottenFoodSource();

                this.AddFoodSourceToMemory(foodSource);
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

            while (this.foodSourcesByOrder.Count > this.maxEatenFoodSourceInMemory)
            {
                this.RemoveOldestFoodSourceFromMemory();
            }
        }

        public void AddFoodSource(EatenFoodSource foodSource)
        {
            this.AddFoodSourceToMemory(foodSource);

            if (this.foodSourcesByOrder.Count > this.maxEatenFoodSourceInMemory)
            {
                this.RemoveOldestFoodSourceFromMemory();
            }

            if (!foodSource.IsForgotten
                && this.OldestFoodSourceIsForgotten())
            {
                this.RemoveOldestFoodSourceFromMemory();
            }
        }

        private bool OldestFoodSourceIsForgotten()
        {
            return this.foodSourcesByOrder.Peek().IsForgotten;
        }

        public void ReCount()
        {
            this.totalVariety = 0;

            this.foodSourcesInfoForPawn.Clear();

            foreach (EatenFoodSource foodSource in this.foodSourcesByOrder)
            {
                if (foodSource.ThingLabel == null && !foodSource.IsForgotten)
                {
                    Log.Warning("DietTracker:: Found food source with null thing label while recounting");
                    continue;
                }

                this.AddFoodSourceToCount(foodSource);
            }
        }

        private void AddFoodSourceToMemory(EatenFoodSource foodSource)
        {
            this.foodSourcesByOrder.Enqueue(foodSource);
            this.mostRecentEatenFoodSource = foodSource;

            this.AddFoodSourceToCount(foodSource);
        }

        private void AddFoodSourceToCount(EatenFoodSource foodSource)
        {
            string foodSourceKey = foodSource.GetFoodSourceKey();

            if (this.foodSourcesInfoForPawn.TryGetValue(foodSourceKey, out FoodSourceInfoForPawn foodSourceInfoForPawn))
            {
                foodSourceInfoForPawn.CountInMemory++;
                foodSourceInfoForPawn.FoodSources.Add(foodSource);

                if (foodSource.IsForgotten)
                {
                    this.totalVariety++;
                }
            }
            else
            {
                bool hasVarietyValueForPawn = VarietyProvider.DoesFoodHasVarietyValueForPawn(this.Pawn, foodSource, out NoVarietyReason noVarietyReason);
                
                if (!hasVarietyValueForPawn && noVarietyReason != NoVarietyReason.NA)
                {
                    Log.Warning("DietTracker:: Variety reason mismatch");
                }

                this.foodSourcesInfoForPawn[foodSourceKey] = new FoodSourceInfoForPawn
                {
                    FoodSources = new List<EatenFoodSource> { foodSource },
                    CountInMemory = 1,
                    HasVarietyValueForPawn = hasVarietyValueForPawn,
                    NoVarietyReason = noVarietyReason,
                };

                if (hasVarietyValueForPawn)
                {
                    this.totalVariety++;
                }
            }
        }

        private void RemoveOldestFoodSourceFromMemory()
        {
            EatenFoodSource oldestFoodSource = this.foodSourcesByOrder.Dequeue();
            string oldestFoodSourceKey = oldestFoodSource.GetFoodSourceKey();
            FoodSourceInfoForPawn foodSourceInfoForPawn = this.foodSourcesInfoForPawn[oldestFoodSourceKey];
            foodSourceInfoForPawn.CountInMemory--;
            foodSourceInfoForPawn.FoodSources.Remove(oldestFoodSource);
            if (foodSourceInfoForPawn.CountInMemory == 0)
            {
                this.foodSourcesInfoForPawn.Remove(oldestFoodSourceKey);
                if (foodSourceInfoForPawn.HasVarietyValueForPawn)
                {
                    this.totalVariety--;
                }
            }
            else if (foodSourceInfoForPawn.FoodSources[0].IsForgotten)
            {
                this.totalVariety--;
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look<Pawn>(ref this.pawn, "pawn");
            Scribe_References.Look<EatenFoodSource>(ref this.mostRecentEatenFoodSource, "mostRecentEatenFoodSource", saveDestroyedThings: true);
            Scribe_Values.Look<int>(ref this.maxEatenFoodSourceInMemory, "maxEatenFoodSourceInMemory");

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<EatenFoodSource> tempList = this.foodSourcesByOrder.ToList();
                Scribe_Collections.Look(ref tempList, "foodSourcesByOrder", LookMode.Reference);
            }

            if (Scribe.mode != LoadSaveMode.Saving)
            {
                List<EatenFoodSource> tempList = null;
                Scribe_Collections.Look(ref tempList, "foodSourcesByOrder", LookMode.Reference);
                if (tempList != null)
                {
                    this.foodSourcesByOrder = new Queue<EatenFoodSource>(tempList.Where(x => IsEatenFoodSourceValid(x)));
                }
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.foodSourcesInfoForPawn = new Dictionary<string, FoodSourceInfoForPawn>(this.maxEatenFoodSourceInMemory + 1);

                this.ReCount();
            }
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

        public class FoodSourceInfoForPawn
        {
            public List<EatenFoodSource> FoodSources { get; set; }

            public int CountInMemory { get; set; }

            public bool HasVarietyValueForPawn { get; set; }

            public NoVarietyReason NoVarietyReason { get; set; }
        }
    }
}
