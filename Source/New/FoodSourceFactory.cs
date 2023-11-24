namespace VarietyMatters.New
{
    using System.Collections.Generic;
    using Verse;

    public static class FoodSourceFactory
    {
        private static readonly Dictionary<Thing, EatenFoodSource> foodSources = new Dictionary<Thing, EatenFoodSource>();

        public static void Init(IEnumerable<EatenFoodSource> eatenFoodSources)
        {
            foreach (EatenFoodSource eatenFoodSource in eatenFoodSources)
            {
                if (eatenFoodSource.Thing != null)
                {
                    foodSources[eatenFoodSource.Thing] = eatenFoodSource;
                }
            }
        }

        public static EatenFoodSource CreateOrGetFoodSourceFromThing(Thing foodSourceThing)
        {
            if (!foodSources.TryGetValue(foodSourceThing, out EatenFoodSource foodSource))
            {
                foodSource = new EatenFoodSource(foodSourceThing);
                foodSources[foodSourceThing] = foodSource;
            }

            return foodSource;
        }
    }
}
