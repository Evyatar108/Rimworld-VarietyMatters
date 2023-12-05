namespace VarietyMatters
{
    using System;
    using RimWorld;
    using Verse;

    // Token: 0x02000006 RID: 6
    public class CompVariety : ThingComp
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000006 RID: 6 RVA: 0x0000210F File Offset: 0x0000030F
		public CompProperties_Variety Props
		{
			get
			{
				return (CompProperties_Variety)this.props;
			}
		}

		// Token: 0x06000007 RID: 7 RVA: 0x0000211C File Offset: 0x0000031C
		public override void PostIngested(Pawn ingester)
		{
			try
			{
				if (!WildManUtility.NonHumanlikeOrWildMan(ingester) && (ingester.IsColonist || ingester.IsPrisoner || ingester.IsSlave))
				{
					Pawn_NeedsTracker needs = ingester.needs;
					Need_FoodVariety need = needs?.TryGetNeed(DefOf_VarietyMatters.FoodVariety) as Need_FoodVariety;

					if (need != null && !need.Disabled)
					{
						VarietyRecord.UpdateVarietyRecord(ingester, this.parent);
					}
				}
			}
			catch (Exception e)
			{
				string error = e.ToString();
				Log.ErrorOnce(error, error.GetHashCode());
            }
		}

		// Token: 0x06000008 RID: 8 RVA: 0x00002174 File Offset: 0x00000374
		public override bool AllowStackWith(Thing other)
		{
			if (ModSettings_VarietyMatters.foodTrackingType != New.FoodTrackingType.ByMealNames && ModSettings_VarietyMatters.stackByIngredients)
			{
				CompIngredients parentIngredients = this.parent.GetComp<CompIngredients>();
				CompIngredients otherIngredients = ((ThingWithComps)other).GetComp<CompIngredients>();
				bool nullIngredients = parentIngredients == null || otherIngredients == null;
				if (nullIngredients)
				{
					return true;
				}
				bool emptyIngredients = parentIngredients.ingredients.Count == 0 && otherIngredients.ingredients.Count == 0;
				if (emptyIngredients)
				{
					return true;
				}
				bool differentIngredientCount = parentIngredients.ingredients.Count != otherIngredients.ingredients.Count;
				if (differentIngredientCount)
				{
					return false;
				}
				for (int i = 0; i < parentIngredients.ingredients.Count; i++)
				{
					bool doesNotHaveIngredient = !otherIngredients.ingredients.Contains(parentIngredients.ingredients[i]);
					if (doesNotHaveIngredient)
					{
						return false;
					}
				}
			}
			return true;
		}

		// Token: 0x06000009 RID: 9 RVA: 0x0000226C File Offset: 0x0000046C
		public static void FoodsAvailable()
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				bool flag = !thingDef.IsNutritionGivingIngestible || !thingDef.ingestible.HumanEdible || thingDef.IsCorpse;
				if (!flag)
				{
					bool flag2 = thingDef.HasComp(typeof(CompIngredients)) && (int)thingDef.ingestible.preferability >= 5;
					if (flag2)
					{
						num++;
					}
					else
					{
						bool flag3 = thingDef.ingestible.joyKind != null;
						if (flag3)
						{
							num++;
							num2++;
						}
						else
						{
							bool flag4 = !thingDef.HasComp(typeof(CompHatcher));
							if (flag4)
							{
								num2++;
								bool isMeat = thingDef.IsMeat;
								if (isMeat)
								{
									num3++;
								}
							}
						}
					}
				}
			}
			VarietyExpectation.meatTypes = (float)num3;
			bool flag5 = num <= 22;
			if (flag5)
			{
				VarietyExpectation.moddedMeals = ((float)num - 24f) / (36f - (float)num);
			}
			else
			{
				VarietyExpectation.moddedMeals = ((float)num - 24f) / (24f + ((float)num * 2f));
			}
			bool flag6 = num2 <= 66;
			if (flag6)
			{
				VarietyExpectation.moddedIngredients = ((float)num2 - 66f) / (132f - (float)num2);
			}
			else
			{
				VarietyExpectation.moddedIngredients = Math.Min(((float)num2 - 66f) / (float)num2, 0.8f);
			}
		}
	}
}
