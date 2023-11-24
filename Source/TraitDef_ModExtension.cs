namespace VarietyMatters
{
    using Verse;

    internal class TraitDef_ModExtension : DefModExtension
	{
		public static bool NeedsVariety(Pawn pawn)
		{
			for (int i = 0; i < pawn.story.traits.allTraits.Count; i++)
			{
				if (pawn.story.traits.allTraits[i].def.HasModExtension<TraitDef_ModExtension>())
				{
					return pawn.story.traits.allTraits[i].def.GetModExtension<TraitDef_ModExtension>().needsVariety;
				}
			}
			return true;
		}

		public float expectationFactor = 1f;

		public int minVarietyExpectation = 2;

		public int maxVarietyExpectation = 40;

		public bool needsVariety = true;
	}
}
