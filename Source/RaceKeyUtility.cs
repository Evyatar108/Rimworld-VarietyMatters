namespace VarietyMatters
{
	using Verse;

	internal static class RaceKeyUtility
	{
		public static string GetRaceKey(ThingDef thingDef)
		{
			string modName = thingDef.modContentPack?.Name ?? "null";
			return modName + "-" + thingDef.defName;
		}
	}
}
