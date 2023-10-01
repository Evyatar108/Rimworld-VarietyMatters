using System;
using Verse;

namespace VarietyMatters
{
	// Token: 0x02000011 RID: 17
	internal class TraitDef_ModExtension : DefModExtension
	{
		// Token: 0x0600002C RID: 44 RVA: 0x000040A8 File Offset: 0x000022A8
		public static bool NeedsVariety(Pawn pawn)
		{
			for (int i = 0; i < pawn.story.traits.allTraits.Count; i++)
			{
				bool flag = pawn.story.traits.allTraits[i].def.HasModExtension<TraitDef_ModExtension>();
				if (flag)
				{
					return pawn.story.traits.allTraits[i].def.GetModExtension<TraitDef_ModExtension>().needsVariety;
				}
			}
			return true;
		}

		// Token: 0x04000027 RID: 39
		public float expectationFactor = 1f;

		// Token: 0x04000028 RID: 40
		public int minVarietyExpectation = 2;

		// Token: 0x04000029 RID: 41
		public int maxVarietyExpectation = 40;

		// Token: 0x0400002A RID: 42
		public bool needsVariety = true;
	}
}
