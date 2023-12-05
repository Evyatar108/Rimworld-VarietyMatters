namespace VarietyMatters.New
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using VarietyMatters.New.Clusters;
    using static VarietyMatters.New.DietTracker;

    internal abstract class EatenFoodSourceCluster 
    {
        private FoodClusterType foodClusterType;
        private Dictionary<FoodSourceInfoForPawnKey, FoodVarietyInfo> foodSourceInfoForPawns;

        public EatenFoodSourceCluster(FoodClusterType foodClusterType)
        {
            this.foodClusterType = foodClusterType;
        }

        public FoodClusterType FoodClusterType => this.foodClusterType;

        public IReadOnlyList<FoodVarietyInfo> GetFoodSourceInfoForPawn(EatenFoodSource foodSource)
        {
            return new List<FoodVarietyInfo> { this.foodSourceInfoForPawns[foodSource.MealType] };
        }

        public void AddFoodSource(EatenFoodSource foodSource)
        {
            var keys = GetKeys(foodSource);

            var key = new FoodSourceInfoForPawnKey(foodSource.MealType);
            if (!this.foodSourceInfoForPawns.TryGetValue(key, out FoodVarietyInfo foodSourceInfoForPawn))
            {
                foodSourceInfoForPawn = new FoodVarietyInfo
                {
                    FoodSources = new List<EatenFoodSource> { foodSource },
                    CountInMemory = 1,
                    HasVarietyValueForPawn = hasVariety,
                    NoVarietyReason = noVarietyReason,
                };
            }
        }

        protected abstract IEnumerable<FoodSourceInfoForPawnKey> GetKeys(EatenFoodSource foodSource);
    }
}
