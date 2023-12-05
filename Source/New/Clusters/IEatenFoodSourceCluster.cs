namespace VarietyMatters.New
{
    using System.Collections.Generic;
    using VarietyMatters.New.Clusters;

    internal interface IEatenFoodSourceCluster
    {
        FoodClusterType FoodClusterType { get; }

        IReadOnlyList<DietTracker.FoodVarietyInfo> GetFoodSourceInfoForPawn(EatenFoodSource foodSource);
    }
}