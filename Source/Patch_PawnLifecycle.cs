namespace VarietyMatters
{
    using System;
    using HarmonyLib;
    using Verse;

    [HarmonyPatch]
    public static class Patch_PawnLifecycle
    {
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill), new Type[] { typeof(DamageInfo?), typeof(Hediff) })]
        [HarmonyPostfix]
        private static void Postfix_Kill(Pawn __instance)
        {
            VarietyRecord.RemoveTrackedPawn(__instance);
            if (Prefs.DevMode)
            {
                Log.Message($"[VarietyMatters] Cleared tracker for dead pawn {__instance?.Name?.ToStringShort ?? "<null>"}");
            }
        }

        [HarmonyPatch(typeof(Pawn), nameof(Pawn.Destroy), new Type[] { typeof(DestroyMode) })]
        [HarmonyPostfix]
        private static void Postfix_Destroy(Pawn __instance)
        {
            VarietyRecord.RemoveTrackedPawn(__instance);
        }
    }
}
