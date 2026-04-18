namespace VarietyMatters
{
    using HarmonyLib;
    using RimWorld;
    using System.Collections.Generic;
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

			if (foodSource.def.ingestible == null)
			{
				return;
			}

			Need_FoodVariety need = eater.needs?.TryGetNeed<Need_FoodVariety>();
			if (need == null || need.Disabled)
			{
				return;
			}

			if (!VarietyRecord.TryGetVarietyRecord(eater, out DietTracker dietTracker)
				|| dietTracker.MostRecentEatenFoodSource == null)
			{
				return;
			}

			CompVariety compVariety = ThingCompUtility.TryGetComp<CompVariety>(foodSource);
			if (compVariety == null)
			{
				return;
			}

			if (FoodUtility.Starving(eater)
				|| eater.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning, true)
				|| HealthAIUtility.ShouldSeekMedicalRest(eater)
				|| FoodUtility.WillGiveNegativeThoughts(foodSource, eater))
			{
				return;
			}

			HashSet<string> keysOfFoodSourcesWithVariety = dietTracker.KeysOfFoodSourcesWithVariety;
			string newFoodSourceKey = compVariety.GetFoodSourceKey();

			if (!keysOfFoodSourcesWithVariety.Contains(newFoodSourceKey))
			{
				__result += 60f;
			}
        }
    }
}
