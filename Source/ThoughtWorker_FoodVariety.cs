namespace VarietyMatters
{
    using System;
    using RimWorld;
    using RimWorld.Planet;
    using UnityEngine;
    using Verse;

    // Token: 0x02000010 RID: 16
    public class ThoughtWorker_FoodVariety : ThoughtWorker
	{
        public override float MoodMultiplier(Pawn p)
        {
            // US-031 stage-aware asymmetric scaling. The variety thought has 13 stages where
            // vanilla baseMoodEffect ranges from -6 (barren menu) through 0 (average) to +6
            // (overwhelming). The player sets maxNegativeMood (default -12, applied at stage 0)
            // and maxPositiveMood (default +12, applied at stage 12); intermediate stages
            // interpolate linearly toward 0. Per-stage scale = abs(maxSide) / 6. Stage 6 is
            // "average" with baseMoodEffect 0, so the multiplier there is irrelevant.
            ThoughtState state = this.CurrentStateInternal(p);
            float scale = 2f; // vanilla-equivalent fallback if state is inactive
            if (state.Active)
            {
                int vanillaEffect = state.StageIndex - 6; // 0..12 → -6..+6
                if (vanillaEffect < 0)
                {
                    scale = Mathf.Abs(ModSettings_VarietyMatters.maxNegativeMood) / 6f;
                }
                else if (vanillaEffect > 0)
                {
                    scale = ModSettings_VarietyMatters.maxPositiveMood / 6f;
                }
                else
                {
                    scale = 1f; // stage 6 has baseMoodEffect 0; multiplier doesn't matter
                }
            }

            TraitDef gourmand = DefOf_VarietyMatters.Gourmand;
            if (gourmand != null && p.story.traits.HasTrait(gourmand))
            {
                scale *= 2;
            }

            return Mathf.Max(0f, scale);
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
