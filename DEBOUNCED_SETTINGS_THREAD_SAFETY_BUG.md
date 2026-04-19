# DebouncedSettingsHandler Runs Verse Mutations on Background Thread

> **Status: SHIPPED.** Fixed in `5b8f1af` (F-014) on branch `ralph/week-2-vfve-integration`. Wraps `PerformVarietyRecalculation` in `LongEventHandler.ExecuteWhenFinished` (option A from the design plan below). This doc is retained as design-history reference.
>
> Originally surfaced by Codex during Week 1 VFVE Integration final-diff review (2026-04-19). Pre-existing on master at that time — **not** a regression introduced by `a0c6d0e` (US-015).

## Report

`DebouncedSettingsHandler.ExecuteRecalculation` runs on a `System.Threading.Timer` callback thread (thread-pool), not RimWorld's main (Unity) thread. The callback invokes `DietTracker.ReCount()`, `VarietyAdjuster.AdjustVarietyLevel(dietTracker)`, and (as of US-015) `DietTracker.UpdateMaxFoodInMemory()` on every live pawn's tracker. All three mutate Verse/Pawn state that is not thread-safe.

No user crash report yet — the hazard is theoretical but real. Two factors have kept it latent:

1. Settings changes are rare relative to other Verse mutations, so the race window is small.
2. `Verse.Log` is thread-safe, so exception traces from this path surface cleanly if the race ever fires.

Codex's verbatim finding:

> The new floor refresh path still runs on a `System.Threading.Timer`, so changing the setting now drives `DietTracker.UpdateMaxFoodInMemory()`, `ReCount()`, and `VarietyAdjuster.AdjustVarietyLevel()` off the main thread. That is unsafe against Verse/Pawn state and is the one merge blocker here.
> — codex-final-review.txt:14 (2026-04-19 session)

## Not a regression from Week 1

Verified against master pre-merge. The offending threading pattern exists in `Source/New/Performance/DebouncedSettingsHandler.cs` at master HEAD `914bcdc`:

```csharp
debounceTimer = new Timer(ExecuteRecalculation, null, DebounceDelayMs, Timeout.Infinite);
// ...
private static void ExecuteRecalculation(object state) {
    // runs on thread-pool thread, not main thread
    PerformVarietyRecalculation();
}

private static void PerformVarietyRecalculation() {
    foreach (DietTracker dietTracker in VarietyRecord.PawnRecords) {
        if (dietTracker?.Pawn != null && !dietTracker.Pawn.Dead) {
            dietTracker.ReCount();
            VarietyAdjuster.AdjustVarietyLevel(dietTracker);
        }
    }
}
```

US-015 (`a0c6d0e`) added one more unsafe call (`UpdateMaxFoodInMemory()`) to the same pattern. The root hazard predates Week 1.

## Hazard surface

Any code path inside `ReCount()`, `AdjustVarietyLevel()`, or `UpdateMaxFoodInMemory()` that touches Verse-managed state can race with main-thread code. Specific concerns:

- `DietTracker.ReCount` (`Source/New/DietTracker.cs:136`) bumps `_version` and mutates `_countByKey`, `foodsVarietyInfos`, `KeysOfFoodSourcesWithVariety`. Concurrent read/write by `Need_FoodVariety.GetTipString` or `Patch_FoodUtility.FoodOptimality` can observe torn state.
- `DietTracker.UpdateMaxFoodInMemory` (`Source/New/DietTracker.cs:89`) reads `Pawn.story`, `traits`, `ageTracker` via `VarietyExpectation.GetVarietyExpectation`. `Pawn.story` is not documented thread-safe.
- `VarietyAdjuster.AdjustVarietyLevel` reads/writes `Need_FoodVariety.CurLevel`. Vanilla need-tick runs on main thread; concurrent write races with need bar UI rendering.
- `foreach (DietTracker dietTracker in VarietyRecord.PawnRecords)` iterates the dict while the main thread may add/remove entries via `VarietyRecord.GetVarietyRecord` or `RemoveTrackedPawn` — `InvalidOperationException` risk.

## Reproduction (theoretical)

1. Start a colony with 10+ tracked pawns.
2. Rapidly toggle a setting that calls `DebouncedSettingsHandler.QueueRecalculation()` (any expectation-tier slider, or `lowExpectationFloor`).
3. On the same tick, trigger a food ingestion on any pawn (`Need_FoodVariety.NeedInterval` → `Patch_FoodUtility.FoodOptimality`).
4. Timer fires `ExecuteRecalculation` on background thread while main thread runs food scoring. Race window = duration of `PerformVarietyRecalculation`.
5. Potential outcomes: `InvalidOperationException` from concurrent dict mutation; torn `CurLevel` reads; stale `_countByKey` state.

No in-the-wild crash has been reported with this stack. Fix is defensive.

## Design options

### (A) `LongEventHandler.ExecuteWhenFinished` in the Timer callback (Recommended)
Replace the direct call with a deferral:

```csharp
private static void ExecuteRecalculation(object state) {
    lock (lockObj) {
        if (!recalculationPending) return;
        recalculationPending = false;
        LongEventHandler.ExecuteWhenFinished(PerformVarietyRecalculation);
        debounceTimer?.Dispose();
        debounceTimer = null;
    }
}
```

`LongEventHandler.ExecuteWhenFinished` queues a callback onto the main thread's end-of-frame handler. No mod-side thread coordination needed; Verse's standard pattern for deferring work to the main thread.

- Pro: one-line change, idiomatic RimWorld, preserves the debounce behavior.
- Con: adds ~1 frame of latency to recalculation. Negligible for settings changes (user isn't watching for < 16 ms precision).

### (B) `Scribe.mode`-style main-thread gate
Check `UnityData.mainThread` inside `PerformVarietyRecalculation`; if off-thread, re-queue via `LongEventHandler`. More defensive, more code. Overkill for a single call site.

### (C) Drop the Timer entirely
Replace debouncing with tick-based delay (`Find.TickManager` + a tick counter). Fully main-thread. Larger change — removes the `System.Threading.Timer` pattern entirely. Worth considering only if this bug recurs from another direction.

## Recommended plan (F-014) — shipped

Shipped option (A) as a single-commit fix under ticket **F-014** (`5b8f1af`). Commit message: `fix: [F-014] - defer DebouncedSettingsHandler recalc to main thread via LongEventHandler`.

- **Files changed**: `Source/New/Performance/DebouncedSettingsHandler.cs` — `ExecuteRecalculation` now wraps `PerformVarietyRecalculation` in `LongEventHandler.ExecuteWhenFinished`. `ChangeLog.txt` entry added.
- **Files read only**: `Source/New/DietTracker.cs` (verified `_version` bump sites unchanged), `Source/VarietyRecord.cs` (verified `PawnRecords` dict iteration safe once deferred).
- **Verification performed**: `dotnet build Source/VarietyMatters.csproj -c Debug` → 0 warnings. Runtime smoke (manual QA, non-gating per week-2 job notes): rapid slider toggle on 10+ pawn save → no `InvalidOperationException` or Verse-state exception; need bars update within ~1s of drag release.
- **Save format impact**: none.
- **CLAUDE.md invariants**: §1 (scratch-field) N/A, §6 (`_version` bumps) unchanged — same `ReCount()` call, now just deferred. §12 (`Disabled` not cached) unaffected.

### Implementation note — deviation from option-A sketch

The shipped code places `LongEventHandler.ExecuteWhenFinished(PerformVarietyRecalculation)` **outside** the `lock (lockObj)` scope, not inside as the option-A sketch above shows. The lock now only guards the `recalculationPending` flag flip and the `debounceTimer` disposal; the main-thread deferral is queued after the lock releases.

This was a minor correctness deviation: holding `lockObj` across the `ExecuteWhenFinished` call is unnecessary (the queued delegate runs on a separate frame under different synchronization) and risked subtle lock-scope expansion if `LongEventHandler` internals ever took their own locks. The exception `try/catch` also migrated from `ExecuteRecalculation` down into `PerformVarietyRecalculation` itself, so errors on the main-thread execution surface via `Log.Error` exactly where they occur.

Net behavior matches option A: Timer fires off-thread → flag flip + dispose under lock → `LongEventHandler` queues the recalc → Verse runs it on the main thread at end-of-frame. The 500 ms debounce semantics are preserved; `ExecuteWhenFinished` only defers the callback, it does not re-queue or extend the debounce timer.

## Acceptance criteria (met)

- ✅ `dotnet build Source/VarietyMatters.csproj -c Debug` → 0 warnings, 0 errors.
- ✅ `PerformVarietyRecalculation` no longer runs on the threadpool Timer callback — `LongEventHandler.ExecuteWhenFinished` routes it to the main thread at end-of-frame.
- ✅ 500 ms debounce semantics preserved — the Timer still coalesces rapid `QueueRecalculation` calls; only the callback landing is deferred.
- ✅ `DietTracker.ReCount`, `DietTracker.UpdateMaxFoodInMemory`, and `VarietyAdjuster.AdjustVarietyLevel` all invoked on the main thread, removing the race against `Need_FoodVariety.GetTipString` / `Patch_FoodUtility.FoodOptimality` readers.
- ✅ `foreach (DietTracker dietTracker in VarietyRecord.PawnRecords)` iteration is now main-thread exclusive — no `InvalidOperationException` risk from concurrent dict mutation.
- ✅ Lock scope narrowed to just the flag flip + timer disposal — `LongEventHandler.ExecuteWhenFinished` queued outside the lock (minor deviation from option-A sketch; see Implementation note).
- ✅ Exception `try/catch` migrated into `PerformVarietyRecalculation` so main-thread errors surface via `Log.Error` where they occur.
- ✅ Committed as `5b8f1af` — `fix: [F-014] - defer DebouncedSettingsHandler recalc to main thread via LongEventHandler`. `ChangeLog.txt` updated.

## Do NOT touch in this fix

- `QueueRecalculation` — its Timer use is fine; only the callback landing needs deferral.
- `ForceImmediateRecalculation` — explicitly documents "for critical setting changes" and accepts main-thread context from its callers. If any caller invokes it off-thread, that's a separate audit.
- `Cleanup` — currently unused (noted in Week 1 review). Out of scope.

## Linked

- Surfaced in: `D:/MyRimworldMods/VarietyMatters/.ralph/jobs/week-1-vfve-integration/codex-final-review.txt`
- Related Week 1 story: `a0c6d0e feat: [US-015]` added `UpdateMaxFoodInMemory()` to the affected callback — did not introduce the threading hazard but did widen its surface.
