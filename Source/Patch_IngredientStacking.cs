using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VarietyMatters
{
	// Token: 0x0200000E RID: 14
	[HarmonyPatch(typeof(CompIngredients), "PreAbsorbStack")]
	public class Patch_IngredientStacking
	{
		// Token: 0x06000021 RID: 33 RVA: 0x00003540 File Offset: 0x00001740
		public static void Prefix(CompIngredients __instance, Thing otherStack, ref List<ThingDef> __state)
		{
			bool flag = ModSettings_VarietyMatters.ignoreIngredients || ModSettings_VarietyMatters.numIngredients <= 3 || __instance.ingredients.Count <= 0;
			if (!flag)
			{
				__state = ThingCompUtility.TryGetComp<CompIngredients>(otherStack).ingredients;
				for (int i = 0; i < __instance.ingredients.Count; i++)
				{
					bool flag2 = !__state.Contains(__instance.ingredients[i]);
					if (flag2)
					{
						__state.Add(__instance.ingredients[i]);
					}
				}
			}
		}

		// Token: 0x06000022 RID: 34 RVA: 0x000035D4 File Offset: 0x000017D4
		public static void Postfix(ref CompIngredients __instance, ref List<ThingDef> __state)
		{
			bool ignoreIngredients = ModSettings_VarietyMatters.ignoreIngredients;
			if (!ignoreIngredients)
			{
				int num = __instance.ingredients.Count;
				bool flag = num < ModSettings_VarietyMatters.numIngredients && __state != null && __state.Count > num;
				if (flag)
				{
					bool flag2 = __state.Count > ModSettings_VarietyMatters.numIngredients;
					if (flag2)
					{
						GenList.Shuffle<ThingDef>(__state);
					}
					for (int i = 0; i < __state.Count; i++)
					{
						bool flag3 = !__instance.ingredients.Contains(__state[i]);
						if (flag3)
						{
							__instance.ingredients.Add(__state[i]);
							num++;
						}
						bool flag4 = num == ModSettings_VarietyMatters.numIngredients;
						if (flag4)
						{
							break;
						}
					}
				}
				else
				{
					while (num > ModSettings_VarietyMatters.numIngredients && num > 0)
					{
						__instance.ingredients.RemoveAt(num - 1);
						num--;
					}
				}
			}
		}
	}
}
