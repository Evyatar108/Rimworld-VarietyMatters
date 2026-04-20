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
		// RimWorld stack-absorption runs on the main thread; scratch reused across Prefix/Postfix calls.
		private static readonly HashSet<ThingDef> _mergeSeenDefsScratch = new HashSet<ThingDef>();

		private static bool ShouldSkipIngredientMerge(CompIngredients instance)
		{
			return ModSettings_VarietyMatters.numIngredients <= 3
				|| instance.ingredients.Count == 0
				|| instance.parent.def.defName == "Kibble";
		}

		// Token: 0x06000021 RID: 33 RVA: 0x00003540 File Offset: 0x00001740
		public static void Prefix(CompIngredients __instance, Thing otherStack, ref List<ThingDef> __state)
		{
			if (ShouldSkipIngredientMerge(__instance)) return;
			var otherComp = ThingCompUtility.TryGetComp<CompIngredients>(otherStack);
			if (otherComp == null) return;
			__state = otherComp.ingredients;
			HashSet<ThingDef> seen = _mergeSeenDefsScratch;
			seen.Clear();
			for (int i = 0; i < __state.Count; i++)
			{
				seen.Add(__state[i]);
			}
			List<ThingDef> instIngredients = __instance.ingredients;
			for (int i = 0; i < instIngredients.Count; i++)
			{
				ThingDef def = instIngredients[i];
				if (seen.Add(def))
				{
					__state.Add(def);
				}
			}
		}

		// Token: 0x06000022 RID: 34 RVA: 0x000035D4 File Offset: 0x000017D4
		public static void Postfix(ref CompIngredients __instance, ref List<ThingDef> __state)
		{
			if (ShouldSkipIngredientMerge(__instance)) return;
			int num = __instance.ingredients.Count;
			if (num < ModSettings_VarietyMatters.numIngredients && __state != null && __state.Count > num)
			{
				if (__state.Count > ModSettings_VarietyMatters.numIngredients)
				{
					GenList.Shuffle<ThingDef>(__state);
				}
				HashSet<ThingDef> seen = _mergeSeenDefsScratch;
				seen.Clear();
				List<ThingDef> instIngredients = __instance.ingredients;
				for (int i = 0; i < instIngredients.Count; i++)
				{
					seen.Add(instIngredients[i]);
				}
				for (int i = 0; i < __state.Count; i++)
				{
					ThingDef def = __state[i];
					if (seen.Add(def))
					{
						instIngredients.Add(def);
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
