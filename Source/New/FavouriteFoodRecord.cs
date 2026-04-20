namespace VarietyMatters.New
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LudeonTK;
    using RimWorld;
    using Verse;

    public enum Bucket { None, Meat, Veg, Wild }

    public class CandidateProgress : IExposable
    {
        public int count;
        public int lastCreditTick = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.count, "count", 0);
            Scribe_Values.Look(ref this.lastCreditTick, "lastCreditTick", -1);
        }
    }

    public class SlotState : IExposable
    {
        public ThingDef favourite;
        public int saturation;
        public int lastTriggerTick = -1;
        public int lastDecayTick = -1;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref this.favourite, "favourite");
            Scribe_Values.Look(ref this.saturation, "saturation", 0);
            Scribe_Values.Look(ref this.lastTriggerTick, "lastTriggerTick", -1);
            Scribe_Values.Look(ref this.lastDecayTick, "lastDecayTick", -1);
        }
    }

    public class PawnFavData : IExposable
    {
        public Dictionary<string, CandidateProgress> meatProgress = new Dictionary<string, CandidateProgress>();
        public Dictionary<string, CandidateProgress> vegProgress = new Dictionary<string, CandidateProgress>();
        public Dictionary<string, CandidateProgress> wildProgress = new Dictionary<string, CandidateProgress>();
        public SlotState meatSlot = new SlotState();
        public SlotState vegSlot = new SlotState();
        public SlotState wildSlot = new SlotState();
        public int totalEligibleMealsEaten;
        public bool lastMealWasIngestedAsFavourite;

        // Scribe-scratch lists for the three progress dicts.
        private List<string> _meatKeys;
        private List<CandidateProgress> _meatValues;
        private List<string> _vegKeys;
        private List<CandidateProgress> _vegValues;
        private List<string> _wildKeys;
        private List<CandidateProgress> _wildValues;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref this.meatProgress, "meatProgress", LookMode.Value, LookMode.Deep,
                ref this._meatKeys, ref this._meatValues);
            Scribe_Collections.Look(ref this.vegProgress, "vegProgress", LookMode.Value, LookMode.Deep,
                ref this._vegKeys, ref this._vegValues);
            Scribe_Collections.Look(ref this.wildProgress, "wildProgress", LookMode.Value, LookMode.Deep,
                ref this._wildKeys, ref this._wildValues);
            Scribe_Deep.Look(ref this.meatSlot, "meatSlot");
            Scribe_Deep.Look(ref this.vegSlot, "vegSlot");
            Scribe_Deep.Look(ref this.wildSlot, "wildSlot");
            Scribe_Values.Look(ref this.totalEligibleMealsEaten, "totalEligibleMealsEaten", 0);
            Scribe_Values.Look(ref this.lastMealWasIngestedAsFavourite, "lastMealWasIngestedAsFavourite", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.meatProgress == null) this.meatProgress = new Dictionary<string, CandidateProgress>();
                if (this.vegProgress == null) this.vegProgress = new Dictionary<string, CandidateProgress>();
                if (this.wildProgress == null) this.wildProgress = new Dictionary<string, CandidateProgress>();
                if (this.meatSlot == null) this.meatSlot = new SlotState();
                if (this.vegSlot == null) this.vegSlot = new SlotState();
                if (this.wildSlot == null) this.wildSlot = new SlotState();
            }
        }
    }

    // GameComponent that tracks per-pawn favourite food discovery (meat / veg / gourmand-wild
    // slots), gated by eligibility floor + freshness + day-spread to prevent monoculture abuse.
    // Mood / UI / letter / message consumers live in US-035 (Need_FoodVariety, Window_VarietyDetails,
    // OnMealIngested mood branch). Pawn-lifecycle cleanup is mirrored in Patch_PawnLifecycle.
    public class FavouriteFoodRecord : GameComponent
    {
        public const int DiscoveryFloor = 15;
        public const float FreshnessMultiplier = 0.80f;
        public const int DaySpreadTicks = 120000;
        public const int SaturationCap = 4;
        private const int DecayTickInterval = 2500;

        private Dictionary<Pawn, PawnFavData> _data = new Dictionary<Pawn, PawnFavData>();
        private List<Pawn> _dataKeys;
        private List<PawnFavData> _dataValues;

        // Scratch catalog (CLAUDE.md §1). Never held across frames.
        //   _deadPawnScratch  : FinalizeInit sweep buffer.
        //   _creditedDefNamesScratch : de-dup set for OnMealIngested ingredient walk.
        //   _debugSb          : DebugAction_LogFavData string builder.
        private static readonly List<Pawn> _deadPawnScratch = new List<Pawn>();
        private static readonly HashSet<string> _creditedDefNamesScratch = new HashSet<string>();
        private static readonly StringBuilder _debugSb = new StringBuilder();

        public FavouriteFoodRecord(Game game) { }

        public static Bucket ClassifyIngredient(ThingDef def)
        {
            if (def == null) return Bucket.None;
            List<ThingCategoryDef> cats = def.thingCategories;
            if (cats == null) return Bucket.None;
            for (int i = 0; i < cats.Count; i++)
            {
                ThingCategoryDef cat = cats[i];
                if (cat == ThingCategoryDefOf.MeatRaw) return Bucket.Meat;
                if (cat == ThingCategoryDefOf.PlantFoodRaw) return Bucket.Veg;
            }
            return Bucket.None;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (this._data == null)
            {
                this._data = new Dictionary<Pawn, PawnFavData>();
                return;
            }
            _deadPawnScratch.Clear();
            foreach (Pawn p in this._data.Keys)
            {
                if (p == null || p.Dead || p.Destroyed)
                {
                    _deadPawnScratch.Add(p);
                }
            }
            for (int i = 0; i < _deadPawnScratch.Count; i++)
            {
                this._data.Remove(_deadPawnScratch[i]);
            }
            _deadPawnScratch.Clear();
        }

        public static void RemoveTrackedPawn(Pawn pawn)
        {
            if (pawn == null) return;
            FavouriteFoodRecord comp = Current.Game?.GetComponent<FavouriteFoodRecord>();
            if (comp == null) return;
            comp._data?.Remove(pawn);
        }

        public PawnFavData TryGet(Pawn pawn)
        {
            if (pawn == null || this._data == null) return null;
            this._data.TryGetValue(pawn, out PawnFavData data);
            return data;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref this._data, "data", LookMode.Reference, LookMode.Deep,
                ref this._dataKeys, ref this._dataValues);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && this._data == null)
            {
                this._data = new Dictionary<Pawn, PawnFavData>();
            }
        }

        public void OnMealIngested(Pawn pawn, EatenFoodSource eatenFoodSource, float varietyScore, float baseline)
        {
            if (pawn == null || eatenFoodSource == null) return;

            // Gate 1: need disabled (ascetic trait, no-variety gene, faction/race/slave/prisoner excluded).
            Need_FoodVariety need = pawn.needs?.TryGetNeed<Need_FoodVariety>();
            if (need == null) return;
            if (need.GetDisabledReason() != DisabledReason.None) return;

            // Gate 2: VarietyProvider.DoesFoodHasVarietyValueForPawn — raw/insect/humanlike/rotten
            // skipped so eating raw meat neither discovers a favourite nor saturates a slot.
            // One-alloc per meal: provider API takes EatenFoodByPawn. Ingest is not a hot path.
            EatenFoodByPawn eatenByPawn = new EatenFoodByPawn(pawn, eatenFoodSource);
            if (!VarietyProvider.DoesFoodHasVarietyValueForPawn(pawn, eatenByPawn, out _)) return;

            PawnFavData data = this.GetOrCreate(pawn);
            data.totalEligibleMealsEaten++;

            // Gate 3: discovery floor. Pawn must have eaten N eligible meals before progress accrues.
            if (data.totalEligibleMealsEaten < DiscoveryFloor) return;

            // Gate 4: freshness. Meal score must exceed 80% of the pawn's tier palate baseline,
            // so monoculture-low-score meals can't crystallize a favourite.
            if (varietyScore < FreshnessMultiplier * baseline) return;

            int nowTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            List<ThingDef> ingredients = eatenFoodSource.IngredientsDefs;

            if (ingredients == null || ingredients.Count == 0)
            {
                if (eatenFoodSource.ThingDef != null)
                {
                    this.CreditIngredient(pawn, data, eatenFoodSource.ThingDef, nowTick);
                }
                return;
            }

            _creditedDefNamesScratch.Clear();
            for (int i = 0; i < ingredients.Count; i++)
            {
                ThingDef ing = ingredients[i];
                if (ing == null) continue;
                if (!_creditedDefNamesScratch.Add(ing.defName)) continue;
                this.CreditIngredient(pawn, data, ing, nowTick);
            }
            _creditedDefNamesScratch.Clear();
        }

        private PawnFavData GetOrCreate(Pawn pawn)
        {
            if (!this._data.TryGetValue(pawn, out PawnFavData data))
            {
                data = new PawnFavData();
                this._data[pawn] = data;
            }
            return data;
        }

        private void CreditIngredient(Pawn pawn, PawnFavData data, ThingDef ing, int nowTick)
        {
            Bucket bucket = ClassifyIngredient(ing);
            Dictionary<string, CandidateProgress> progressMap;
            SlotState slot;

            switch (bucket)
            {
                case Bucket.Meat:
                    progressMap = data.meatProgress;
                    slot = data.meatSlot;
                    break;
                case Bucket.Veg:
                    progressMap = data.vegProgress;
                    slot = data.vegSlot;
                    break;
                default:
                    // Wild bucket only tracked for Gourmand pawns — modded meat without MeatRaw
                    // and eggs / milk / fungus fall here for Gourmand, skipped otherwise.
                    if (!IsGourmand(pawn)) return;
                    progressMap = data.wildProgress;
                    slot = data.wildSlot;
                    break;
            }

            if (slot.favourite == ing) return;

            if (!progressMap.TryGetValue(ing.defName, out CandidateProgress progress))
            {
                progress = new CandidateProgress { count = 0, lastCreditTick = -1 };
                progressMap[ing.defName] = progress;
            }

            // Day-spread gate: ≥ 120000 ticks (2 in-game days) since last credit for this ingredient.
            if (progress.lastCreditTick >= 0 && (nowTick - progress.lastCreditTick) < DaySpreadTicks)
            {
                return;
            }
            progress.count++;
            progress.lastCreditTick = nowTick;

            int threshold = GetThresholdForSaturation(slot.saturation, slot.favourite != null);
            if (progress.count >= threshold)
            {
                this.Crystallize(slot, progressMap, ing);
            }
        }

        // Saturation-to-rival-threshold table. First-ever discovery (no incumbent) uses base 3.
        // Rival-replacement thresholds: sat 0-1 → 3, sat 2-3 → 2, sat 4 → 1 (open-faced favourite).
        internal static int GetThresholdForSaturation(int saturation, bool hasExisting)
        {
            if (!hasExisting) return 3;
            if (saturation >= 4) return 1;
            if (saturation >= 2) return 2;
            return 3;
        }

        private void Crystallize(SlotState slot, Dictionary<string, CandidateProgress> progressMap, ThingDef newFav)
        {
            slot.favourite = newFav;
            slot.saturation = 0;
            slot.lastTriggerTick = -1;
            slot.lastDecayTick = -1;
            progressMap.Clear();
        }

        private static bool IsGourmand(Pawn pawn)
        {
            return pawn?.story?.traits?.HasTrait(DefOf_VarietyMatters.Gourmand) == true;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            int tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (tick % DecayTickInterval != 0) return;
            if (this._data == null || this._data.Count == 0) return;

            foreach (KeyValuePair<Pawn, PawnFavData> kv in this._data)
            {
                PawnFavData data = kv.Value;
                if (data == null) continue;
                DecaySlot(data.meatSlot, tick);
                DecaySlot(data.vegSlot, tick);
                DecaySlot(data.wildSlot, tick);
            }
        }

        // -1 saturation per slot per 2 in-game days without a matching-favourite trigger. Skips
        // when saturation is already 0 or no favourite yet. lastDecayTick debounces to once per
        // 2-day window so the decay doesn't re-fire every hourly tick inside the window.
        private static void DecaySlot(SlotState slot, int tick)
        {
            if (slot == null || slot.favourite == null) return;
            if (slot.saturation <= 0) return;
            if (slot.lastTriggerTick >= 0 && (tick - slot.lastTriggerTick) < DaySpreadTicks) return;
            if (slot.lastDecayTick >= 0 && (tick - slot.lastDecayTick) < DaySpreadTicks) return;
            slot.saturation = Math.Max(0, slot.saturation - 1);
            slot.lastDecayTick = tick;
        }

        [DebugAction("Mod: VarietyMatters", "Log favourite food data",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAction_LogFavData(Pawn pawn)
        {
            if (pawn == null) return;
            FavouriteFoodRecord comp = Current.Game?.GetComponent<FavouriteFoodRecord>();
            if (comp == null)
            {
                Log.Message("[VarietyMatters] No FavouriteFoodRecord component.");
                return;
            }
            PawnFavData data = comp.TryGet(pawn);
            _debugSb.Clear();
            _debugSb.Append("[VarietyMatters] Favourite food data for ").Append(pawn.LabelShort).Append(":");
            if (data == null)
            {
                _debugSb.AppendLine().Append("  (no entry)");
                Log.Message(_debugSb.ToString());
                _debugSb.Clear();
                return;
            }
            _debugSb.AppendLine();
            _debugSb.Append("  totalEligibleMealsEaten: ").Append(data.totalEligibleMealsEaten).AppendLine();
            _debugSb.Append("  lastMealWasIngestedAsFavourite: ").Append(data.lastMealWasIngestedAsFavourite).AppendLine();
            AppendSlot(_debugSb, "meat", data.meatSlot);
            AppendSlot(_debugSb, "veg", data.vegSlot);
            AppendSlot(_debugSb, "wild", data.wildSlot);
            AppendProgress(_debugSb, "meatProgress", data.meatProgress);
            AppendProgress(_debugSb, "vegProgress", data.vegProgress);
            AppendProgress(_debugSb, "wildProgress", data.wildProgress);
            Log.Message(_debugSb.ToString());
            _debugSb.Clear();
        }

        private static void AppendSlot(StringBuilder sb, string label, SlotState slot)
        {
            sb.Append("  ").Append(label).Append(" slot: fav=")
              .Append(slot?.favourite?.defName ?? "none")
              .Append(" sat=").Append(slot?.saturation ?? 0)
              .Append(" trig=").Append(slot?.lastTriggerTick ?? -1)
              .Append(" decay=").Append(slot?.lastDecayTick ?? -1)
              .AppendLine();
        }

        private static void AppendProgress(StringBuilder sb, string label, Dictionary<string, CandidateProgress> map)
        {
            sb.Append("  ").Append(label).Append(" (").Append(map?.Count ?? 0).Append("):").AppendLine();
            if (map == null) return;
            foreach (KeyValuePair<string, CandidateProgress> kv in map)
            {
                sb.Append("    ").Append(kv.Key)
                  .Append(" count=").Append(kv.Value?.count ?? 0)
                  .Append(" lastTick=").Append(kv.Value?.lastCreditTick ?? -1)
                  .AppendLine();
            }
        }
    }
}
