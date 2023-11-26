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
        public static EatenFoodSource ForgottenEatenFoodSource = new EatenFoodSource() { IsForgotton = true, uniqueId = "EatenFoodSource_ForgottenFood", ThingLabel = "Forgotten food", ingredients = new List<ThingDef>() };

        public EatenFoodSource()
        {
            FastLazy<string> rottenString = new FastLazy<string>(() => this.IsRotten ? "Rotten " : string.Empty);

            FastLazy<string> ingredientsString = new FastLazy<string>(() => this.IngredientsDefs.Count > 0 ? string.Join(", ", this.IngredientsDefs.Select(x => x.label)) : "Unknown");

            this.MealType = new FastLazy<string>(() => this.GetMealType());

            this.MealTypeAsString = new FastLazy<string>(() => $"{rottenString}{this.MealType}");

            this.MealAsString = new FastLazy<string>(() => $"{rottenString}{this.ThingLabel}");

            this.MealNameAndTypeAsString = new FastLazy<string>(() => $"{rottenString}{this.ThingLabel} (type: {this.MealTypeAsString})");

            this.IngredientsAsString = new FastLazy<string>(() => $"{rottenString}meal, ingredients: {ingredientsString}");

            this.MealTypeAndIngredientsAsString = new FastLazy<string>(() => $"{this.MealTypeAsString}, ingredients: {ingredientsString}");

            this.MealAndIngredientsAsString = new FastLazy<string>(() => $"{this.MealAsString}, ingredients: {ingredientsString}");

            this.MealNameAndTypeAndIngredientsAsString = new FastLazy<string>(() => $"{this.MealNameAndTypeAsString}, ingredients: {ingredientsString}");

            this.HasMealType = new FastLazy<bool>(() => !this.isForgotton && this.ThingLabel != this.MealType);
        }

        public EatenFoodSource(Thing foodSourceThing)
            : this()
        {
            if (foodSourceThing == null)
            {
                throw new ArgumentNullException(nameof(foodSourceThing));
            }

            this.Thing = foodSourceThing;

            this.IsForgotton = false;

            this.uniqueId = "EatenFoodSource_" + foodSourceThing.GetUniqueLoadID();

            this.ThingDef = foodSourceThing.def;

            if (this.ThingDef == null)
            {
                throw new ArgumentNullException(nameof(this.ThingDef));
            }

            this.ThingLabel = foodSourceThing.Label;

            if (this.ThingLabel == null)
            {
                throw new ArgumentNullException(nameof(this.ThingLabel));
            }

            this.IngredientsDefs = ThingCompUtility.TryGetComp<CompIngredients>(foodSourceThing)?.ingredients?.ToList() ?? new List<ThingDef>();
            this.IngredientsDefs.SortBy(x => x.label);

            this.IsRotten = RottableUtility.IsNotFresh(foodSourceThing);
            this.IsAcceptableToCarnivores = new FastLazy<bool>(() => FoodUtility.AcceptableCarnivore(foodSourceThing));
            this.IsAcceptableToVegetarians = new FastLazy<bool>(() => FoodUtility.AcceptableVegetarian(foodSourceThing));
            this.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient = new FastLazy<bool>(() => FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(foodSourceThing));
            this.MeatSourceCategory = new FastLazy<MeatSourceCategory>(() => FoodUtility.GetMeatSourceCategory(foodSourceThing.def));
            this.FoodPreferability = foodSourceThing.def.ingestible.preferability;

            this.IsFungus = foodSourceThing.def.IsFungus;
        }

        private Thing thing;
        public Thing Thing
        {
            get => this.thing;
            private set => this.thing = value;
        }

        private ThingDef thingDef;
        public ThingDef ThingDef
        {
            get => this.thingDef;
            private set => this.thingDef = value;
        }

        private string thingLabel;
        public string ThingLabel
        {
            get => this.thingLabel;
            private set => this.thingLabel = value;
        }

        private FoodPreferability foodPreferability;
        public FoodPreferability FoodPreferability
        {
            get => this.foodPreferability;
            private set => this.foodPreferability = value;
        }

        private List<ThingDef> ingredients;
        public List<ThingDef> IngredientsDefs
        {
            get => this.ingredients;
            private set => this.ingredients = value;
        }

        private bool isForgotton;
        public bool IsForgotton
        {
            get => this.isForgotton;
            private set => this.isForgotton = value;
        }

        private bool isRotten;
        public bool IsRotten
        {
            get => this.isRotten;
            private set => this.isRotten = value;
        }

        private FastLazy<bool> isAcceptableToCarnivores;
        public FastLazy<bool> IsAcceptableToCarnivores
        {
            get => this.isAcceptableToCarnivores;
            private set => this.isAcceptableToCarnivores = value;
        }

        private FastLazy<bool> isAcceptableToVegetarians;
        public FastLazy<bool> IsAcceptableToVegetarians
        {
            get => this.isAcceptableToVegetarians;
            private set => this.isAcceptableToVegetarians = value;
        }

        private FastLazy<bool> isHumanlikeCorpseOrHumanlikeMeatOrIngredient;
        public FastLazy<bool> IsHumanlikeCorpseOrHumanlikeMeatOrIngredient
        {
            get => this.isHumanlikeCorpseOrHumanlikeMeatOrIngredient;
            private set => this.isHumanlikeCorpseOrHumanlikeMeatOrIngredient = value;
        }

        private FastLazy<MeatSourceCategory> meatSourceCategory;
        public FastLazy<MeatSourceCategory> MeatSourceCategory
        {
            get => this.meatSourceCategory;
            private set => this.meatSourceCategory = value;
        }

        private bool isFungus;
        public bool IsFungus
        {
            get => this.isFungus;
            private set => this.isFungus = value;
        }

        public FastLazy<string> IngredientsAsString { get; }

        public FastLazy<string> MealAndIngredientsAsString { get; }

        public FastLazy<string> MealTypeAndIngredientsAsString { get; }

        public FastLazy<string> MealNameAndTypeAndIngredientsAsString { get; }

        public FastLazy<string> MealAsString { get; }

        public FastLazy<string> MealNameAndTypeAsString { get; }

        public FastLazy<string> MealType { get; }

        public FastLazy<string> MealTypeAsString { get; }

        public FastLazy<bool> HasMealType { get; }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref this.thingDef, "thingDef");
            Scribe_Values.Look(ref this.thingLabel, "thingLabel");
            Scribe_Values.Look(ref this.isForgotton, "isForgotton");
            Scribe_Values.Look(ref this.foodPreferability, "foodPreferability");
            Scribe_Collections.Look(ref this.ingredients, "ingredients", LookMode.Def);
            Scribe_Values.Look(ref this.isRotten, "isRotten");
            Scribe_Deep.Look(ref this.isAcceptableToCarnivores, "isAcceptableToCarnivores");
            Scribe_Deep.Look(ref this.isAcceptableToVegetarians, "isAcceptableToVegetarians");
            Scribe_Deep.Look(ref this.isHumanlikeCorpseOrHumanlikeMeatOrIngredient, "isHumanlikeCorpseOrHumanlikeMeatOrIngredient");
            Scribe_Deep.Look(ref this.meatSourceCategory, "meatSourceCategory");
            Scribe_Values.Look(ref this.isFungus, "isFungus");
            Scribe_Values.Look(ref this.uniqueId, "uniqueId");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.thingLabel == null && !this.isForgotton)
                {
                    Log.Warning("Loaded EatenFoodSource with null thingLabel");
                }
            }
        }

        public string GetFoodSourceKey()
        {
            if (this.IsForgotton)
            {
                return "Forgotten meal";
            }

            if (!ModSettings_VarietyMatters.clusterSimilarMealsTogether || !this.HasMealType)
            {
                switch (ModSettings_VarietyMatters.foodTrackingType)
                {
                    case FoodTrackingType.ByMealNames:
                        return this.MealAsString;
                    case FoodTrackingType.ByMealIngredientsCombination:
                        return this.IngredientsAsString;
                    case FoodTrackingType.ByMealNamesAndIngredients:
                        if (this.IngredientsDefs.Count > 0)
                        {
                            return this.MealAndIngredientsAsString;
                        }
                        else
                        {
                            return this.MealAsString;
                        }
                    default:
                        throw new Exception($"Not valid FoodTrackingType: {ModSettings_VarietyMatters.foodTrackingType}");
                }
            }
            else
            {
                switch (ModSettings_VarietyMatters.foodTrackingType)
                {
                    case FoodTrackingType.ByMealNames:
                        return this.MealTypeAsString;
                    case FoodTrackingType.ByMealIngredientsCombination:
                        return this.IngredientsAsString;
                    case FoodTrackingType.ByMealNamesAndIngredients:
                        return this.MealTypeAndIngredientsAsString;
                    default:
                        throw new Exception($"Not valid FoodTrackingType: {ModSettings_VarietyMatters.foodTrackingType}");
                }
            }
        }

        public override string ToString()
        {
            if (this.IsForgotton)
            {
                return "Forgotten meal";
            }

            if (!ModSettings_VarietyMatters.clusterSimilarMealsTogether || !this.HasMealType)
            {
                switch (ModSettings_VarietyMatters.foodTrackingType)
                {
                    case FoodTrackingType.ByMealNames:
                        return this.MealAsString;
                    case FoodTrackingType.ByMealIngredientsCombination:
                        return this.IngredientsAsString;
                    case FoodTrackingType.ByMealNamesAndIngredients:
                        if (this.IngredientsDefs.Count > 0)
                        {
                            return this.MealAndIngredientsAsString;
                        }
                        else
                        {
                            return this.MealAsString;
                        }
                    default:
                        throw new Exception($"Not valid FoodTrackingType: {ModSettings_VarietyMatters.foodTrackingType}");
                }
            }
            else
            {
                switch (ModSettings_VarietyMatters.foodTrackingType)
                {
                    case FoodTrackingType.ByMealNames:
                        return this.MealNameAndTypeAsString;
                    case FoodTrackingType.ByMealIngredientsCombination:
                        return this.IngredientsAsString;
                    case FoodTrackingType.ByMealNamesAndIngredients:
                        if (this.IngredientsDefs.Count > 0)
                        {
                            return this.MealNameAndTypeAndIngredientsAsString;
                        }
                        else
                        {
                            return this.MealNameAndTypeAsString;
                        }
                    default:
                        throw new Exception($"Not valid FoodTrackingType: {ModSettings_VarietyMatters.foodTrackingType}");
                }
            }
        }

        private string uniqueId;

        public string GetUniqueLoadID()
        {
            return this.uniqueId;
        }

        private string GetMealType()
        {
            if (this.foodPreferability < FoodPreferability.MealSimple)
            {
                // It's not a meal, return the same type as the name of the meal.
                return this.ThingLabel;
            }

            var mealLabelWithoutQualifiers = MealQualifiers.RemoveMealQualifiersFromMealLabel(this.ThingLabel);

            if (mealLabelWithoutQualifiers == "meal")
            {
                return "regular meal";
            }

            return mealLabelWithoutQualifiers;
        }
    }
}
