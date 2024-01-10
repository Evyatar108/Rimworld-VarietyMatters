namespace VarietyMatters
{
    using System.Collections.Generic;
    using HarmonyLib;
    using RimWorld;
    using Verse;

    // Token: 0x0200000E RID: 14
    [HarmonyPatch(typeof(CompIngredients), "PreAbsorbStack")]
	public class Patch_IngredientStacking
	{
		// Token: 0x06000021 RID: 33 RVA: 0x00003540 File Offset: 0x00001740
		public static void Prefix(CompIngredients __instance, Thing otherStack, ref List<ThingDef> __state)
		{
			bool flag = ModSettings_VarietyMatters.foodTrackingType == New.FoodTrackingType.ByMealNames || ModSettings_VarietyMatters.numIngredients <= 3 || __instance.ingredients.Count == 0 || __instance.parent.def.label == "kibble";
            if (!flag)
			{
				__state = ThingCompUtility.TryGetComp<CompIngredients>(otherStack).ingredients;
				for (int i = 0; i < __instance.ingredients.Count; i++)
				{
					if (!__state.Contains(__instance.ingredients[i]))
					{
						__state.Add(__instance.ingredients[i]);
					}
				}
			}
		}

		// Token: 0x06000022 RID: 34 RVA: 0x000035D4 File Offset: 0x000017D4
		public static void Postfix(ref CompIngredients __instance, ref List<ThingDef> __state)
		{
			bool ignoreIngredients = ModSettings_VarietyMatters.foodTrackingType == New.FoodTrackingType.ByMealNames;
			if (!ignoreIngredients && __instance.parent.def.label != "kibble")
			{
				int num = __instance.ingredients.Count;
				if (num < ModSettings_VarietyMatters.numIngredients && __state != null && __state.Count > num)
				{
					if (__state.Count > ModSettings_VarietyMatters.numIngredients)
					{
						GenList.Shuffle<ThingDef>(__state);
					}
					for (int i = 0; i < __state.Count; i++)
					{
						if (!__instance.ingredients.Contains(__state[i]))
						{
							__instance.ingredients.Add(__state[i]);
							num++;
						}
						if (num == ModSettings_VarietyMatters.numIngredients)
						{
							break;
						}
					}
				}
				else
				{
                    if (num > ModSettings_VarietyMatters.numIngredients && num > 0)
                    {
                        __instance.ingredients.RemoveRange(ModSettings_VarietyMatters.numIngredients, num - ModSettings_VarietyMatters.numIngredients);
                    }
                }
			}
		}
	}
}
