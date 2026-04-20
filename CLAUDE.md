# VarietyMatters — project invariants

Short reference for recurring conventions. Read before touching cooking / variety code.

## 1. Scratch-field ethos

Per-call allocation is a perf regression. New scratch goes in the catalog at the top of the owning class:

```csharp
private static readonly Dictionary<T, U> _nameScratch = new Dictionary<T, U>();
```

`.Clear()` at the entry of the consumer method (or at the top of `BestVariety_AllowMix` when shared across a bill). Name `_fooScratch` so greps find all of them. Document shared scratch in the catalog comment — if multiple methods touch one field, list them.

Example catalog: `Source/VarietyCooking.cs:15-27`.

## 2. Commit style

Bracketed ticket IDs, matching recent history:

- `fix: [F-###] - short description` — bug fixes
- `feat: [US-###] - short description` — feature work (user stories)
- `chore: ...` / `docs: ...` — no ticket

Check `git log --oneline | grep -E "\[F-"` before picking a new F-### — IDs can be reused across branches but not across master. Latest on master: F-034 (`6fa0adb`).

## 3. Soft-cap pattern

When a setting acts as a *target* over variable structural input, use `max(setting, structural_minimum)` not raw `setting`. E.g. `effectiveBudget = max(numIngredients, variableSlotCount)` so modded 3+-slot recipes never starve a slot.

Pure `setting` creates footguns with modded inputs. Add a comment explaining the soft-cap direction.

## 4. `BestVariety_AllowMix` gate chain

The prefix at `Source/VarietyCooking.cs:53` has four sequential gates before the distribution loop. Any change to cooking behavior must respect all four:

1. `foodTrackingType == ByMealNames || !preferVariety` → vanilla path
2. Recipe gate: non-cooking skill OR no `ingestible` OR `specialProducts != null` → vanilla path
3. `cookingRecord == null` → vanilla path
4. Reaches distribution loop → E' adaptive water-fill runs

If your change should also skip under gate N, mirror the gate; don't invent a new one.

## 5. `TryFillIngredientSlotWithCap` pass semantics

`Source/VarietyCooking.cs:TryFillIngredientSlotWithCap` runs up to three passes per slot:

- **Pass 1** (top-K cap): collects first `slotMaxDefs` distinct defs in sort order, jitter-normalizes per-def caps summing to `slotBaseCount`, fills from stacks. Enforces `≤ slotMaxDefs` distinct defs.
- **Pass 2a** (top-K residual): drains residual nutrition restricted to Pass-1's top-K set. Preserves the cap.
- **Pass 2b** (unrestricted): relaxes cap only when top-K stacks can't satisfy the slot. Grabs defs outside top-K → may overshoot `slotMaxDefs` by ≤1 per slot. Safety net for stack-depth / cross-slot contention; near-dead under E' but not removable without a quantity-aware pre-scan.

When adding a new cap concern, thread it through Pass 1 + Pass 2a identically; leave Pass 2b as the escape hatch.

## Deploy / test

- Build: `dotnet build Source/VarietyMatters.csproj -c Debug`
- Deploy to Steam: invoke the `rimworld-tools:deploy` skill
- In-game dev verify: `Prefs.DevMode` on, inspect `CompIngredients.ingredients.Count` on produced meal (via dev inspector). Do NOT add permanent `Log.Message` for feature validation — remove after confirming.

## 6. `DietTracker._version` — transient cache-invalidation counter

`Source/New/DietTracker.cs` has a `private int _version` field (not `Scribe_`-persisted) bumped at exactly three public entry points:

- `AddFoodSource:134` (top, post null-guard)
- `UpdateMaxFoodInMemory:129` (end)
- `ReCount:204` (end)

All private mutators (`AddForgottenFoods`, `AddFoodSourceToMemory`, `AddFoodSourceToCount`, `RemoveFoodFromQueueAndCountBasedOnKey`, `RemoveOldestFoodSourceFromMemory`, `RemoveFoodFromCount`) are reached only through those three entry points — no redundant bumps needed. Consumers (e.g. `Need_FoodVariety` UI cache, `DietTracker.GetOverlapBreakdown` cache) compare against stored version + rebuild on mismatch. Sentinel `_lastVersion = -1` forces first-access rebuild regardless of tracker state. Load resets `_version` to 0; `ReCount` bumps it, cache rebuilds once.

Top-level driver `VarietyRecord.ReCountAll()` fans out to every tracker's `ReCount()`, then calls `VarietyAdjuster.AdjustVarietyLevel(tracker)` per tracker — used by settings mode-switch + post-load migration (see F-001 / §11 site 1). The fan-out relies on `ReCount`'s bump; no direct `_version++` in `ReCountAll`.

If you add a new DietTracker mutation entry point, bump `_version` at the end. If you add a private helper, it's covered by its caller.

## 7. Read-only tracker lookup — `TryGetVarietyRecord` vs `GetVarietyRecord`

`Source/VarietyRecord.cs`:

- `GetVarietyRecord(Pawn)` — **creates** a `DietTracker` on miss. Use only on ingest/update paths.
- `TryGetVarietyRecord(Pawn, out DietTracker)` — **non-creating**. Returns `false` + null on miss. Use on read-only scoring paths (e.g. `Patch_FoodUtility.FoodOptimality`).

Allocating a tracker just to read a score is a perf regression. If your code only reads from the tracker, use `TryGetVarietyRecord`.

## 8. Save format stability

Preserve `Scribe_` layout on any cache/perf change:

- `VarietyCookingRecord.RecentListEx.entries` is serialized as `List<string>` with `LookMode.Value`. **Do not** swap storage to `ThingDef`. If you need ref-based keying, build a `Dictionary<string, ThingDef>` at use-site (see `SortIngredients` label→def map).
- `EatenFoodSource.varietyScore` (US-023) — `Scribe_Values.Look(..., "varietyScore", 1f)`. Default `1f` is deliberate: old saves loaded into `ByIngredientOverlap` mode get full-credit scores until `ReCountAll` rebuilds from stored `IngredientsDefs` / `AteAtTick`. Do not change the default — it is the migration contract.
- `DietTracker._version`, `DietTracker.totalVarietyScore`, `DietTracker._breakdownCacheMeal`, `DietTracker._breakdownCacheVersion`, `DietTracker._breakdownOverlapScratch`, `DietTracker._breakdownFreshScratch`, `DietTracker._recentIngSetScratch`, `DietTracker._mealIndexScratch`, `Window_VarietyDetails._mealsAgoScratch`, `Need_FoodVariety._lastTipVersion`, `Need_FoodVariety._lastIconsVersion`, `Need_FoodVariety._lastIconsMemoryMult`, `Need_FoodVariety._iconOverlayHash`, `Need_FoodVariety._overlayWidth`, `Need_FoodVariety._foodDefsScratch`, `Need_FoodVariety._drawHoverOverlayAction`, `_raceKeysVersion` — transient, never Scribed.
- `_countByKey` in `DietTracker` — transient; rebuilt from scratch in `ReCount:204`. `totalVarietyScore` rebuilt in same spot from each source's stored `varietyScore`.

## 9. Pawn-lifecycle cleanup hooks

`Source/Patch_PawnLifecycle.cs` has two Harmony postfixes, both unconditionally call `VarietyRecord.RemoveTrackedPawn(__instance)`:

- **Primary**: `Pawn.Kill(DamageInfo?, Hediff)` — fires synchronously on death, same-tick dict cleanup.
- **Safety net**: `Pawn.Destroy(DestroyMode)` — catches pawns that vanish without going through Kill (trader sales, caravan loss, animal despawn, etc.).

`RemoveTrackedPawn` is idempotent, so double-fire during normal death is harmless.

Do NOT hook `Pawn.DeSpawn` for cleanup — caravan transitions fire DeSpawn but the tracker should persist (pawn returns).

## 10. DefOf pattern

`Source/DefOf_VarietyMatters.cs` uses `[DefOf]` class attribute + `DefOfHelper.EnsureInitializedInCtor`. Fields resolve before any game tick. Consumers should add a null-guard (treat `null` as "trait/precept disabled") as defense-in-depth against malformed load order.

Prefer DefOf over `TraitDef.Named("X")` / `DefDatabase<T>.GetNamed(...)` string lookups — the latter are slow and break on load-order drift.

## 11. `raceVariety` keying + settings cache invalidation

`ModSettings_VarietyMatters.raceVariety` is `Dictionary<string, RaceSetting>` keyed by `"modName-defName"` (see `GetRaceKey` at `ModSettings_VarietyMatters.cs:44`). NOT ThingDef-keyed. Any cache of enabled races must use `HashSet<string>` with the same key form.

`Need_FoodVariety._enabledRaceKeys` + `_raceKeysVersion` invalidation sites (US-032 removed the per-race UI, so the two interactive sites are gone — only load-time + mod-driven regeneration remain):

1. `ModSettings_VarietyMatters.ExposeData` post-load write
2. `ModSettings_VarietyMatters.GenerateRaces` end (covers `AddCompVariety.cs` humanlike-race discovery callers)

Do NOT double-bump inside `GenerateRaces` callers — those are already covered by (2). See `F-003` history. The old Race-checkbox + Reset-Races-button invalidation sites were removed in US-032 along with the right-side race panel.

## 12. `Disabled` check — optimize, don't cache the bool

`Need_FoodVariety.Disabled` is hot (read from `Patch_FoodUtility.FoodOptimality` per scoring candidate). The final bool is NOT cached — caching risks stale leaks into food scoring on trait/faction/settings/gene change. Instead: reorder cheap early-exits first, cache the one expensive sub-result (`_enabledRaceKeys` HashSet, see §11). Preserve null-safe access chain (`pawn.story?.traits?`, `pawn.genes?`).

US-033 added a `DisabledReason.GeneDisabled` branch that checks `pawn.genes?.HasActiveGene(DefOf_VarietyMatters.VM_NoVarietyNeed)` guarded by `ModsConfig.BiotechActive`. Genes are mutable at runtime (gene extraction, xenogerm implant, germline edit) so the gene result must NOT be cached either — the ordering-first principle still applies (gene check runs AFTER trait check, AFTER faction/settings checks, since it's the narrowest edge case).

## Do NOT

- Add per-call `new T[]` / `new List<T>()` in cooking hot path (US-004 no-alloc ethos).
- Add permanent `Prefs.DevMode`-gated logs that fire per-meal or per-pawn-death (too noisy; see `9ad6656`).
- Touch `Patch_IngredientStacking.cs numIngredients` when fixing cooking-cap issues — that's the stack-cap, different concern.
- Cache `Need_FoodVariety.Disabled` as a bool (see §12).
- Swap `RecentListEx.entries` storage type (see §8).
- Hook `Pawn.DeSpawn` for tracker cleanup (see §9).
