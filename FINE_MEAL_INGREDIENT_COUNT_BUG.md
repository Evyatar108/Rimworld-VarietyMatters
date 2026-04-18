# Fine Meal Uses Fewer Ingredients Than Setting

## Report

User sets "num of ingredients" (`ModSettings_VarietyMatters.numIngredients`) to `3`. Observed behavior:
- **Simple meal** (`MakeMealSimple`): 3 distinct ingredients. Correct.
- **Fine meal** (`MakeMealFine`): **2** distinct ingredients. User expected 3.

Reported 2026-04-18 after deploying the VarietyMatters performance pass (branch `ralph/varietymatters-performance-pass`, master HEAD `eb90fae`).

## Not a regression from the perf pass

Verified via git history. The slot-budget distribution logic that causes this behavior was introduced in commit `6b4be5f` ("fix: refinements to cooking ingredient selection", 2026-04-17) — **before** the performance pass (`3c62519`..`eb90fae`). The perf pass (US-003..US-005) only restructured allocations and sort keying; it did not change the cap distribution math.

Pre-`6b4be5f` behavior (grandparent commit `4aa7f2a`): `TryFillIngredientSlotWithCap` had no `slotMaxDefs` parameter. Each slot independently capped at `numIngredients`. So fine meal (2 slots) × `numIngredients=3` yielded up to 6 distinct defs per meal.

`6b4be5f` changed it to distribute the total budget across slots.

## Current code path

`D:/MyRimworldMods/VarietyMatters/Source/VarietyCooking.cs:87-98`

```csharp
int totalSlots = bill.recipe.ingredients.Count;
int totalBudget = Mathf.Max(1, ModSettings_VarietyMatters.numIngredients);
int budgetRemainder = totalBudget % totalSlots;
int budgetBase = totalBudget / totalSlots;
for (int i = 0; i < totalSlots; i++)
{
    // ...
    int slotMaxDefs = Mathf.Max(1, budgetBase + (i < budgetRemainder ? 1 : 0));
    if (!VarietyCooking.TryFillIngredientSlotWithCap(
            availableThings, bill, ingredientCount, chosen, applyCapForSlot, slotMaxDefs)) { ... }
}
```

`TryFillIngredientSlotWithCap` at `VarietyCooking.cs:169-280` uses `slotMaxDefs` as a top-K cap on distinct defs selected per slot (line 191: `if (_distinctDefsScratch.Count >= maxDefs) break;`). Pass 2a preserves the cap; Pass 2b relaxes it only if top-K stacks can't satisfy the slot quantity.

## Math for fine meal × `numIngredients=3`

Fine meal = 2 ingredient slots (vanilla: one meat/animal-product, one plant):

| Slot | Formula | `slotMaxDefs` |
|------|---------|---------------|
| 0 | `max(1, 3/2 + (0 < 1 ? 1 : 0)) = max(1, 2)` | **2** |
| 1 | `max(1, 3/2 + (1 < 1 ? 1 : 0)) = max(1, 1)` | **1** |

Meal total = **up to 3** distinct defs. But the user sees **2**, which implies one slot filled with fewer distinct defs than its cap. Possible causes:
- Only 1 meat-type or 1 plant-type available in the cook's haul region (sort produced 1 distinct def after filtering)
- `ingredientCount.IsFixedIngredient == true` for a slot → `applyCapForSlot = false` → Pass 1 skipped → Pass 2 may grab a single def worth of nutrition without enforcing distinct-def mixing
- `distinctDefCount <= 1` short-circuit at line 197 → Pass 1 skipped → one def fills the slot

## Design question

What does "num of ingredients" mean?

- **(A)** Current design: *Maximum total distinct defs in the produced meal*. Clean cap. Fine meal underuses when one category is sparse.
- **(B)** "Per-slot cap": each slot independently capped at `numIngredients`. Pre-`6b4be5f` behavior. Fine meal could reach 6 defs with setting=3, which may overshoot user intent.
- **(C)** "Fill to N": ensure meal has *exactly* `numIngredients` distinct defs when availability permits. Would need a post-pass that relaxes slot caps when total distinct < `numIngredients`.
- **(D)** "Per-slot min": each slot gets at least `ceil(numIngredients / totalSlots)` defs. Fine meal = 2+2 = up to 4. Overshoots by 1 on setting=3.
- **(E')** **CHOSEN. Adaptive water-fill, soft cap.** Pre-scan distinct eligible defs per capped slot; distribute `effectiveBudget = max(numIngredients, variableSlotCount)` across slots giving each at least 1, water-fill remainder to slots with spare capacity. Random tie-break. Fixed and `specialProducts` slots excluded from budget. Matches user intent ("3 ingredients means up to 3 when available, never starve a slot").

### Why E'

Vanilla has NO distinct-def cap — `numIngredients` is 100% mod-defined. Vanilla meal recipes: simple=1 slot, fine=2, lavish=2, survival=2. Modded recipes may have 3+ slots; soft cap `max(N, slots)` keeps them from starving.

Reviewed by codex + subagents. Both converge on E'. Alternatives rejected:
- B re-introduces the 10+ fanout bug `6b4be5f` fixed.
- C's post-pass needs rollback of already-consumed stacks — effectively forces pre-scan anyway.
- D overshoots by design.
- H (per-slot for ≤2 slots else distribute) has ad-hoc threshold; soft-cap in E' handles modded recipes cleanly without branching.
- F (quantity-aware: count *realizable* defs given stack depth) would tighten E' but adds ~2× code and a threshold-selection hazard. Pass 2b's existing cap-relaxation already handles shallow-stack cases. Revisit only if logs show consistent undershoot.

## Reproduction

1. Load save with a cook and both a butcher table supply and a crop supply.
2. Set `numIngredients = 3` in mod settings.
3. Create a `MakeMealFine` bill with "Allow Mix" enabled, `specialProducts == null`.
4. Ensure map has ≥ 2 distinct meat types AND ≥ 2 distinct plant types in haul reach.
5. Produce a fine meal. Inspect `CompIngredients.ingredients.Count` on the produced meal.
6. Expected per current design: up to 3 distinct defs. Actually observed: 2.

If step 4's preconditions aren't met on user's map, finding is "availability limitation, not bug". Verify map inventory first.

## Implementation plan (E')

Target: `VarietyCooking.cs:87-98` (loop body) + new static scratch fields alongside the existing ones at `:18-23` + new `CountEligibleDefs` helper.

### Pre-conditions

- `BestVariety_AllowMix` already early-returns when `bill.recipe.specialProducts != null` via the `gate` branch at `:68-74`. **E' never sees a recipe with `recipeAllowsCap == false`**; the local `recipeAllowsCap` at `:86` is always `true` once we reach the distribution loop. Remove it from the E' algorithm — it's dead. Fixed slots (`IsFixedIngredient == true`) are the only non-capped case inside the loop.
- `numIngredients` is clamped to `[1, 10]` by the settings UI (`Mod_VarietyMatters.cs:174`). `Mathf.Max(1, ...)` is a belt-and-suspenders guard; no `=0` path. Document that `=0` is unreachable.

### Scratch fields (add to `VarietyCooking.cs:18-23`)

```csharp
// Per-call scratch for E' budget distribution. Cleared at top of BestVariety_AllowMix.
private static readonly List<int> _eligibleDistinctScratch = new List<int>();
private static readonly List<int> _assignedScratch         = new List<int>();
```

No `new int[]` per call — matches the US-004 no-alloc ethos. Reservoir sampling below uses local ints (`picked`, `candidateK`), no candidate list needed.

### Algorithm (two-phase: scan, then fill)

```csharp
int totalSlots = bill.recipe.ingredients.Count;
int numIngredients = Mathf.Max(1, ModSettings_VarietyMatters.numIngredients);

_eligibleDistinctScratch.Clear();
_assignedScratch.Clear();

// Phase A: pre-scan each slot. Must complete before any fill call — _distinctDefsScratch
// is shared with TryFillIngredientSlotWithCap and gets clobbered at :172 on entry there.
// CountEligibleDefs returns int (never a reference/enumerator) — no live enumerator may
// cross the phase boundary.
int variableSlotCount = 0;
for (int i = 0; i < totalSlots; i++) {
    IngredientCount ic = bill.recipe.ingredients[i];
    int eligible = 0;
    int assigned = 0;
    if (!ic.IsFixedIngredient) {
        variableSlotCount++;
        eligible = CountEligibleDefs(availableThings, ic, bill);
        assigned = eligible > 0 ? 1 : 0;
    }
    _eligibleDistinctScratch.Add(eligible);
    _assignedScratch.Add(assigned);
}

// Soft cap: never starve a variable slot.
int effectiveBudget = Mathf.Max(numIngredients, variableSlotCount);
int sumAssigned = 0;
for (int i = 0; i < _assignedScratch.Count; i++) sumAssigned += _assignedScratch[i];
int remaining = effectiveBudget - sumAssigned;

// Water-fill remaining budget with reservoir-style single-pass tie-break (no alloc).
// On each while-iteration: count candidates (assigned<eligible), pick one uniformly
// via Rand.Range(0, k)==0 replacement rule, increment assigned, decrement remaining.
while (remaining > 0) {
    int picked = -1;
    int candidateK = 0;
    for (int i = 0; i < _assignedScratch.Count; i++) {
        if (_assignedScratch[i] < _eligibleDistinctScratch[i]) {
            candidateK++;
            if (Rand.Range(0, candidateK) == 0) picked = i;
        }
    }
    if (picked < 0) break;                              // no more capacity
    _assignedScratch[picked] = _assignedScratch[picked] + 1;
    remaining--;
}

// Phase B: fill. slotMaxDefs is 0 for (a) fixed slots (applyCap=false ignores it) and
// (b) capped slots whose pre-scan found eligible==0. In case (b), TryFillIngredientSlotWithCap
// raises 0→1 at :183 (Mathf.Max) but finds nothing matching the filters → slot fails → bill fails.
// Matches vanilla behavior for unmakeable filtered bills.
for (int i = 0; i < totalSlots; i++) {
    IngredientCount ic = bill.recipe.ingredients[i];
    bool applyCapForSlot = !ic.IsFixedIngredient;
    int slotMaxDefs = _assignedScratch[i];  // 0 for fixed (ignored), >=1 for capped
    if (!VarietyCooking.TryFillIngredientSlotWithCap(
            availableThings, bill, ic, chosen, applyCapForSlot, slotMaxDefs)) {
        __result = false;
        return false;
    }
}
```

### Pre-scan helper

Must match fill predicate exactly (`VarietyCooking.cs:187-188`). Returns `int` only — never exposes `_distinctDefsScratch` reference:

```csharp
private static int CountEligibleDefs(List<Thing> availableThings, IngredientCount ic, Bill bill) {
    _distinctDefsScratch.Clear();
    for (int j = 0; j < availableThings.Count; j++) {
        Thing t = availableThings[j];
        if (!ic.filter.Allows(t)) continue;
        if (!ic.IsFixedIngredient && !bill.ingredientFilter.Allows(t)) continue;
        _distinctDefsScratch.Add(t.def);
    }
    return _distinctDefsScratch.Count;
}
```

**Scratch ordering invariant**: `_distinctDefsScratch` is shared mutable state with `TryFillIngredientSlotWithCap` (fill, `:172`). Safe only because Phase A completes before Phase B. Never interleave. `CountEligibleDefs` must return `int`, not a reference to the set — any leaked reference would observe Phase B overwriting the contents.

### Invariants

- `assigned[i] <= eligibleDistinct[i]` (water-fill clamp)
- `sum(assigned) <= effectiveBudget`
- `effectiveBudget = max(numIngredients, variableSlotCount)` (soft cap; may exceed `numIngredients` for modded recipes with ≥ `numIngredients` variable slots)
- Realized distinct defs in `chosen` ≤ `sum(assigned) + #fixedSlots + Pass2bFallbackDefs`
  - Pass 2b can still fire when stack depth or cross-slot `_takenByThingScratch` contention prevents top-K from filling the slot. It grabs defs outside top-K → overshoots `sum(assigned)` by ≤1 per slot. Not removable without making pre-scan quantity-aware (design F, rejected).
- **All-fixed recipes** (`variableSlotCount == 0`): E' is a no-op. Every slot passes `slotMaxDefs=0` with `applyCap=false`; `TryFillIngredientSlotWithCap` fills unconditionally, matching pre-`6b4be5f` fixed-slot behavior.
- **Empty recipe** (`totalSlots == 0`): Phase A/B both zero iterations, `__result=true`. Incidentally fixes a divide-by-zero hazard in the current `totalBudget % totalSlots` at `:90`.

### Rand determinism

E' adds new `Rand.Range` calls in water-fill. This changes RNG draw count and order vs current code. Pre-existing Pass-1 jitter at `:203` already consumed RNG non-deterministically across saves, so E' does not newly break determinism. Verification uses *distinct-def count* (deterministic), not specific def identity.

### Smoke-test observability

Add a `Prefs.DevMode`-gated `Log.Message` in the **prefix**, right before `__result = true; return false;` at `:104-105`. Must be in the prefix — `assigned[]` and `effectiveBudget` are method-local to the prefix and unavailable from the postfix.

```
[VarietyMatters E'] recipe=<defName> budget=<effectiveBudget> assigned=[<comma-list>] realizedDistinct=<chosen.Select(c=>c.Thing.def).Distinct().Count()>
```

Pattern mirrors existing `Prefs.DevMode` log at `Patch_PawnLifecycle.cs`. Keep permanently (cheap, off by default for players).

### Keep untouched

- `TryFillIngredientSlotWithCap` — takes `slotMaxDefs` as-is.
- Pass 2b cap-relaxation — still live. Fires when stack depth or cross-slot `_takenByThingScratch` contention prevents top-K from satisfying the slot. Removable only if pre-scan becomes quantity-aware (design F, rejected).
- `SortIngredients` — E' only counts, doesn't reorder.
- Settings hot-reload — `ModSettings_VarietyMatters.numIngredients` is read once per `BestVariety_AllowMix` call; mid-bill changes are not observable. No handling needed.

### Comments to update (stale after E')

- `VarietyCooking.cs:17` — scratch catalog lists "BestVariety_AllowMix or SortIngredients" as consumers; append `CountEligibleDefs` for `_distinctDefsScratch`.
- `VarietyCooking.cs:88` — replace with:
  > Soft cap: `effectiveBudget = max(numIngredients, variableSlotCount)`. Water-filled across variable slots, random tie-break. Fixed slots pass `slotMaxDefs=0` (ignored via `applyCap=false`). Modded recipes with more variable slots than `numIngredients` never starve — budget grows to match slot count.
- `VarietyCooking.cs:96` — delete the old `budgetBase + budgetRemainder` paragraph entirely. The allocation logic is now documented in the pre-scan/water-fill block above the loop.
- `VarietyCooking.cs:182` — replace with:
  > Collect distinct defs in sort order, capped at this slot's precomputed E' allowance (`slotMaxDefs`) from the Phase-A pre-scan + water-fill. Top-K by sort order preserves variety/distance/spoiling priority.

### Suggested investigation steps (before implementing)

1. **Reproduce with controlled inventory.** Dev-mode spawn 2+ meat types + 2+ plant types; trigger fine meal cook; observe `CompIngredients.ingredients.Count == 2` on produced meal.
2. **Implement E'.** Single commit targeting `VarietyCooking.cs:87-98` + new static scratch lists + `CountEligibleDefs` helper + the three stale comments.
3. **Verify with DevMode log.** Enable dev mode; produce fine meal; observe log line `budget=3 assigned=[2,1] realizedDistinct=3` (or `[1,2]`, random tie-break).
4. **Regression sweep**:
   - Simple meal (1 slot): `budget=3 assigned=[3]` → up to 3 defs.
   - Lavish meal (2 slots): same as fine.
   - Nutrient paste: log never emits (gated by `specialProducts != null` before E' runs).
   - Pemmican: same as paste (has `specialProducts`).
   - **`foodTrackingType == ByMealNames`**: E' never runs (early-return at `:59`). Cooking uses vanilla path. Verify no log emitted and meal cooks successfully.
   - **`preferVariety == false`**: same gate at `:59`. Verify no-op.
5. **Sparse-inventory degradation**: dev-spawn only 1 meat def + 1 plant def. Fine meal produces `realizedDistinct=2`. Acceptable per soft-cap semantics.
6. **Modded 3+-slot sanity (synthetic)**: if no installed mod ships a 3-slot cookable recipe, hand-craft a test recipe def in a scratch save (or dev-mode patch). Verify `effectiveBudget = max(N, 3) = 3` on `N=2` (soft cap engages) and each slot's `assigned ≥ 1`. If no modded recipe available to test against, note "deferred — no test vector" in the commit and rely on code inspection.

## Files to touch

- `D:/MyRimworldMods/VarietyMatters/Source/VarietyCooking.cs` — static scratch fields (`:18-23`), distribution loop (`:87-98`), `CountEligibleDefs` helper, stale comments at `:88`, `:96`, `:182`, DevMode log line in `UpdateVariety_AllowMix` or end of prefix.

## Acceptance criteria

- `dotnet build Source/VarietyMatters.csproj -c Debug` → 0 warnings, 0 errors.
- `git diff` scoped to `Source/VarietyCooking.cs` (+ this doc).
- DevMode log verifies `realizedDistinct == 3` on fine meal with `numIngredients=3` + ≥2 meat + ≥2 plant in haul reach.
- Sparse-inventory case (1 meat + 1 plant) produces `realizedDistinct=2` cleanly — no bill failure.
- No regression in simple meal, lavish meal, nutrient paste, pemmican.
- Zero per-call allocations added — new scratch uses `private static readonly List<int>` fields cleared at `BestVariety_AllowMix` entry.
- Commit style: `fix: [F-006] - fine-meal ingredient count honors numIngredients setting` (matching recent `F-001`..`F-005` pattern; `F-005` already used on `af11b73`).

## Do NOT touch in this fix

- Perf-pass changes (US-001..US-014) — orthogonal.
- `Patch_IngredientStacking.cs numIngredients` usage — that's the stack-cap, different concern.
- Serialization / save compatibility — none of the options above touch persisted state.
