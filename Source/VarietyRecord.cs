namespace VarietyMatters
{
    using System.Collections.Generic;
    using System.Linq;
    using VarietyMatters.New;
    using Verse;

    // Token: 0x02000015 RID: 21
    public class VarietyRecord : GameComponent
	{
		// Token: 0x06000040 RID: 64 RVA: 0x000048A8 File Offset: 0x00002AA8
		public VarietyRecord(Game game)
		{
			VarietyRecord.varietyRecord = new Dictionary<Pawn, DietTracker>();
		}

		// Token: 0x06000041 RID: 65 RVA: 0x000048BC File Offset: 0x00002ABC
		public static DietTracker GetVarietyRecord(Pawn trackedPawn)
		{
            DietTracker pawn_VarietyTracker;
			bool flag = VarietyRecord.varietyRecord.TryGetValue(trackedPawn, out pawn_VarietyTracker);
            DietTracker result;
			if (flag)
			{
				result = pawn_VarietyTracker;
			}
			else
			{
				result = null;
			}
			return result;
		}

		// Token: 0x06000042 RID: 66 RVA: 0x000048E8 File Offset: 0x00002AE8
		public static void UpdateVarietyRecord(Pawn trackedPawn, Thing foodSourceThing)
		{
			float memoryMultiplier = ModSettings_VarietyMatters.memoryMultiplier;

            int varietyExpectation = VarietyExpectation.GetVarietyExpectation(trackedPawn);
			int maxEatenFoodSourceInMemory = (int)(varietyExpectation * memoryMultiplier);
            if (!VarietyRecord.varietyRecord.TryGetValue(trackedPawn, out var tracker))
			{
                tracker = new DietTracker(trackedPawn, maxEatenFoodSourceInMemory);
                VarietyRecord.varietyRecord.Add(trackedPawn, tracker);
                VarietyRecord.trackedPawns.Add(trackedPawn);
                VarietyRecord.pawnRecords.Add(tracker);
			}

            EatenFoodSource foodSource = FoodSourceFactory.CreateOrGetFoodSourceFromThing(foodSourceThing);

			tracker.UpdateMaxFoodInMemory(maxEatenFoodSourceInMemory);

			tracker.AddFoodSource(foodSource);

			VarietyAdjuster.AdjustVarietyLevel(tracker, varietyExpectation);
		}

		// Token: 0x06000043 RID: 67 RVA: 0x0000493C File Offset: 0x00002B3C
		public static void RemoveTrackedPawn(Pawn trackedPawn)
		{
			if (VarietyRecord.varietyRecord.TryGetValue(trackedPawn, out var tracker))
			{
				VarietyRecord.varietyRecord.Remove(trackedPawn);
				VarietyRecord.trackedPawns.Remove(trackedPawn);
				VarietyRecord.pawnRecords.Remove(tracker);
			}
		}

		// Token: 0x06000044 RID: 68 RVA: 0x00004968 File Offset: 0x00002B68
		public override void FinalizeInit()
		{
			if (VarietyRecord.varietyRecord != null)
			{
				foreach (Pawn pawn in VarietyRecord.varietyRecord.Keys)
				{
					bool dead = pawn.Dead;
					if (dead)
					{
						VarietyRecord.RemoveTrackedPawn(pawn);
					}
				}
			}
			CompVariety.FoodsAvailable();
			base.FinalizeInit();
		}

		// Token: 0x06000045 RID: 69 RVA: 0x000049EC File Offset: 0x00002BEC
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look<Pawn, DietTracker>(ref VarietyRecord.varietyRecord, "VarietyRecordV2", LookMode.Reference, LookMode.Deep, ref trackedPawns, ref pawnRecords, logNullErrors: true);

			if (Scribe.mode == LoadSaveMode.PostLoadInit && varietyRecord == null)
			{
				varietyRecord = new Dictionary<Pawn, DietTracker>();
				trackedPawns = new List<Pawn>();
				pawnRecords = new List<DietTracker>();
            }

			if (Scribe.mode == LoadSaveMode.Saving)
			{
                eatenFoodSources = varietyRecord?.Values?.SelectMany(x => x.EatenFoodSourcesByOrder)?.ToList()?.Distinct()?.ToList() ?? new List<EatenFoodSource>();
			}

			Scribe_Collections.Look<EatenFoodSource>(ref eatenFoodSources, "eatenFoodSources", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && eatenFoodSources != null)
			{
				FoodSourceFactory.Init(eatenFoodSources);
            }
		}

		// Token: 0x04000030 RID: 48
		public static List<DietTracker> pawnRecords = new List<DietTracker>();

		// Token: 0x04000031 RID: 49
		private static List<Pawn> trackedPawns = new List<Pawn>();

		// Token: 0x04000032 RID: 50
		private static Dictionary<Pawn, DietTracker> varietyRecord;

		private static List<EatenFoodSource> eatenFoodSources;

    }
}
