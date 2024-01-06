namespace VarietyMatters
{
    using System.Collections.Generic;
    using Verse;

    // Token: 0x02000004 RID: 4
    [StaticConstructorOnStartup]
	public class AddCompVariety
	{
		// Token: 0x06000003 RID: 3 RVA: 0x0000206C File Offset: 0x0000026C
		static AddCompVariety()
		{
			List<ThingDef> allDefsListForReading = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < allDefsListForReading.Count; i++)
			{
				bool flag = (allDefsListForReading[i].IsMeat || allDefsListForReading[i].IsCorpse) && allDefsListForReading[i].GetCompProperties<CompProperties_Variety>() == null;
				if (flag)
				{
					CompProperties_Variety item = new CompProperties_Variety();
					allDefsListForReading[i].comps.Add(item);
				}
			}

			ModSettings_VarietyMatters.GenerateRaces();
		}
	}
}
