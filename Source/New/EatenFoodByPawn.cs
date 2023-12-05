namespace VarietyMatters.New
{
    using RimWorld;
    using VarietyMatters.New.Performance;
    using Verse;

    public class EatenFoodByPawn : IExposable
    {
        private Pawn pawn;
        private EatenFoodSource eatenFoodSource;

        private FastLazy<bool> isVeneratedAnimalMeatOrCorpseOrHasIngredients;
        private FastLazy<float> moodFromIngesting;

        public EatenFoodByPawn()
        {
        }

        public EatenFoodByPawn(Pawn pawn, EatenFoodSource eatenFoodSource)
        {
            this.pawn = pawn;
            this.eatenFoodSource = eatenFoodSource;

            this.isVeneratedAnimalMeatOrCorpseOrHasIngredients = new FastLazy<bool>(() => eatenFoodSource.Thing != null ? FoodUtility.IsVeneratedAnimalMeatOrCorpseOrHasIngredients(eatenFoodSource.Thing, pawn) : false);
            this.moodFromIngesting = new FastLazy<float>(() => eatenFoodSource.Thing != null ? FoodUtility.MoodFromIngesting(pawn, eatenFoodSource.Thing, eatenFoodSource.ThingDef) : 0);
        }

        public float MoodFromIngesting  => this.moodFromIngesting;

        public bool IsVeneratedAnimalMeatOrCorpseOrHasIngredients => this.isVeneratedAnimalMeatOrCorpseOrHasIngredients;

        public EatenFoodSource Source => this.eatenFoodSource;

        public Pawn Pawn => this.pawn;

        public void ExposeData()
        {
            Scribe_References.Look<Pawn>(ref this.pawn, "pawn");
            Scribe_References.Look<EatenFoodSource>(ref this.eatenFoodSource, "eatenFoodSource");

            Scribe_Deep.Look<FastLazy<bool>>(ref this.isVeneratedAnimalMeatOrCorpseOrHasIngredients, "isVeneratedAnimalMeatOrCorpseOrHasIngredients");
            Scribe_Deep.Look<FastLazy<float>>(ref this.moodFromIngesting, "moodFromIngesting");
        }
    }
}
