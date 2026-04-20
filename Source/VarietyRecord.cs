namespace VarietyMatters
{
    using System;
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
		}

		public static bool TryGetVarietyRecord(Pawn trackedPawn, out DietTracker tracker)
		{
			if (trackedPawn == null)
			{
				tracker = null;
				return false;
			}

			return VarietyRecord.DietRecord.TryGetValue(trackedPawn, out tracker);
		}

		// Token: 0x06000041 RID: 65 RVA: 0x000048BC File Offset: 0x00002ABC
		public static DietTracker GetVarietyRecord(Pawn trackedPawn)
		{
			if (trackedPawn == null)
			{
				throw new ArgumentNullException(nameof(trackedPawn));
			}

            DietTracker tracker;
			if (!VarietyRecord.DietRecord.TryGetValue(trackedPawn, out tracker))
			{
				tracker = AddVarietyRecord(trackedPawn);
			}

			return tracker;
		}

		private static DietTracker AddVarietyRecord(Pawn pawn)
		{
            if (pawn == null)
            {
                throw new ArgumentNullException(nameof(pawn));
            }

            var tracker = new DietTracker(pawn);
            VarietyRecord.DietRecord.Add(pawn, tracker);
            VarietyRecord.TrackedPawns.Add(pawn);
            VarietyRecord.PawnRecords.Add(tracker);

			return tracker;
        }

		// Token: 0x06000042 RID: 66 RVA: 0x000048E8 File Offset: 0x00002AE8
		public static void UpdateVarietyRecord(Pawn trackedPawn, Thing foodSourceThing)
		{
            if (trackedPawn == null)
            {
                throw new ArgumentNullException(nameof(trackedPawn));
            }

            if (foodSourceThing == null)
            {
                throw new ArgumentNullException(nameof(foodSourceThing));
            }

            if (!VarietyRecord.DietRecord.TryGetValue(trackedPawn, out var tracker))
			{
				tracker = AddVarietyRecord(trackedPawn);
			}

            EatenFoodSource foodSource = FoodSourceFactory.CreateOrGetFoodSourceFromThing(foodSourceThing);

            string newFoodSourceKey = foodSource.GetFoodSourceKey();

            tracker.UpdateMaxFoodInMemory();

			tracker.AddFoodSource(foodSource);

			Current.Game.GetComponent<FavouriteFoodRecord>()?.OnMealIngested(
				trackedPawn, foodSource, foodSource.VarietyScore, VarietyAdjuster.BaselineForTierPublic(trackedPawn));

			VarietyAdjuster.AdjustVarietyLevel(tracker);
		}

		// Rebuild every tracker's cached totals + per-meal variety scores. Invoked when a
		// setting that affects scoring flips (e.g. foodTrackingType -> ByIngredientOverlap)
		// so prior meals get a retroactive Jaccard score instead of the default 1f.
		public static void ReCountAll()
		{
			if (dietRecord == null)
			{
				return;
			}

			foreach (DietTracker tracker in dietRecord.Values)
			{
				if (tracker?.Pawn != null && !tracker.Pawn.Dead)
				{
					tracker.ReCount();
					VarietyAdjuster.AdjustVarietyLevel(tracker);
				}
			}
		}

		// Token: 0x06000043 RID: 67 RVA: 0x0000493C File Offset: 0x00002B3C
		public static void RemoveTrackedPawn(Pawn trackedPawn)
		{
			if (VarietyRecord.DietRecord.TryGetValue(trackedPawn, out var tracker))
			{
				VarietyRecord.DietRecord.Remove(trackedPawn);
				VarietyRecord.TrackedPawns.Remove(trackedPawn);
				VarietyRecord.PawnRecords.Remove(tracker);
			}
		}

		// Token: 0x06000044 RID: 68 RVA: 0x00004968 File Offset: 0x00002B68
		public override void FinalizeInit()
		{
			if (VarietyRecord.DietRecord != null)
			{
				List<Pawn> deadPawns = VarietyRecord.DietRecord.Keys.Where(p => p.Dead).ToList();
				foreach (var p in deadPawns)
				{
					VarietyRecord.RemoveTrackedPawn(p);
				}
			}

			CompVariety.FoodsAvailable();
			base.FinalizeInit();
		}

		// Token: 0x06000045 RID: 69 RVA: 0x000049EC File Offset: 0x00002BEC
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look<Pawn, DietTracker>(ref VarietyRecord.dietRecord, "VarietyRecordV2", LookMode.Reference, LookMode.Deep, ref trackedPawns, ref pawnRecords, logNullErrors: true);

            if (Scribe.mode == LoadSaveMode.Saving)
   {
    eatenFoodSources = GetDistinctEatenFoodSources();
   }

			Scribe_Collections.Look<EatenFoodSource>(ref eatenFoodSources, label: "eatenFoodSources", saveDestroyedThings: true, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && eatenFoodSources != null)
			{
				FoodSourceFactory.Init(eatenFoodSources);
            }
		}

        public static List<DietTracker> PawnRecords
		{
			get
			{
				if (pawnRecords == null)
				{
					pawnRecords = new List<DietTracker>();
				}

				return pawnRecords;
			}

			set => pawnRecords = value;
		}

        public static List<Pawn> TrackedPawns
        {
            get
            {
                if (trackedPawns == null)
                {
                    trackedPawns = new List<Pawn>();
                }

                return trackedPawns;
            }

            set => trackedPawns = value;
        }

        public static Dictionary<Pawn, DietTracker> DietRecord 
        {
            get
            {
                if (dietRecord == null)
                {
                    dietRecord = new Dictionary<Pawn, DietTracker>();
                }

                return dietRecord;
            }

            set => dietRecord = value;
        }

        public static List<DietTracker> pawnRecords = new List<DietTracker>();
		private static List<Pawn> trackedPawns = new List<Pawn>();
		private static Dictionary<Pawn, DietTracker> dietRecord = new Dictionary<Pawn, DietTracker>();

		private static List<EatenFoodSource> eatenFoodSources;

		      /// <summary>
		      /// Optimized method to get distinct eaten food sources without expensive LINQ chains
		      /// </summary>
		      private static List<EatenFoodSource> GetDistinctEatenFoodSources()
		      {
		          if (DietRecord?.Values == null)
		              return new List<EatenFoodSource>();

		          var seen = new HashSet<string>(); // Use string hash instead of object comparison
		          var result = new List<EatenFoodSource>();

		          foreach (var tracker in DietRecord.Values)
		          {
		              if (tracker?.EatenFoodSourcesByOrder == null)
		                  continue;

		              foreach (var eatenByPawn in tracker.EatenFoodSourcesByOrder)
		              {
		                  if (eatenByPawn?.Source == null)
		                      continue;

		                  string uniqueId = eatenByPawn.Source.GetUniqueLoadID();
		                  if (seen.Add(uniqueId)) // HashSet.Add returns true if item was not already present
		                  {
		                      result.Add(eatenByPawn.Source);
		                  }
		              }
		          }

		          return result;
		      }

        private class EatenFoodSourceEqualityComparer : IEqualityComparer<EatenFoodSource>
        {
			public readonly static EatenFoodSourceEqualityComparer Instance = new EatenFoodSourceEqualityComparer();
            public bool Equals(EatenFoodSource x, EatenFoodSource y)
            {
				return x.GetUniqueLoadID() == y.GetUniqueLoadID();
            }

            public int GetHashCode(EatenFoodSource obj)
            {
				return obj.GetUniqueLoadID().GetHashCode();
            }
        }
    }
}
