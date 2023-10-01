using System;
using System.Collections.Generic;
using Verse;

namespace VarietyMatters
{
	// Token: 0x02000015 RID: 21
	public class VarietyRecord : GameComponent
	{
		// Token: 0x06000040 RID: 64 RVA: 0x000048A8 File Offset: 0x00002AA8
		public VarietyRecord(Game game)
		{
			VarietyRecord.varietyRecord = new Dictionary<Pawn, Pawn_VarietyTracker>();
		}

		// Token: 0x06000041 RID: 65 RVA: 0x000048BC File Offset: 0x00002ABC
		public static Pawn_VarietyTracker GetVarietyRecord(Pawn trackedPawn)
		{
			Pawn_VarietyTracker pawn_VarietyTracker;
			bool flag = VarietyRecord.varietyRecord.TryGetValue(trackedPawn, out pawn_VarietyTracker);
			Pawn_VarietyTracker result;
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
		public static void UpdateVarietyRecord(Pawn trackedPawn, Thing foodSource)
		{
			Pawn_VarietyTracker value = new Pawn_VarietyTracker();
			bool flag = VarietyRecord.varietyRecord.ContainsKey(trackedPawn);
			if (flag)
			{
				value = VarietyRecord.GetVarietyRecord(trackedPawn);
			}
			else
			{
				VarietyRecord.varietyRecord.Add(trackedPawn, value);
			}
			Pawn_VarietyTracker.TrackRecentlyConsumed(ref value, trackedPawn, foodSource);
			VarietyRecord.varietyRecord[trackedPawn] = value;
		}

		// Token: 0x06000043 RID: 67 RVA: 0x0000493C File Offset: 0x00002B3C
		public static void RemoveTrackedPawn(Pawn trackedPawn)
		{
			bool flag = VarietyRecord.varietyRecord.ContainsKey(trackedPawn);
			if (flag)
			{
				VarietyRecord.varietyRecord.Remove(trackedPawn);
			}
		}

		// Token: 0x06000044 RID: 68 RVA: 0x00004968 File Offset: 0x00002B68
		public override void FinalizeInit()
		{
			bool flag = VarietyRecord.varietyRecord != null;
			if (flag)
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
			Scribe_Collections.Look<Pawn, Pawn_VarietyTracker>(ref VarietyRecord.varietyRecord, "VarietyRecord", (LookMode)3, (LookMode)2, ref VarietyRecord.trackedPawns, ref VarietyRecord.pawnRecords, true);
		}

		// Token: 0x04000030 RID: 48
		public static List<Pawn_VarietyTracker> pawnRecords = new List<Pawn_VarietyTracker>();

		// Token: 0x04000031 RID: 49
		private static List<Pawn> trackedPawns = new List<Pawn>();

		// Token: 0x04000032 RID: 50
		private static Dictionary<Pawn, Pawn_VarietyTracker> varietyRecord;
	}
}
