namespace VarietyMatters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using RimWorld;
    using VarietyMatters.New;
    using Verse;
    using static VarietyMatters.New.DietTracker;

    // Token: 0x0200000C RID: 12
    public class Need_FoodVariety : Need
	{
		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000018 RID: 24 RVA: 0x00002F8C File Offset: 0x0000118C
		public MenuVarietyLevel CurCategory
        {
            get
            {
                if (this.Disabled || this.IsFrozen)
                {
                    return MenuVarietyLevel.NA;
                }

                var levelByPercentage = this.CurLevel * 2;

                if (levelByPercentage <= 0.1538f) // 15.38% or less
                {
                    return MenuVarietyLevel.Barren;
                }
                if (levelByPercentage <= 0.3077f) // Up to 30.77%
                {
                    return MenuVarietyLevel.Empty;
                }
                if (levelByPercentage <= 0.4615f) // Up to 46.15%
                {
                    return MenuVarietyLevel.Poor;
                }
                if (levelByPercentage <= 0.6154f) // Up to 61.54%
                {
                    return MenuVarietyLevel.Scarce;
                }
                if (levelByPercentage <= 0.7692f) // Up to 76.92%
                {
                    return MenuVarietyLevel.Limited;
                }
                if (levelByPercentage <= 0.9231f) // Up to 92.31%
                {
                    return MenuVarietyLevel.BelowAverage;
                }
                if (levelByPercentage <= 1.0769f) // Up to 107.69%
                {
                    return MenuVarietyLevel.Average;
                }
                if (levelByPercentage <= 1.2308f) // Up to 123.08%
                {
                    return MenuVarietyLevel.AboveAverage;
                }
                if (levelByPercentage <= 1.3846f) // Up to 138.46%
                {
                    return MenuVarietyLevel.Good;
                }
                if (levelByPercentage <= 1.5385f) // Up to 153.85%
                {
                    return MenuVarietyLevel.Great;
                }
                if (levelByPercentage <= 1.6923f) // Up to 169.23%
                {
                    return MenuVarietyLevel.Excellent;
                }
                if (levelByPercentage <= 1.8462f) // Up to 184.62%
                {
                    return MenuVarietyLevel.Exceptional;
                }

                // Above 184.62%
                return MenuVarietyLevel.Overwhelming;
            }
        }

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000019 RID: 25 RVA: 0x0000303D File Offset: 0x0000123D
		public override int GUIChangeArrow
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x0600001A RID: 26 RVA: 0x00003040 File Offset: 0x00001240
		public override bool ShowOnNeedList
		{
			get
			{
				return !this.Disabled;
			}
		}

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x0600001B RID: 27 RVA: 0x0000304C File Offset: 0x0000124C
		public bool Disabled
		{
			get
			{
				bool doesNotHaveVarietyNeed = this.pawn.Dead || !TraitDef_ModExtension.NeedsVariety(this.pawn) || this.pawn.needs.food == null || this.pawn.needs.mood == null;
				if (doesNotHaveVarietyNeed)
				{
					return true;
				}

				if (!ModSettings_VarietyMatters.slavesHaveVarietyNeed && this.pawn.IsSlave)
				{
					return true;
				}

                if (!ModSettings_VarietyMatters.prisonersHaveVarietyNeed && this.pawn.IsPrisoner)
                {
                    return true;
                }

                bool isRaceWithVariety = ModSettings_VarietyMatters.raceVariety.ContainsKey(this.pawn.def.label) && ModSettings_VarietyMatters.raceVariety[this.pawn.def.label];
				return !isRaceWithVariety;
			}
		}

		// Token: 0x0600001C RID: 28 RVA: 0x000030EC File Offset: 0x000012EC
		public Need_FoodVariety(Pawn pawn) : base(pawn)
		{
            this.threshPercents = new List<float>
            {
                0.0833f,
                0.1666f,
                0.2500f,
                0.3333f,
                0.4166f,
                0.5000f,
                0.5833f,
                0.6666f,
                0.7500f,
                0.8333f,
                0.9166f
            };
        }

		// Token: 0x0600001D RID: 29 RVA: 0x00003151 File Offset: 0x00001351
		public override void SetInitialLevel()
		{
			this.CurLevel = 0.5f;
		}

		// Token: 0x0600001E RID: 30 RVA: 0x00003160 File Offset: 0x00001360
		public override void NeedInterval()
		{
			if (this.Disabled)
			{
				this.CurLevel = 0.5f;
                return;
			}
		}

		// Token: 0x0600001F RID: 31 RVA: 0x00003188 File Offset: 0x00001388
		public override string GetTipString()
		{
			string tipString = base.GetTipString();
            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(this.pawn);
            string varietesCountExpectedText = varietyExpectation.ToString();
			string actualVarietiesCountText = "0";
			string mostRecentFoodSourceText = ", but I don't remember them for some reason..";
            DietTracker dietTracker = VarietyRecord.GetVarietyRecord(this.pawn);
			if (dietTracker != null && dietTracker.MostRecentEatenFoodSource != null)
			{
				actualVarietiesCountText = dietTracker.TotalVariety.ToString();
                mostRecentFoodSourceText = GetEatenFoodSourcesDescription(dietTracker);
            }

            float memoryMult = ModSettings_VarietyMatters.memoryMultiplier;

            string mealMemoryString = ((int)(memoryMult * varietyExpectation)).ToString();

            return string.Concat(new string[]
			{
				tipString,
				"\n\nI expect at least ", varietesCountExpectedText, " different meals; ",
				"recently I had ", actualVarietiesCountText, ".",
				"\nI have the capacity to recall the last ", mealMemoryString, " meals I had",
				mostRecentFoodSourceText
			});
		}

        private string GetEatenFoodSourcesDescription(DietTracker dietTracker)
        {
            IEnumerable<(EatenFoodSource, FoodSourceInfoForPawn)> foodSourcesInfoForPawn = dietTracker.GetEatenFoodSourcesInfoForPawnInOrderOfIngestion().Reverse();
            Dictionary<FoodSourceInfoForPawn, int> appearancesOfFoodSources = new Dictionary<FoodSourceInfoForPawn, int>();
            StringBuilder stringBuilder = new StringBuilder(", the most recent ones were:\n");

            int i = 0;
            int maxViewableRecentMeals = 7;
            foreach ((EatenFoodSource eatenFoodSource, FoodSourceInfoForPawn foodSourceInfoForPawn) in foodSourcesInfoForPawn)
            {
                if (i >= maxViewableRecentMeals)
                {
                    break;
                }

                stringBuilder.AppendLine();

                bool isForgotten = eatenFoodSource.IsForgotton;

                if (!appearancesOfFoodSources.TryGetValue(foodSourceInfoForPawn, out int count))
                {
                    count = 0;
                }

                count++;
                appearancesOfFoodSources[foodSourceInfoForPawn] = count;

                int timesAlreadyIngestedSoFar = foodSourceInfoForPawn.CountInMemory - count + 1;
                string apperanceString = $"Appearance {timesAlreadyIngestedSoFar} out of {foodSourceInfoForPawn.CountInMemory}";

                string foodKey = eatenFoodSource.GetFoodSourceKey();

                string foodInfo = GenText.CapitalizeFirst(eatenFoodSource.ToString());

                int seed = (foodKey, timesAlreadyIngestedSoFar).GetHashCode();

                string varietyInfo = foodSourceInfoForPawn.HasVarietyValueForPawn
                    ? isForgotten ? GetVarietyInfoForForgotten(seed) : GetVarietyInfoBasedOnIngestionCount(timesAlreadyIngestedSoFar, seed)
                    : GetVarietyInfoBasedOnNoVarietyReason(foodSourceInfoForPawn, seed);

                stringBuilder.Append(" ◊  ").Append(foodInfo);

                stringBuilder.AppendLine();

                stringBuilder.Append("✧ ").AppendLine(varietyInfo);

                if (!isForgotten && foodSourceInfoForPawn.CountInMemory > 1)
                {
                    stringBuilder.Append("☆ ").Append("Appearance ").Append(timesAlreadyIngestedSoFar).Append(" out of ").Append(foodSourceInfoForPawn.CountInMemory);

                    if (eatenFoodSource.HasMealType)
                    {
                        stringBuilder.Append(" by type");
                    }

                    stringBuilder.AppendLine();
                }

                i++;
            }

            return stringBuilder.ToString();
        }

        private string GetVarietyInfoForForgotten(int seed)
        {
            return RandChooseForgottenFoodStrings(seed);
        }

        private string GetVarietyInfoBasedOnIngestionCount(int timesAlreadyIngestedSoFar, int seed)
        {
            if (timesAlreadyIngestedSoFar > 1)
            {
                return this.RandChooseNoVarietyStrings(seed);
            }
            else
            {
                return this.RandChooseHasVarietyStrings(seed);
            }
        }

        private string RandChooseForgottenFoodStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(Has Variety) There's variety in my meals, but I can't recall the specifics.",
                "(Has Variety) I remember enjoying a variety, though the details escape me.",
                "(Has Variety) There was something different about this meal, but what was it?",
                "(Has Variety) I know there's been variety lately, but the details are fuzzy.",
                "(Has Variety) Variety's there, but the details are as clear as a foggy morning.",
                "(Has Variety) I recall different foods, but the specifics are just out of reach.",
                "(Has Variety) There's a sense of variety, but the exact meals are a blur.",
                "(Has Variety) I remember being excited by different dishes, yet can't recall them.",
                "(Has Variety) The details are lost, but I'm sure there was a good mix of foods.",
                "(Has Variety) Variety's been good, though I can't quite remember the meals.",
                "(Has Variety) Different meals for sure, but it's like trying to remember a dream.",
                "(Has Variety) A variety of tastes, yet the memories are elusive.",
                "(Has Variety) I know I enjoyed the variety, but the details are hazy.",
                "(Has Variety) There was diversity on my plate, but the details slipped away.",
                "(Has Variety) I remember enjoying the variety, but the meals themselves are forgotten.",
                "(Has Variety) Definitely a variety, but it's like a forgotten melody.",
                "(Has Variety) Each meal was different, I think, but I can't pin down the details.",
                "(Has Variety) Variety was there, but the specifics have faded away.",
                "(Has Variety) The feeling of variety is there, but the meals are a distant memory.",
                "(Has Variety) I recall the joy of varied meals, but not what they were."
            );
        }

        private string RandChooseNoVarietyStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Not this again! I need something different to keep things interesting.",
                "(No Variety) The same meal, yet again? It's becoming a dull routine.",
                "(No Variety) Another round of the usual... Where's the variety gone?",
                "(No Variety) It feels like I'm eating in a loop. Some change would be nice!",
                "(No Variety) This monotony in meals is disheartening. Craving for something new!",
                "(No Variety) Eating this again feels like a repetitive chore.",
                "(No Variety) The same dish over and over? My taste buds are bored.",
                "(No Variety) Variety is the spice of life, but this is just bland repetition.",
                "(No Variety) A meal repeated is a meal unappreciated. Yearning for a change.",
                "(No Variety) Are we stuck in a culinary Groundhog Day? Time for something new.",
                "(No Variety) Again the same? It's like my taste buds are on a treadmill.",
                "(No Variety) This lack of diversity in food is really starting to get to me.",
                "(No Variety) Meal deja vu? A little variety wouldn't hurt!",
                "(No Variety) The definition of insanity is eating the same meal over and over.",
                "(No Variety) I feel like a robot with these repetitive meals. Craving some spontaneity.",
                "(No Variety) My culinary journey seems to be walking in circles.",
                "(No Variety) It's high time we added some new flavors to this repetitive menu.",
                "(No Variety) Variety is the missing ingredient in these repetitive meals.",
                "(No Variety) Another day, the same meal. It's becoming predictably boring.",
                "(No Variety) Is our chef stuck in a loop? Time to switch things up!"
                );
        }

        private string RandChooseHasVarietyStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(Has Variety) Finally, something different! This definitely counts as variety.",
                "(Has Variety) A pleasant change in our menu! It's refreshing to have some variety.",
                "(Has Variety) New flavors! This is the variety I've been craving.",
                "(Has Variety) Diversity on my plate at last! This is what a good meal looks like.",
                "(Has Variety) Look at this selection! It's nice to break the monotony.",
                "(Has Variety) Something out of the ordinary, finally! That's the spice of life.",
                "(Has Variety) This meal breaks the routine, in the best way possible.",
                "(Has Variety) A welcome change! It's great to have different options.",
                "(Has Variety) This new taste is a delightful surprise. I'm glad for the variety.",
                "(Has Variety) It's a joy to have something other than the usual. Variety matters!",
                "(Has Variety) What a refreshing lineup of dishes! This really livens up the meal.",
                "(Has Variety) A culinary adventure on my plate! This is exciting.",
                "(Has Variety) Such an array of choices! This is what dining should be like.",
                "(Has Variety) Every bite is a new experience. Loving this variety!",
                "(Has Variety) This is a feast for the senses! So glad for the change.",
                "(Has Variety) A smorgasbord of flavors! This is how to keep meals interesting.",
                "(Has Variety) So many options, it's hard to choose. But that's a good problem!",
                "(Has Variety) Each meal is a delightful surprise now. This variety is wonderful.",
                "(Has Variety) The richness of choices here is amazing. Every meal is a discovery.",
                "(Has Variety) Gone are the days of boring meals. This variety is a game changer!");
        }

        private string GetVarietyInfoBasedOnNoVarietyReason(FoodSourceInfoForPawn foodSourceInfoForPawn, int seed)
        {
            switch (foodSourceInfoForPawn.NoVarietyReason)
            {
                case NoVarietyReason.Rotten:
                    return RandChooseRottenStrings(seed);
                case NoVarietyReason.HumanLikeMeat:
                    return RandChooseHumanLikeMeatStrings(seed);
                case NoVarietyReason.InsectMeat:
                    return RandChooseInsectMeatStrings(seed);
                case NoVarietyReason.RawOrRawlikeFood:
                    return RandChooseRawOrRawlikeFoodStrings(seed);
                case NoVarietyReason.Fungus:
                    return RandChooseFungusStrings(seed);
                case NoVarietyReason.HasChemicals:
                    return RandChooseChemicalStrings(seed);
                case NoVarietyReason.UnacceptableByVegetarians:
                    return RandChooseUnacceptableByVegetariansStrings(seed);
                case NoVarietyReason.UnacceptableByCarnivores:
                    return RandChooseUnacceptableByCarnivoresStrings(seed);
                case NoVarietyReason.IsOrHasVenetratedAnimalMeat:
                    return RandChooseIsOrHasVenetratedAnimalMeatStrings(seed);
                default:
                    throw new NotImplementedException($"No handling logic implemented in switch for NoVarietyReason value: {foodSourceInfoForPawn.NoVarietyReason}.");
            }
        }

        private string RandChooseRottenStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) This food was rotten or spoiled, I wanted to puke.",
                "(No Variety) Spoiled again? The smell alone is a turn-off.",
                "(No Variety) Rotten food is a no-go. It's completely inedible.");
        }

        private string RandChooseHumanLikeMeatStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Eating human-like meat? That's a line I can't cross. It's horrifying.",
                "(No Variety) Human-like meat on the plate? That's beyond my limits.",
                "(No Variety) This is too much. Eating something so human-like is disturbing.");
        }

        private string RandChooseInsectMeatStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Insect meat? It might be nutritious, but it's not my idea of variety.",
                "(No Variety) Bugs in the diet? That's not the variety I had in mind.",
                "(No Variety) Eating insects doesn't quite meet my expectations for diverse cuisine.");
        }

        private string RandChooseRawOrRawlikeFoodStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Raw or almost raw... this isn't variety, it's a survival test.",
                "(No Variety) Nearly raw food again? Where's the joy in eating?",
                "(No Variety) This is too raw for my liking. Craving for something more cooked.");
        }

        private string RandChooseFungusStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Just fungus? It's monotonous and lacks the essence of a varied meal.",
                "(No Variety) Fungus all the time? That's not exactly a feast of variety.",
                "(No Variety) A diet heavy in fungus isn't what I'd call varied. It's pretty one-note.");
        }

        private string RandChooseChemicalStrings(int seed)
        {
            return this.RandChoose(seed,
                "(No Variety) Drugs in my food? That's a health hazard I'm not willing to take.",
                "(No Variety) I try to keep it natural. These drugs just don't sit right with me.",
                "(No Variety) Eating this feels like a science experiment gone wrong. No, thank you.",
                "(No Variety) My body isn't a lab for drug testing. I'll pass on this.",
                "(No Variety) Drugs in my meal? That's a line I won't cross for the sake of variety.",
                "(No Variety) I prefer my food free of additives. Drugs are a big no-no for me.",
                "(No Variety) It's hard to enjoy a meal when you know it's laced with drugs."
            );
        }

        private string RandChooseUnacceptableByVegetariansStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) As a vegetarian, this just doesn't work for me. It's all wrong for variety.",
                "(No Variety) This isn't vegetarian-friendly at all. Can't consider it varied.",
                "(No Variety) Eating this would go against my vegetarian principles. It lacks variety.");
        }

        private string RandChooseUnacceptableByCarnivoresStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) For a carnivore like me, this isn't variety, it's a plant-based challenge.",
                "(No Variety) Where's the meat? This isn't the kind of variety a carnivore needs.",
                "(No Variety) All plants and no meat? That's not what a carnivore's diet should be.");
        }

        private string RandChooseIsOrHasVenetratedAnimalMeatStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) Venerated animal meat is off-limits. This isn't variety, it's disrespect.",
                "(No Variety) Eating meat from a venerated animal? That's not something I can do.",
                "(No Variety) This includes meat from a revered animal. Can't partake in it.");
        }

        private string RandChoose(int seed, params string[] choices)
        {
            Random rand = new Random(seed);

            int caseSelector = rand.Next(0, choices.Length);

            return choices[caseSelector].ToString();
        }
    }
}
