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
                mostRecentFoodSourceText = this.GetEatenFoodSourcesDescription(dietTracker);
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

        private int GetEatenFoodSourceCountKey(EatenFoodSource eatenFoodSource, FoodVarietyInfo foodVarietyInfo)
        {
            return (eatenFoodSource.GetFoodSourceKey(), foodVarietyInfo != null ? 1 : 0).GetHashCode();
        }

        private string GetEatenFoodSourcesDescription(DietTracker dietTracker)
        {
            IEnumerable<(EatenFoodSource eatenFoodSource, FoodVarietyInfo foodVarietyInfo, NoVarietyReason? noVarietyReason)> foodsVarietyInfoInIngestionOrder =
                dietTracker.GetFoodVarietyInfoForPawnInIngestionOrder().Reverse();
            Dictionary<int, int> totalAppearancesOfFoodSources = new Dictionary<int, int>();
            Dictionary<int, int> appearancesOfFoodSourcesSoFar = new Dictionary<int, int>();
            foreach ((EatenFoodSource eatenFoodSource, FoodVarietyInfo foodVarietyInfo, NoVarietyReason? noVarietyReason) in foodsVarietyInfoInIngestionOrder)
            {
                var key = GetEatenFoodSourceCountKey(eatenFoodSource, foodVarietyInfo);
                if (!totalAppearancesOfFoodSources.TryGetValue(key, out var count))
                {
                    count = 0;
                }

                totalAppearancesOfFoodSources[key] = count + 1;
                appearancesOfFoodSourcesSoFar[key] = 0;
            }


            StringBuilder stringBuilder = new StringBuilder(", the most recent ones were:\n");

            int i = 0;
            int maxViewableRecentMeals = 7;
            foreach ((EatenFoodSource eatenFoodSource, FoodVarietyInfo foodVarietyInfo, NoVarietyReason? noVarietyReason) in foodsVarietyInfoInIngestionOrder)
            {
                if (foodVarietyInfo == null && noVarietyReason == null)
                {
                    Log.Warning("Found eaten food source with null variety info and null no variety reason");
                    continue;
                }

                if (i >= maxViewableRecentMeals)
                {
                    break;
                }

                stringBuilder.AppendLine();

                string foodKey = eatenFoodSource.GetFoodSourceKey();

                int countKey = GetEatenFoodSourceCountKey(eatenFoodSource, foodVarietyInfo);

                appearancesOfFoodSourcesSoFar[countKey]++;

                int timesAlreadyIngestedSoFar = totalAppearancesOfFoodSources[countKey] - appearancesOfFoodSourcesSoFar[countKey] + 1;

                string foodInfo = GenText.CapitalizeFirst(eatenFoodSource.ToString());

                int seed = (foodKey, timesAlreadyIngestedSoFar).GetHashCode();

                string varietyInfo = foodVarietyInfo != null
                    ? (eatenFoodSource.IsForgotten ? this.GetVarietyInfoForForgotten(seed) : this.GetVarietyInfoBasedOnIngestionCount(timesAlreadyIngestedSoFar, seed))
                    : this.GetVarietyInfoBasedOnNoVarietyReason(noVarietyReason.Value, seed);

                stringBuilder.Append(" ◊  ").Append(foodInfo);

                stringBuilder.AppendLine();

                stringBuilder.Append("✧ ").AppendLine(varietyInfo);

                if (foodVarietyInfo != null && !eatenFoodSource.IsForgotten && foodVarietyInfo.CountInMemory > 1)
                {
                    stringBuilder.Append("☆ ").Append("Appearance ").Append(timesAlreadyIngestedSoFar).Append(" out of ").Append(foodVarietyInfo.CountInMemory);


                    if (ModSettings_VarietyMatters.clusterSimilarMealsTogether && eatenFoodSource.HasMealType)
                    {
                        switch (ModSettings_VarietyMatters.foodTrackingType)
                        {
                            case FoodTrackingType.ByMealNames:
                                stringBuilder.Append(" by type");
                                break;
                            case FoodTrackingType.ByIngredientsCombination:
                                stringBuilder.Append(" by ingredients");
                                break;
                            case FoodTrackingType.ByMealNamesAndIngredientsCombination:
                                stringBuilder.Append(" by meal type and ingredients");
                                break;
                        }
                    }
                    else
                    {
                        switch (ModSettings_VarietyMatters.foodTrackingType)
                        {
                            case FoodTrackingType.ByMealNames:
                                stringBuilder.Append(" by meal name");
                                break;
                            case FoodTrackingType.ByIngredientsCombination:
                                stringBuilder.Append(" by ingredients");
                                break;
                            case FoodTrackingType.ByMealNamesAndIngredientsCombination:
                                stringBuilder.Append(" by meal name and ingredients");
                                break;
                        }
                    }

                    stringBuilder.AppendLine();
                }

                i++;
            }

            return stringBuilder.ToString();
        }

        private string GetVarietyInfoForForgotten(int seed)
        {
            return this.RandChooseForgottenFoodStrings(seed);
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
                "(Has Variety) Every taste is a new experience. Loving this variety!",
                "(Has Variety) This is a feast for the senses! So glad for the change.",
                "(Has Variety) A smorgasbord of flavors! This is how to keep meals interesting.",
                "(Has Variety) So many options, it's hard to choose. But that's a good problem!",
                "(Has Variety) Each meal is a delightful surprise now. This variety is wonderful.",
                "(Has Variety) The richness of choices here is amazing. Every meal is a discovery.",
                "(Has Variety) Gone are the days of boring meals. This variety is a game changer!");
        }

        private string GetVarietyInfoBasedOnNoVarietyReason(NoVarietyReason noVarietyReason, int seed)
        {
            switch (noVarietyReason)
            {
                case NoVarietyReason.Rotten:
                    return this.RandChooseRottenStrings(seed);
                case NoVarietyReason.HumanLikeMeat:
                    return this.RandChooseHumanLikeMeatStrings(seed);
                case NoVarietyReason.InsectMeat:
                    return this.RandChooseInsectMeatStrings(seed);
                case NoVarietyReason.RawOrRawlikeFood:
                    return this.RandChooseRawOrRawlikeFoodStrings(seed);
                case NoVarietyReason.Fungus:
                    return this.RandChooseFungusStrings(seed);
                case NoVarietyReason.HasChemicals:
                    return this.RandChooseChemicalStrings(seed);
                case NoVarietyReason.UnacceptableByVegetarians:
                    return this.RandChooseUnacceptableByVegetariansStrings(seed);
                case NoVarietyReason.UnacceptableByCarnivores:
                    return this.RandChooseUnacceptableByCarnivoresStrings(seed);
                case NoVarietyReason.IsOrHasVeneratedAnimalMeat:
                    return this.RandChooseIsOrHasVeneratedAnimalMeatStrings(seed);
                case NoVarietyReason.DisgustingMeal:
                    return this.RandChooseDisgustingMealStrings(seed);
                default:
                    throw new NotImplementedException($"No handling logic implemented in switch for NoVarietyReason value: {noVarietyReason}.");
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

        private string RandChooseDisgustingMealStrings(int seed)
        {
            return this.RandChoose(
                seed,
                "(No Variety) That was revolting. I hope I never have to eat something like that again.",
                "(No Variety) Absolutely disgusting! It's hard to believe that's considered food.",
                "(No Variety) I've eaten some bad meals, but that one takes the cake for being the worst.",
                "(No Variety) The mere thought of that meal makes my stomach turn.",
                "(No Variety) That was horrendous. I'd rather go hungry than eat that again.",
                "(No Variety) What was that? It tasted like a mix of bad decisions and regret.",
                "(No Variety) Eating that was a challenge. It's the opposite of a culinary delight.",
                "(No Variety) If there's a contest for the worst meal ever, that one's a strong contender.",
                "(No Variety) Never again. That meal was a nightmare on a plate.",
                "(No Variety) That dish was an assault on my taste buds. Truly appalling.",
                "(No Variety) How can something edible be that repulsive? I'm baffled and nauseated.",
                "(No Variety) It's like someone cooked up disappointment and served it with a side of misery.",
                "(No Variety) I didn't think food could be that bad. I was wrong.",
                "(No Variety) That's one meal I wish I could erase from my memory – and my taste buds.",
                "(No Variety) It's hard to describe how bad that was. 'Disgusting' is an understatement.",
                "(No Variety) Whoever thought that was a good idea for a meal was sadly mistaken.",
                "(No Variety) I'm not sure what that was, but it's a culinary abomination.",
                "(No Variety) That meal was a gastronomic disaster. Just terrible.",
                "(No Variety) I've had some bad meals, but that one is going straight to the hall of shame.",
                "(No Variety) A truly vile concoction. It's a mystery how it can even be classified as food."
            );
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

        private string RandChooseIsOrHasVeneratedAnimalMeatStrings(int seed)
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
