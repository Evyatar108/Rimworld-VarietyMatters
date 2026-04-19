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
        public static EatenFoodSource ForgottenEatenFoodSource = CreateForgottenFoodSource();

        private static EatenFoodSource CreateForgottenFoodSource()
        {
            var forgotten = new EatenFoodSource();
            forgotten.IsForgotten = true;
            forgotten.uniqueId = "EatenFoodSource_ForgottenFood";
            forgotten.ThingLabel = "Forgotten food";
            forgotten.ingredients = new List<ThingDef>();
            return forgotten;
        }

        public EatenFoodSource()
        {
            // Use consolidated food strings to reduce FastLazy object proliferation
            consolidatedStrings = new FastLazy<ConsolidatedFoodStrings>(() => new ConsolidatedFoodStrings(this));
        }

        public EatenFoodSource(Thing foodSourceThing)
            : this()
        {
            if (foodSourceThing == null)
            {
                throw new ArgumentNullException(nameof(foodSourceThing));
            }

            this.Thing = foodSourceThing;

            this.IsForgotten = false;

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
            this.DrugCategory = foodSourceThing.def.ingestible.drugCategory;

            this.IsFungus = foodSourceThing.def.IsFungus;

            this.AteAtTick = Current.ProgramState == ProgramState.Playing && Find.TickManager != null
                ? Find.TickManager.TicksGame
                : -1;
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

        // Tick the meal was eaten. -1 = unknown (old saves where this field didn't exist, or the
        // forgotten-food singleton). Save-compat: Scribe_Values with default -1 preserves old saves.
        private int ateAtTick = -1;
        public int AteAtTick
        {
            get => this.ateAtTick;
            private set => this.ateAtTick = value;
        }

        private FoodPreferability foodPreferability;
        public FoodPreferability FoodPreferability
        {
            get => this.foodPreferability;
            private set => this.foodPreferability = value;
        }

        private DrugCategory drugCategory;
        public DrugCategory DrugCategory
        {
            get => this.drugCategory;
            private set => this.drugCategory = value;
        }

        private List<ThingDef> ingredients;
        public List<ThingDef> IngredientsDefs
        {
            get => this.ingredients;
            private set => this.ingredients = value;
        }

        private bool isForgotten;
        public bool IsForgotten
        {
            get => this.isForgotten;
            private set => this.isForgotten = value;
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

        // Consolidated string properties using single FastLazy object
        private FastLazy<ConsolidatedFoodStrings> consolidatedStrings;

        public string IngredientsAsString => consolidatedStrings.Value.IngredientsAsString;
        public string MealAndIngredientsAsString => consolidatedStrings.Value.MealAndIngredientsAsString;
        public string MealTypeAndIngredientsAsString => consolidatedStrings.Value.MealTypeAndIngredientsAsString;
        public string MealNameAndTypeAndIngredientsAsString => consolidatedStrings.Value.MealNameAndTypeAndIngredientsAsString;
        public string MealAsString => consolidatedStrings.Value.MealAsString;
        public string MealNameAndTypeAsString => consolidatedStrings.Value.MealNameAndTypeAsString;
        public string MealType => consolidatedStrings.Value.MealType;
        public string MealTypeAsString => consolidatedStrings.Value.MealTypeAsString;
        public bool HasMealType => consolidatedStrings.Value.HasMealType;
        public string NormalizedThingName => consolidatedStrings.Value.NormalizedThingName;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref this.thingDef, "thingDef");
            Scribe_Values.Look(ref this.thingLabel, "thingLabel");
            Scribe_Values.Look(ref this.isForgotten, "isForgotton");
            Scribe_Values.Look(ref this.foodPreferability, "foodPreferability");
            // Default -1 = unknown timestamp for saves written before this field existed.
            Scribe_Values.Look(ref this.ateAtTick, "ateAtTick", -1);
            Scribe_Collections.Look(ref this.ingredients, "ingredients", LookMode.Def);
            Scribe_Values.Look(ref this.isRotten, "isRotten");
            Scribe_Deep.Look(ref this.isAcceptableToCarnivores, "isAcceptableToCarnivores");
            Scribe_Deep.Look(ref this.isAcceptableToVegetarians, "isAcceptableToVegetarians");
            Scribe_Deep.Look(ref this.isHumanlikeCorpseOrHumanlikeMeatOrIngredient, "isHumanlikeCorpseOrHumanlikeMeatOrIngredient");
            Scribe_Deep.Look(ref this.meatSourceCategory, "meatSourceCategory");
            // Do NOT save consolidatedStrings - it will be recreated on demand
            Scribe_Values.Look(ref this.isFungus, "isFungus");
            Scribe_Values.Look(ref this.uniqueId, "uniqueId");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.thingLabel == null && !this.isForgotten)
                {
                    Log.Warning("EatenFoodSource:: Loaded EatenFoodSource with null thingLabel");
                }
                
                // Always re-initialize consolidatedStrings after loading since it's not saved
                this.consolidatedStrings = new FastLazy<ConsolidatedFoodStrings>(() => new ConsolidatedFoodStrings(this));

                // Ensure ingredients list is initialized
                if (this.ingredients == null)
                {
                    this.ingredients = new List<ThingDef>();
                }

                // Re-initialize other FastLazy objects that may have lost their valueFactory during save/load
                if (this.isAcceptableToCarnivores != null && this.Thing != null)
                {
                    // Only re-initialize if we still have the Thing reference
                    var tempThing = this.Thing;
                    this.isAcceptableToCarnivores = new FastLazy<bool>(() => FoodUtility.AcceptableCarnivore(tempThing));
                    this.isAcceptableToVegetarians = new FastLazy<bool>(() => FoodUtility.AcceptableVegetarian(tempThing));
                    this.isHumanlikeCorpseOrHumanlikeMeatOrIngredient = new FastLazy<bool>(() => FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(tempThing));
                    this.meatSourceCategory = new FastLazy<MeatSourceCategory>(() => FoodUtility.GetMeatSourceCategory(tempThing.def));
                }
            }
        }

        public string GetFoodSourceKey()
        {
            if (this.IsForgotten)
            {
                return "Forgotten meal";
            }

            if (!ModSettings_VarietyMatters.clusterSimilarMealsTogether || !this.HasMealType)
            {
                switch (ModSettings_VarietyMatters.foodTrackingType)
                {
                    case FoodTrackingType.ByMealNames:
                        return this.MealAsString;
                    case FoodTrackingType.ByIngredientsCombination:
                        return this.IngredientsAsString;
                    case FoodTrackingType.ByMealNamesAndIngredientsCombination:
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
                    case FoodTrackingType.ByIngredientsCombination:
                        return this.IngredientsAsString;
                    case FoodTrackingType.ByMealNamesAndIngredientsCombination:
                        return this.MealTypeAndIngredientsAsString;
                    default:
                        throw new Exception($"Not valid FoodTrackingType: {ModSettings_VarietyMatters.foodTrackingType}");
                }
            }
        }

        public override string ToString()
        {
            if (this.IsForgotten)
            {
                return "Forgotten meal";
            }

            if (!ModSettings_VarietyMatters.clusterSimilarMealsTogether || !this.HasMealType)
            {
                switch (ModSettings_VarietyMatters.foodTrackingType)
                {
                    case FoodTrackingType.ByMealNames:
                        return this.MealAsString;
                    case FoodTrackingType.ByIngredientsCombination:
                        return this.IngredientsAsString;
                    case FoodTrackingType.ByMealNamesAndIngredientsCombination:
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
                    case FoodTrackingType.ByIngredientsCombination:
                        return this.IngredientsAsString;
                    case FoodTrackingType.ByMealNamesAndIngredientsCombination:
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

        // GetMealType method moved to ConsolidatedFoodStrings for better encapsulation
    }
}
