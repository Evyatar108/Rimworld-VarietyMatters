using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VarietyMatters
{
	// Token: 0x0200000F RID: 15
	public class Pawn_VarietyTracker : IExposable
	{
		// Token: 0x06000024 RID: 36 RVA: 0x000036E0 File Offset: 0x000018E0
		public static void TrackRecentlyConsumed(ref Pawn_VarietyTracker pawnRecord, Pawn ingester, Thing foodSource)
		{
			float baseVarietyExpectation = VarietyExpectation.GetBaseVarietyExpectation(ingester);
			int varietyExpectation = VarietyExpectation.GetVarietyExpectation(ingester);
			bool flag = pawnRecord.recentlyConsumed == null;
			if (flag)
			{
				pawnRecord.recentlyConsumed = new List<string>();
				int num = 0;
				while ((float)num < baseVarietyExpectation - 1f)
				{
					pawnRecord.recentlyConsumed.Add(num.ToString());
					num++;
				}
				pawnRecord.lastVariety = new List<string>();
			}
			Pawn_VarietyTracker.TrackVarieties(ref pawnRecord, ingester, foodSource, varietyExpectation);
			pawnRecord.recentVarieties = pawnRecord.recentlyConsumed.Distinct<string>().Count<string>();
			bool flag2 = pawnRecord.recentlyConsumed[0] == "BadVariety";
			if (flag2)
			{
				pawnRecord.recentVarieties--;
			}
			bool flag3 = ingester.needs.TryGetNeed<Need_FoodVariety>() != null && !ingester.needs.TryGetNeed<Need_FoodVariety>().Disabled;
			if (flag3)
			{
				Pawn_VarietyTracker.AdjustVarietyLevel(pawnRecord.recentVarieties, ingester, varietyExpectation, baseVarietyExpectation);
			}
			Pawn_VarietyTracker.RemoveVarieties(ref pawnRecord.recentlyConsumed, varietyExpectation, (int)baseVarietyExpectation);
		}

		// Token: 0x06000025 RID: 37 RVA: 0x000037F0 File Offset: 0x000019F0
		public static void TrackVarieties(ref Pawn_VarietyTracker pawnRecord, Pawn ingester, Thing foodSource, int varietyExpectation)
		{
			CompIngredients compIngredients = ThingCompUtility.TryGetComp<CompIngredients>(foodSource);
			FoodPreferability preferability = foodSource.def.ingestible.preferability;
			string label = foodSource.def.label;
			pawnRecord.lastVariety.Clear();
			int num = (int)VarietyExpectation.GetBaseVarietyExpectation(ingester);
			bool flag = RottableUtility.IsNotFresh(foodSource);
			if (flag)
			{
				pawnRecord.recentlyConsumed.Insert(0, "BadVariety");
				pawnRecord.lastVariety.Add("rotten food");
			}
			else
			{
				bool flag2 = ModsConfig.IdeologyActive && ingester.Ideo != null;
				if (flag2)
				{
					bool flag3 = FoodUtility.IsVeneratedAnimalMeatOrCorpseOrHasIngredients(foodSource, ingester);
					if (flag3)
					{
						pawnRecord.recentlyConsumed.Insert(0, "BadVariety");
						pawnRecord.lastVariety.Add("venerated animal");
						return;
					}
					bool flag4 = !FoodUtility.AcceptableCarnivore(foodSource);
					if (flag4)
					{
						bool flag5 = FoodUtility.HasMeatEatingRequiredPrecept(ingester.Ideo);
						if (flag5)
						{
							pawnRecord.recentlyConsumed.Insert(0, "BadVariety");
							pawnRecord.recentlyConsumed.Add("plant food");
							return;
						}
						bool flag6 = FoodUtility.HasVegetarianRequiredPrecept(ingester.Ideo);
						if (flag6)
						{
							int num2 = Rand.Range(0, num / 3);
							bool flag7 = !pawnRecord.recentlyConsumed.Contains(("vegetarian" + num2.ToString()).ToString());
							if (flag7)
							{
								pawnRecord.recentlyConsumed.Add(("vegetarian" + num2.ToString()).ToString());
							}
						}
					}
					bool flag8 = !FoodUtility.AcceptableVegetarian(foodSource) && FoodUtility.HasVegetarianRequiredPrecept(ingester.Ideo);
					if (flag8)
					{
						pawnRecord.recentlyConsumed.Insert(0, "BadVariety");
						pawnRecord.recentlyConsumed.Add("disgusting meat");
						return;
					}
					bool isFungus = foodSource.def.IsFungus;
					if (isFungus)
					{
						List<Precept> preceptsListForReading = ingester.Ideo.PreceptsListForReading;
						for (int i = 0; i < preceptsListForReading.Count; i++)
						{
							bool flag9 = preceptsListForReading[i].def == DefOf_VarietyMatters.FungusEating_Preferred;
							if (flag9)
							{
								pawnRecord.recentlyConsumed.Add(label);
								pawnRecord.lastVariety.Add(label);
								return;
							}
						}
					}
				}
				bool flag10 = (int)preferability <= 4;
				if (flag10)
				{
					bool flag11 = FoodUtility.IsHumanlikeCorpseOrHumanlikeMeat(foodSource, foodSource.def);
					if (flag11)
					{
						bool flag12 = ingester.story.traits.HasTrait(TraitDefOf.Cannibal);
						if (flag12)
						{
							pawnRecord.recentlyConsumed.Add("Raw humanlike flesh");
							pawnRecord.lastVariety.Add("tasty raw humanlike flesh");
						}
						else
						{
							ThoughtHandler thoughts = ingester.needs.mood.thoughts;
							object obj;
							if (thoughts == null)
							{
								obj = null;
							}
							else
							{
								MemoryThoughtHandler memories = thoughts.memories;
								obj = ((memories != null) ? memories.GetFirstMemoryOfDef(ThoughtDefOf.AteHumanlikeMeatDirect) : null);
							}
							bool flag13 = obj == null;
							if (flag13)
							{
								pawnRecord.recentlyConsumed.Add(label);
							}
							pawnRecord.recentlyConsumed.Insert(0, "BadVariety");
							pawnRecord.lastVariety.Add(label);
						}
					}
					else
					{
						bool flag14 = (int)FoodUtility.GetMeatSourceCategory(foodSource.def) != 2;
						if (flag14)
						{
							ThoughtHandler thoughts2 = ingester.needs.mood.thoughts;
							object obj2;
							if (thoughts2 == null)
							{
								obj2 = null;
							}
							else
							{
								MemoryThoughtHandler memories2 = thoughts2.memories;
								obj2 = ((memories2 != null) ? memories2.GetFirstMemoryOfDef(ThoughtDefOf.AteInsectMeatDirect) : null);
							}
							bool flag15 = obj2 != null;
							if (flag15)
							{
								pawnRecord.recentlyConsumed.Insert(0, "BadVariety");
							}
							pawnRecord.recentlyConsumed.Add(label);
							pawnRecord.lastVariety.Add(label);
						}
						else
						{
							bool flag16 = foodSource.def.ingestible.joyKind == null;
							if (flag16)
							{
								pawnRecord.recentlyConsumed.Insert(0, "BadVariety");
							}
							pawnRecord.recentlyConsumed.Add(label);
							pawnRecord.lastVariety.Add(label);
						}
					}
				}
				else
				{
					bool flag17 = (int)preferability == 5;
					if (flag17)
					{
						pawnRecord.recentlyConsumed.Add("Raw" + label);
						pawnRecord.lastVariety.Add("raw " + label);
					}
					else
					{
						bool flag18 = compIngredients == null || ModSettings_VarietyMatters.ignoreIngredients || ModSettings_VarietyMatters.maxVariety;
						if (flag18)
						{
							pawnRecord.recentlyConsumed.Add(label);
							pawnRecord.lastVariety.Add(label);
						}
						bool flag19 = compIngredients == null || ModSettings_VarietyMatters.ignoreIngredients;
						if (!flag19)
						{
							bool flag20 = compIngredients.ingredients.Count == 0 && !ModSettings_VarietyMatters.maxVariety;
							if (flag20)
							{
								bool flag21 = (int)preferability >= 9;
								if (flag21)
								{
									pawnRecord.recentlyConsumed.Add("Mystery lavish" + Rand.Range(0, num / 2).ToString());
									pawnRecord.lastVariety.Add("lavish ingredient");
								}
								bool flag22 = (int)preferability >= 8;
								if (flag22)
								{
									pawnRecord.recentlyConsumed.Add("Mystery meat" + Rand.Range(0, num / 2).ToString());
									pawnRecord.lastVariety.Add("fine ingredient");
								}
								pawnRecord.recentlyConsumed.Add("Mystery ingredient" + Rand.Range(0, (varietyExpectation / 2 + num) / 2).ToString());
								pawnRecord.lastVariety.Add("unknown ingredient");
							}
							else
							{
								for (int j = 0; j < compIngredients.ingredients.Count; j++)
								{
									ThingDef thingDef = compIngredients.ingredients[j];
									label = thingDef.label;
									bool isNutritionGivingIngestible = thingDef.IsNutritionGivingIngestible;
									if (isNutritionGivingIngestible)
									{
										bool flag23 = (int)preferability == 9 && !pawnRecord.recentlyConsumed.Contains("Lavish" + label);
										if (flag23)
										{
											pawnRecord.recentlyConsumed.Add("Lavish" + label);
											pawnRecord.lastVariety.Add("lavishly prepared " + label);
										}
										else
										{
											pawnRecord.recentlyConsumed.Add(label);
											pawnRecord.lastVariety.Add(label);
										}
									}
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x06000026 RID: 38 RVA: 0x00003E40 File Offset: 0x00002040
		public static void RemoveVarieties(ref List<string> recentlyConsumed, int varietyExpectation, int baseVarietyExpectation)
		{
			int num = Mathf.Max(baseVarietyExpectation, varietyExpectation) * 2;
			for (int i = recentlyConsumed.Count; i > num; i--)
			{
				recentlyConsumed.RemoveAt(Rand.Range(0, num - 1));
			}
		}

		// Token: 0x06000027 RID: 39 RVA: 0x00003E84 File Offset: 0x00002084
		public static void AdjustVarietyLevel(int distinctVarieties, Pawn ingester, int varietyExpectation, float baseExpectation)
		{
			Need_FoodVariety need_FoodVariety = ingester.needs.TryGetNeed<Need_FoodVariety>();
			float num = (float)(2 * (distinctVarieties - varietyExpectation));
			bool flag = need_FoodVariety.CurLevel > 0.6f && distinctVarieties <= varietyExpectation;
			if (flag)
			{
				num -= baseExpectation / 3f + 1f;
			}
			bool flag2 = need_FoodVariety.CurLevel < 0.4f && distinctVarieties >= varietyExpectation;
			if (flag2)
			{
				num += baseExpectation / 3f + 1f;
			}
			num = Mathf.Clamp(num, 0f - (baseExpectation / 2f + 4f), baseExpectation / 2f + 4f);
			need_FoodVariety.CurLevel = Mathf.Clamp(need_FoodVariety.CurLevel + num / 100f, 0f, 1f);
		}

		// Token: 0x06000028 RID: 40 RVA: 0x00003F4C File Offset: 0x0000214C
		public void ExposeData()
		{
			Scribe_Collections.Look<string>(ref this.lastVariety, "lastVariety", (LookMode)1, Array.Empty<object>());
			Scribe_Collections.Look<string>(ref this.recentlyConsumed, "recentlyConsumed", (LookMode)1, Array.Empty<object>());
			Scribe_Values.Look<int>(ref this.recentVarieties, "recentVarieties", 0, false);
		}

		// Token: 0x04000024 RID: 36
		public List<string> recentlyConsumed;

		// Token: 0x04000025 RID: 37
		public List<string> lastVariety;

		// Token: 0x04000026 RID: 38
		public int recentVarieties;
	}
}
