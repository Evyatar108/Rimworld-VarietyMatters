namespace VarietyMatters
{
    using HarmonyLib;
    using RimWorld;
    using VarietyMatters.New;
    using Verse;

    // Token: 0x0200000D RID: 13
    [HarmonyPatch]
	public static class Patch_FoodUtility
	{
		// Token: 0x06000020 RID: 32 RVA: 0x00003298 File Offset: 0x00001498
		[HarmonyPatch(typeof(FoodUtility), "FoodOptimality")]
		[HarmonyPostfix]
		private static void Postfix_FoodOptimality(ref float __result, Pawn eater, Thing foodSource, bool takingToInventory)
		{
			if (takingToInventory)
			{
				return;
			}

			DietTracker dietTracker = VarietyRecord.GetVarietyRecord(eater);
			if (dietTracker?.MostRecentEatenFoodSource == null
				|| ThingCompUtility.TryGetComp<CompVariety>(foodSource) == null
				|| eater.needs.TryGetNeed<Need_FoodVariety>() == null
				|| eater.needs.TryGetNeed<Need_FoodVariety>().Disabled
				|| FoodUtility.Starving(eater)
				|| eater.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning, true)
				|| HealthAIUtility.ShouldSeekMedicalRest(eater)
				|| FoodUtility.WillGiveNegativeThoughts(foodSource, eater))
			{
				return;
			}

			EatenFoodSource mostRecentFoodSource = VarietyRecord.GetVarietyRecord(eater).MostRecentEatenFoodSource;
			CompIngredients compIngredients = ThingCompUtility.TryGetComp<CompIngredients>(foodSource);

            switch (ModSettings_VarietyMatters.foodTrackingType)
            {
                case FoodTrackingType.ByMealAndIngredients:
                    if (mostRecentFoodSource.ThingLabel != foodSource.def.label)
                    {
                        __result += 60f;
                    }
                    else
                    {
                        if (compIngredients != null)
                        {
                            for (int i = 0; i < compIngredients.ingredients.Count; i++)
                            {
                                if (!mostRecentFoodSource.IngredientsDefs.Contains(compIngredients.ingredients[i]))
                                {
                                    __result += 60f;
                                    break;
                                }
                            }
                        }
                    }
                    break;
                case FoodTrackingType.ByIngredients:
                    if (mostRecentFoodSource.ThingLabel != foodSource.def.label)
                    {
                        __result += 60f;
                    }
                    else
                    {
                        if (compIngredients != null)
                        {
                            for (int i = 0; i < compIngredients.ingredients.Count; i++)
                            {
                                if (!mostRecentFoodSource.IngredientsDefs.Contains(compIngredients.ingredients[i]))
                                {
                                    __result += 60f;
                                    break;
                                }
                            }
                        }
                    }
                    break;
                case FoodTrackingType.ByMeal:
                    if (mostRecentFoodSource.ThingLabel != foodSource.def.label)
                    {
                        __result += 60f;
                    }
                    break;
            }
        }
    }
}
