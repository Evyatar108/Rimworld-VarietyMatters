namespace VarietyMatters
{
    using System;
    using RimWorld;
    using RimWorld.Planet;
    using Verse;

    // Token: 0x02000010 RID: 16
    public class ThoughtWorker_FoodVariety : ThoughtWorker
	{
        public override float MoodMultiplier(Pawn p)
        {
            float baseMultiplier = 2f;

            if (ModSettings_VarietyMatters.halveVarietyMoodImpact)
            {
                baseMultiplier = 1f;
            }

            if (p.story.traits.HasTrait(TraitDef.Named("Gourmand")))
            {
                baseMultiplier *= 2;
            }

            return baseMultiplier;
        }

        // Token: 0x0600002A RID: 42 RVA: 0x00003FA4 File Offset: 0x000021A4
        protected override ThoughtState CurrentStateInternal(Pawn p)
		{
			Need_FoodVariety need_FoodVariety = p.needs.TryGetNeed<Need_FoodVariety>();

			bool shoulNotHaveThought = need_FoodVariety == null || FoodUtility.Starving(p) || p.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning, true) || CaravanUtility.IsCaravanMember(p) || p.story.traits.HasTrait(TraitDefOf.Ascetic);
            if (shoulNotHaveThought)
			{
				return ThoughtState.Inactive;
			}

            switch (need_FoodVariety.CurCategory)
            {
                case MenuVarietyLevel.NA:
                    return ThoughtState.Inactive;

                case MenuVarietyLevel.Barren:
                    return ThoughtState.ActiveAtStage(0);

                case MenuVarietyLevel.Empty:
                    return ThoughtState.ActiveAtStage(1);

                case MenuVarietyLevel.Poor:
                    return ThoughtState.ActiveAtStage(2);

                case MenuVarietyLevel.Scarce:
                    return ThoughtState.ActiveAtStage(3);

                case MenuVarietyLevel.Limited:
                    return ThoughtState.ActiveAtStage(4);

                case MenuVarietyLevel.BelowAverage:
                    return ThoughtState.ActiveAtStage(5);

                case MenuVarietyLevel.Average:
                    return ThoughtState.ActiveAtStage(6);

                case MenuVarietyLevel.AboveAverage:
                    return ThoughtState.ActiveAtStage(7);

                case MenuVarietyLevel.Good:
                    return ThoughtState.ActiveAtStage(8);

                case MenuVarietyLevel.Great:
                    return ThoughtState.ActiveAtStage(9);

                case MenuVarietyLevel.Excellent:
                    return ThoughtState.ActiveAtStage(10);

                case MenuVarietyLevel.Exceptional:
                    return ThoughtState.ActiveAtStage(11);

                case MenuVarietyLevel.Overwhelming:
                    return ThoughtState.ActiveAtStage(12);

                default:
                    throw new NotImplementedException($"MenuVarietyLevel '{need_FoodVariety.CurCategory}' is not implemented in the switch statement.");
            }
		}
	}
}
