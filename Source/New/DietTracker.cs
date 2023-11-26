﻿namespace VarietyMatters.New
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Verse;

    public class DietTracker : IExposable
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

            AddForgottenFoods();
        }

        public Pawn Pawn => this.pawn;

        public int TotalVariety => totalVariety;

        public IEnumerable<EatenFoodSource> EatenFoodSourcesByOrder => this.foodSourcesByOrder;

        public EatenFoodSource mostRecentEatenFoodSource;
        public EatenFoodSource MostRecentEatenFoodSource => mostRecentEatenFoodSource;

        public IEnumerable<(EatenFoodSource, FoodSourceInfoForPawn)> GetEatenFoodSourcesInfoForPawnInOrderOfIngestion()
        {
            return this.foodSourcesByOrder.Select(x => (x, this.foodSourcesInfoForPawn[x.GetFoodSourceKey()]));
        }

        private void AddForgottenFoods()
        {
            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(pawn);

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

            if (this.foodSourcesByOrder.Count > maxEatenFoodSourceInMemory)
            {
                this.RemoveOldestFoodSourceFromMemory();
            }

            if (!foodSource.IsForgotton
                && this.OldestFoodSourceIsForgotten())
            {
                this.RemoveOldestFoodSourceFromMemory();
            }
        }

        private bool OldestFoodSourceIsForgotten()
        {
            return this.foodSourcesByOrder.Peek().IsForgotton;
        }

        public void ReCount()
        {
            this.totalVariety = 0;

            this.foodSourcesInfoForPawn.Clear();

            foreach (EatenFoodSource foodSource in this.foodSourcesByOrder)
            {
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

                if (foodSource.IsForgotton)
                {
                    this.totalVariety++;
                }
            }
            else
            {
                bool hasVarietyValueForPawn = VarietyProvider.DoesFoodHasVarietyValueForPawn(this.Pawn, foodSource, out NoVarietyReason noVarietyReason);
                
                if (!hasVarietyValueForPawn && noVarietyReason != NoVarietyReason.NA)
                {
                    Log.Warning("Variety reason mismatch");
                }

                this.foodSourcesInfoForPawn[foodSourceKey] = new FoodSourceInfoForPawn
                {
                    FoodSource = foodSource,
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
            if (foodSourceInfoForPawn.CountInMemory == 0)
            {
                this.foodSourcesInfoForPawn.Remove(oldestFoodSourceKey);
                if (foodSourceInfoForPawn.HasVarietyValueForPawn)
                {
                    this.totalVariety--;
                }
            }
            else if (foodSourceInfoForPawn.FoodSource.IsForgotton)
            {
                this.totalVariety--;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look<int>(ref this.totalVariety, "totalVariety");
            Scribe_References.Look<Pawn>(ref this.pawn, "pawn");
            Scribe_References.Look<EatenFoodSource>(ref this.mostRecentEatenFoodSource, "mostRecentEatenFoodSource", saveDestroyedThings: true);
            Scribe_Collections.Look<string, FoodSourceInfoForPawn>(ref this.foodSourcesInfoForPawn, "foodSourcesInfoForPawn", keyLookMode: LookMode.Value, valueLookMode: LookMode.Deep);
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
                    foodSourcesByOrder = new Queue<EatenFoodSource>(tempList);
                }
            }
        }

        public class FoodSourceInfoForPawn :  IExposable
        {
            private EatenFoodSource foodSource;
            public EatenFoodSource FoodSource
            {
                get => foodSource;
                set => foodSource = value;
            }

            private int countInMemory;
            public int CountInMemory
            {
                get => countInMemory;
                set => countInMemory = value;
            }

            private bool hasVarietyValueForPawn;
            public bool HasVarietyValueForPawn
            {
                get => hasVarietyValueForPawn;
                set => hasVarietyValueForPawn = value;
            }

            private NoVarietyReason noVarietyReason;
            public NoVarietyReason NoVarietyReason
            {
                get => noVarietyReason;
                set => noVarietyReason = value;
            }

            public void ExposeData()
            {
                Scribe_References.Look(ref foodSource, "foodSource", saveDestroyedThings: true);
                Scribe_Values.Look(ref countInMemory, "countInMemory");
                Scribe_Values.Look(ref hasVarietyValueForPawn, "hasVarietyValueForPawn");
                Scribe_Values.Look(ref noVarietyReason, "noVarietyReason", NoVarietyReason.NA);
            }
        }
    }
}
