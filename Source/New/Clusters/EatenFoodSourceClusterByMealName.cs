namespace VarietyMatters.New
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using VarietyMatters.New.Clusters;
    using static VarietyMatters.New.DietTracker;

    internal class EatenFoodSourceClusterByMealName : IEatenFoodSourceCluster
    {
        private FoodClusterType foodClusterType;
        private Dictionary<string, FoodVarietyInfo> foodSourceInfoForPawns;

        public EatenFoodSourceClusterByMealName(FoodClusterType foodClusterType)
        {
            this.foodClusterType = foodClusterType;
        }

        public FoodClusterType FoodClusterType => this.foodClusterType;

        public IReadOnlyList<FoodVarietyInfo> GetFoodSourceInfoForPawn(EatenFoodSource foodSource)
        {
            return new List<FoodVarietyInfo> { this.foodSourceInfoForPawns[foodSource.ThingLabel] };
        }
    }
}
