using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VarietyMatters
{
	// Token: 0x02000010 RID: 16
	public class ThoughtWorker_FoodVariety : ThoughtWorker
	{
        // Token: 0x0600002A RID: 42 RVA: 0x00003FA4 File Offset: 0x000021A4
        protected override ThoughtState CurrentStateInternal(Pawn p)
		{
			Need_FoodVariety need_FoodVariety = p.needs.TryGetNeed<Need_FoodVariety>();
			bool flag = need_FoodVariety == null || FoodUtility.Starving(p) || p.health.hediffSet.HasHediff(HediffDefOf.FoodPoisoning, true) || CaravanUtility.IsCaravanMember(p) || (ModSettings_VarietyMatters.sickPawns && HealthAIUtility.ShouldSeekMedicalRest(p));
			ThoughtState result;
			if (flag)
			{
				result = ThoughtState.Inactive;
			}
			else
			{
				MenuCategory curCategory = need_FoodVariety.CurCategory;
				if (!true)
				{
				}
				ThoughtState thoughtState;
				switch (curCategory)
				{
				case MenuCategory.None:
					thoughtState = ThoughtState.Inactive;
					break;
				case MenuCategory.Empty:
					thoughtState = ThoughtState.ActiveAtStage(0);
					break;
				case MenuCategory.Sparse:
					thoughtState = ThoughtState.ActiveAtStage(1);
					break;
				case MenuCategory.Limited:
					thoughtState = ThoughtState.ActiveAtStage(2);
					break;
				case MenuCategory.Simple:
					thoughtState = ThoughtState.ActiveAtStage(3);
					break;
				case MenuCategory.Fine:
					thoughtState = ThoughtState.ActiveAtStage(4);
					break;
				case MenuCategory.Lavish:
					thoughtState = ThoughtState.ActiveAtStage(5);
					break;
				case MenuCategory.Gourmet:
					thoughtState = ThoughtState.ActiveAtStage(6);
					break;
				default:
					throw new NotImplementedException();
				}
				if (!true)
				{
				}
				result = thoughtState;
			}
			return result;
		}
	}
}
