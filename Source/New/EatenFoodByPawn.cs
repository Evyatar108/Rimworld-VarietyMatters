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

            this.isVeneratedAnimalMeatOrCorpseOrHasIngredients = new FastLazy<bool>(() => SafeCheckVeneratedAnimalMeat());
            this.moodFromIngesting = new FastLazy<float>(() => SafeGetMoodFromIngesting());
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

            // Ensure FastLazy objects are properly initialized after loading
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureFastLazyInitialized();
            }
        }

        private void EnsureFastLazyInitialized()
        {
            if (this.isVeneratedAnimalMeatOrCorpseOrHasIngredients == null)
            {
                this.isVeneratedAnimalMeatOrCorpseOrHasIngredients = new FastLazy<bool>(() => SafeCheckVeneratedAnimalMeat());
            }
            if (this.moodFromIngesting == null)
            {
                this.moodFromIngesting = new FastLazy<float>(() => SafeGetMoodFromIngesting());
            }
        }

        private bool SafeCheckVeneratedAnimalMeat()
        {
            try
            {
                // Null safety checks
                if (this.eatenFoodSource?.Thing == null || this.pawn == null)
                {
                    return false;
                }

                // Check if ideology system is active and pawn has an ideology
                if (!ModsConfig.IdeologyActive || this.pawn.Ideo == null)
                {
                    return false;
                }

                return FoodUtility.IsVeneratedAnimalMeatOrCorpseOrHasIngredients(this.eatenFoodSource.Thing, this.pawn);
            }
            catch (System.Exception ex)
            {
                // Log the error and return safe default
                Verse.Log.Warning($"VarietyMatters: Error checking venerated animal meat for {this.pawn?.Name?.ToStringShort ?? "null pawn"}: {ex.Message}");
                return false;
            }
        }

        private float SafeGetMoodFromIngesting()
        {
            try
            {
                // Null safety checks
                if (this.eatenFoodSource?.Thing == null || this.pawn == null)
                {
                    return 0f;
                }

                return FoodUtility.MoodFromIngesting(this.pawn, this.eatenFoodSource.Thing, this.eatenFoodSource.ThingDef);
            }
            catch (System.Exception ex)
            {
                // Log the error and return safe default
                Verse.Log.Warning($"VarietyMatters: Error getting mood from ingesting for {this.pawn?.Name?.ToStringShort ?? "null pawn"}: {ex.Message}");
                return 0f;
            }
        }
    }
}
