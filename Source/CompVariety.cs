namespace VarietyMatters
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using RimWorld;
    using Verse;

    // Token: 0x02000006 RID: 6
    public class CompVariety : ThingComp
  {
        private string cachedIngredientsKey;
        private string cachedFoodSourceKey;

        private static readonly List<string> _ingredientDefNamesScratch = new List<string>();
        private static readonly StringBuilder _ingredientsKeyBuilder = new StringBuilder();

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
				Pawn_NeedsTracker needs = ingester.needs;
				Need_FoodVariety need = needs?.TryGetNeed(DefOf_VarietyMatters.FoodVariety) as Need_FoodVariety;

				if (need != null && !need.Disabled)
				{
					VarietyRecord.UpdateVarietyRecord(ingester, this.parent);
				}
			}
			catch (Exception e)
			{
				string error = e.ToString();
				
				Log.ErrorOnce(error, error.GetHashCode());
			}
		}

				   public string GetFoodSourceKey()
				   {
				       if (cachedFoodSourceKey == null)
				       {
				           cachedFoodSourceKey = new New.EatenFoodSource(parent).GetFoodSourceKey();
				       }
				       return cachedFoodSourceKey;
				   }

		// Token: 0x06000008 RID: 8 RVA: 0x00002174 File Offset: 0x00000374
		      public string GetIngredientsKey()
		      {
		          if (cachedIngredientsKey == null)
		          {
		              CompIngredients ingredients = this.parent.GetComp<CompIngredients>();
		              if (ingredients == null || ingredients.ingredients.Count == 0)
		              {
		                  cachedIngredientsKey = "";
		              }
		              else
		              {
		                  List<ThingDef> source = ingredients.ingredients;
		                  _ingredientDefNamesScratch.Clear();
		                  for (int i = 0; i < source.Count; i++)
		                  {
		                      _ingredientDefNamesScratch.Add(source[i].defName);
		                  }
		                  _ingredientDefNamesScratch.Sort();
		                  _ingredientsKeyBuilder.Clear();
		                  for (int i = 0; i < _ingredientDefNamesScratch.Count; i++)
		                  {
		                      if (i > 0) _ingredientsKeyBuilder.Append(',');
		                      _ingredientsKeyBuilder.Append(_ingredientDefNamesScratch[i]);
		                  }
		                  cachedIngredientsKey = _ingredientsKeyBuilder.ToString();
		              }
		          }
		          return cachedIngredientsKey;
		      }

		      public override bool AllowStackWith(Thing other)
		      {
		          if (this.parent.def.label == "kibble")
		          {
		              return true;
		          }

		          if (ModSettings_VarietyMatters.foodTrackingType != New.FoodTrackingType.ByMealNames && ModSettings_VarietyMatters.stackByIngredients)
		          {
		              CompVariety otherComp = other.TryGetComp<CompVariety>();
		              if (otherComp == null)
		              {
		                  return true;
		              }

		              return this.GetIngredientsKey() == otherComp.GetIngredientsKey();
		          }
		          return true;
		      }

		// Token: 0x06000009 RID: 9 RVA: 0x0000226C File Offset: 0x0000046C
		// OPTIMIZATION: Cache food analysis results to avoid repeated DefDatabase iteration
		private static bool foodAnalysisComplete = false;
		
		public static void FoodsAvailable()
		{
			// OPTIMIZATION: Only analyze once per game session
			if (foodAnalysisComplete) return;
			
			int mealCount = 0;
			int ingredientCount = 0;
			int meatTypeCount = 0;
			
			// OPTIMIZATION: Use cached references for frequently accessed comp types
			var compIngredientsType = typeof(CompIngredients);
			var compHatcherType = typeof(CompHatcher);
			
			// OPTIMIZATION: Pre-filter for nutrition-giving ingestibles to reduce iteration
			foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				// OPTIMIZATION: Early exit for non-food items
				if (!thingDef.IsNutritionGivingIngestible ||
				    !thingDef.ingestible.HumanEdible ||
				    thingDef.IsCorpse)
					continue;
				
				// OPTIMIZATION: Single preferability check instead of casting
				bool isMeal = thingDef.HasComp(compIngredientsType) &&
				             thingDef.ingestible.preferability >= FoodPreferability.MealAwful;
				
				if (isMeal)
				{
					mealCount++;
				}
				else
				{
					bool hasJoy = thingDef.ingestible.joyKind != null;
					if (hasJoy)
					{
						mealCount++;
						ingredientCount++;
					}
					else if (!thingDef.HasComp(compHatcherType))
					{
						ingredientCount++;
						if (thingDef.IsMeat)
						{
							meatTypeCount++;
						}
					}
				}
			}
			
			// OPTIMIZATION: Direct assignment instead of redundant calculations
			VarietyExpectation.meatTypes = (float)meatTypeCount;
			
			// Calculate modded meal ratio
			if (mealCount <= 22)
			{
				VarietyExpectation.moddedMeals = ((float)mealCount - 24f) / (36f - (float)mealCount);
			}
			else
			{
				VarietyExpectation.moddedMeals = ((float)mealCount - 24f) / (24f + ((float)mealCount * 2f));
			}
			
			// Calculate modded ingredient ratio
			if (ingredientCount <= 66)
			{
				VarietyExpectation.moddedIngredients = ((float)ingredientCount - 66f) / (132f - (float)ingredientCount);
			}
			else
			{
				VarietyExpectation.moddedIngredients = Math.Min(((float)ingredientCount - 66f) / (float)ingredientCount, 0.8f);
			}
			
			foodAnalysisComplete = true;
		}
	}
}
