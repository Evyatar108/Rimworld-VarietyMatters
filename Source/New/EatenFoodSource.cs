namespace VarietyMatters.New
{
    using RimWorld;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using VarietyMatters.New.Performance;
    using Verse;

    public class EatenFoodSource : IExposable, ILoadReferenceable
    {
        public EatenFoodSource()
        {
        }

        public EatenFoodSource(Thing foodSourceThing)
        {
            this.Thing = foodSourceThing;

            this.InitPropertiesWithThing(foodSourceThing);
        }

        private void InitPropertiesWithThing(Thing foodSourceThing)
        {
            this.uniqueId = "EatenFoodSource_" + foodSourceThing.GetUniqueLoadID();

            IngredientsDefs = ThingCompUtility.TryGetComp<CompIngredients>(foodSourceThing)?.ingredients?.ToList() ?? new List<ThingDef>();
            IngredientsDefs.SortBy(x => x.label);

            IsRotten = RottableUtility.IsNotFresh(foodSourceThing);
            IsAcceptableToCarnivores = new FastLazy<bool>(() => FoodUtility.AcceptableCarnivore(foodSourceThing));
            IsAcceptableToVegetarians = new FastLazy<bool>(() => FoodUtility.AcceptableVegetarian(foodSourceThing));
            IsHumanlikeCorpseOrHumanlikeMeatOrIngredient = new FastLazy<bool>(() => FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(foodSourceThing));
            MeatSourceCategory = new FastLazy<MeatSourceCategory>(() => FoodUtility.GetMeatSourceCategory(foodSourceThing.def));
            FoodPreferability = foodSourceThing.def.ingestible.preferability;

            IsFungus = foodSourceThing.def.IsFungus;

            string rottenString = IsRotten ? "Rotten " : string.Empty;
            string ingredientsString = IngredientsDefs.Count > 0 ? string.Join(", ", IngredientsDefs.Select(x => x.label)) : "Unknown";

            MealAsString = $"{rottenString}{foodSourceThing.def.label}";

            IngredientsAsString = new FastLazy<string>(() => $"{rottenString}meal, ingredients: {ingredientsString}");

            MealAndIngredientsAsString = new FastLazy<string>(() => $"{MealAsString}, ingredients: {ingredientsString}");
        }

        private Thing thing;
        public Thing Thing
        {
            get => thing;
            private set => thing = value;
        }

        private ThingDef thingDef;
        public ThingDef ThingDef
        {
            get => thingDef;
            private set => thingDef = value;
        }

        private string thingLabel;
        public string ThingLabel
        {
            get => thingLabel;
            private set => thingLabel = value;
        }

        private FoodPreferability foodPreferability;
        public FoodPreferability FoodPreferability
        {
            get => foodPreferability;
            private set => foodPreferability = value;
        }

        private List<ThingDef> ingredients;
        public List<ThingDef> IngredientsDefs
        {
            get => ingredients;
            private set => ingredients = value;
        }

        private bool isRotten;
        public bool IsRotten
        {
            get => isRotten;
            private set => isRotten = value;
        }

        private FastLazy<bool> isAcceptableToCarnivores;
        public FastLazy<bool> IsAcceptableToCarnivores
        {
            get => isAcceptableToCarnivores;
            private set => isAcceptableToCarnivores = value;
        }

        private FastLazy<bool> isAcceptableToVegetarians;
        public FastLazy<bool> IsAcceptableToVegetarians
        {
            get => isAcceptableToVegetarians;
            private set => isAcceptableToVegetarians = value;
        }

        private FastLazy<bool> isHumanlikeCorpseOrHumanlikeMeatOrIngredient;
        public FastLazy<bool> IsHumanlikeCorpseOrHumanlikeMeatOrIngredient
        {
            get => isHumanlikeCorpseOrHumanlikeMeatOrIngredient;
            private set => isHumanlikeCorpseOrHumanlikeMeatOrIngredient = value;
        }

        private FastLazy<bool> isInsectMeat;
        public FastLazy<bool> IsInsectMeat
        {
            get => isInsectMeat;
            private set => isInsectMeat = value;
        }

        private FastLazy<MeatSourceCategory> meatSourceCategory;
        public FastLazy<MeatSourceCategory> MeatSourceCategory
        {
            get => meatSourceCategory;
            private set => meatSourceCategory = value;
        }

        private bool isFungus;
        public bool IsFungus
        {
            get => isFungus;
            private set => isFungus = value;
        }

        private FastLazy<string> ingredientsAsString;
        public FastLazy<string> IngredientsAsString
        {
            get => ingredientsAsString;
            private set => ingredientsAsString = value;
        }

        private FastLazy<string> mealAndIngredientsAsString;
        public FastLazy<string> MealAndIngredientsAsString
        {
            get => mealAndIngredientsAsString;
            private set => mealAndIngredientsAsString = value;
        }

        private string mealAsString;
        public string MealAsString
        {
            get => mealAsString;
            private set => mealAsString = value;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref thingLabel, "thingLabel");
            Scribe_Values.Look(ref foodPreferability, "foodPreferability");
            Scribe_Collections.Look(ref ingredients, "ingredients", LookMode.Def);
            Scribe_Values.Look(ref isRotten, "isRotten");
            Scribe_Deep.Look(ref isAcceptableToCarnivores, "isAcceptableToCarnivores");
            Scribe_Deep.Look(ref isAcceptableToVegetarians, "isAcceptableToVegetarians");
            Scribe_Deep.Look(ref isHumanlikeCorpseOrHumanlikeMeatOrIngredient, "isHumanlikeCorpseOrHumanlikeMeatOrIngredient");
            Scribe_Deep.Look(ref isInsectMeat, "isInsectMeat");
            Scribe_Deep.Look(ref meatSourceCategory, "meatSourceCategory");
            Scribe_Values.Look(ref isFungus, "isFungus");
            Scribe_Deep.Look(ref ingredientsAsString, "ingredientsAsString");
            Scribe_Deep.Look(ref mealAndIngredientsAsString, "mealAndIngredientsAsString");
            Scribe_Values.Look(ref mealAsString, "mealAsString");
            Scribe_Values.Look(ref uniqueId, "uniqueId");
        }

        public string GetFoodSourceKey(FoodTrackingType foodTrackingType)
        {
            switch (foodTrackingType)
            {
                case FoodTrackingType.ByMeal:
                    return MealAsString;
                case FoodTrackingType.ByIngredients:
                    return IngredientsAsString;
                case FoodTrackingType.ByMealAndIngredients:
                    return MealAndIngredientsAsString;
                default:
                    throw new Exception($"Not valid FoodTrackingType: {foodTrackingType}");
            }
        }

        public string ToString(FoodTrackingType foodTrackingType)
        {
            switch (foodTrackingType)
            {
                case FoodTrackingType.ByMeal:
                    return MealAsString;
                case FoodTrackingType.ByIngredients:
                case FoodTrackingType.ByMealAndIngredients:
                    if (IngredientsDefs.Count > 0)
                    {
                        return MealAndIngredientsAsString;
                    }
                    else
                    {
                        return MealAsString;
                    }
                default:
                    throw new Exception($"Not valid FoodTrackingType: {foodTrackingType}");
            }
        }

        private string uniqueId;

        public string GetUniqueLoadID()
        {
            return this.uniqueId;
        }
    }
}
