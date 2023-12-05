namespace VarietyMatters
{
    using RimWorld;
    using Verse;

    // Token: 0x02000007 RID: 7
    [DefOf]
	public class DefOf_VarietyMatters
	{
		// Token: 0x0600000B RID: 11 RVA: 0x0000241D File Offset: 0x0000061D
		static DefOf_VarietyMatters()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(DefOf_VarietyMatters));
		}

		// Token: 0x04000002 RID: 2
		public static FoodVariety_NeedDef FoodVariety;

		// Token: 0x04000003 RID: 3
		[MayRequireIdeology]
		public static PreceptDef FungusEating_Despised;

        [MayRequireIdeology]
        public static PreceptDef Cannibalism_Acceptable;

        [MayRequireIdeology]
        public static PreceptDef NutrientPasteEating_Disgusting;

        [MayRequireIdeology]
        public static PreceptDef InsectMeatEating_Despised_Classic;

        public static ThingDef MealNutrientPaste;
    }
}
