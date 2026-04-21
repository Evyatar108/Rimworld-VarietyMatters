# Opportunistic Wins — O-1 through O-6

Per local code audit (review round). Each O-# is a cheap win unblocked by existing code. No dedicated phase — attach to the nearest relevant release.

## O-1 — `FoodOptimality` secondary scoring axis
**Attach to**: Release 1.0 (before or during Phase 1)
**Effort**: 1–2 h
**Scope**:
- `Patch_FoodUtility.FoodOptimality` postfix at `Source\Patch_FoodUtility.cs:14` currently uses ~30% of its hook power.
- Add concerns in the SAME postfix (no new patch):
  - Ingredient freshness modifier (tighter window than vanilla spoilage bonus).
  - Ingredient rarity (inverse-frequency from `DietTracker`).
  - Chef-skill scaling (high-skill cook's meals gain scoring bonus).
- Cumulative score still clamps at a sensible ceiling — don't let stacking trivialize meal-pick.

**Acceptance**:
- Postfix unchanged patch site; new concerns behind setting toggles (all default ON).
- No new allocations in hot path (§1).
- Meal pick visibly prefers fresher + rarer + skill-made meals.

## O-2 — Dining-room-proximity in meal pick
**Attach to**: Release 1.0 (same patch as O-1)
**Effort**: 2 h
**Scope**:
- Fixes universal "pawn walks across map for lavish, eats on floor" complaint (competitor agent harvest).
- In same `Patch_FoodUtility.FoodOptimality` postfix: bias score toward meals inside dining rooms.
- Tuning: +5 optimality if meal is in a dining-room-tagged room.
- Overlaps semantically with Phase 4 US-054 (dining room pref). O-2 is the scoped early version; US-054 extends it.

**Acceptance**:
- Given two otherwise-equal meals, pawn picks the one in the dining room.
- Setting to disable: `diningRoomPreferenceEnabled` (default ON).
- Phase 4 US-054 builds on this foundation.

## O-3 — Bill hysteresis (cook thrashing fix)
**Attach to**: Release 1.1 (parallel w/ Phase 3)
**Effort**: 1 d
**Scope**:
- Universal Workshop complaint: cook fires back to stove on every 2-meal delta. Context-switch thrashing.
- Harmony patch on `Bill_Production.ShouldDoNow` (or `RecipeDef` count check site) to implement low-high thermostat: cook until `targetCount`, don't fire again until count drops below `lowThreshold` (e.g. 50% of target).
- Opt-in: new checkbox on Bill UI "Batch mode (low-high)". Default OFF to avoid surprising existing users.

**Acceptance**:
- Bill w/ "Do until 20 of X" + Batch mode → cook fires at count ≤ 10, produces until 20, then idles until count drops below 10 again.
- Vanilla behavior preserved when Batch mode off.
- No savegame impact; per-bill runtime flag.

## O-4 — Expose `DietTracker.GetFoodCategoryBreakdown()`
**Attach to**: Release 1.1 (bridge to Phase 5)
**Effort**: 2 h
**Scope**:
- Local audit: `DietTracker._tempList` + ingredient-frequency logic already computes category breakdown per call. Expose as a public method.
- Unblocks Phase 5 bucket intake without new computation.
- Also unblocks Phase 8 US-081 Menu Fatigue threshold check (uses per-bucket read).

**Acceptance**:
- `DietTracker.GetFoodCategoryBreakdown(Pawn) → Dictionary<Bucket, int>` (count of bucket-tagged intake last N days).
- Scratch-field catalogued (§1). No alloc per call.
- Unit-test-like verification: seeded tracker state returns expected counts.

## O-5 — Settings UI race-pane collapse
**Attach to**: Release 1.0 (Phase 1 co-requirement)
**Effort**: 3 h
**Scope**:
- Right pane in `Source\Mod_VarietyMatters.cs` dedicates 40% width to race enable/disable (~5–15 rows).
- Collapse to single "Races (N enabled)" row + foldout showing toggles on demand.
- Reclaims ~200 px vertical + 40% horizontal for new toggles (Phase 1 exclusion settings UI, Phase 5 diet-health toggles).
- Rect layout restructure ~15 lines.

**Acceptance**:
- Default view → race list collapsed.
- Expand → existing toggle UI renders as-is.
- No regression on race-enable/disable persistence.
- Left pane has breathing room for 5+ new controls.

## O-6 — Per-food `varietyScore` field framework extension
**Attach to**: Deferred / opportunistic
**Effort**: 3–4 h
**Scope**:
- `EatenFoodSource.varietyScore` Scribed default `1f` already exists (§8). Under-used.
- Extension: allow features to write per-food scores (e.g. "this meal was a signature → score 1.2").
- Not urgent — just catalog the extension point so Phase 3/5/9 features use it correctly.
- No ticket to execute; this is a reference note.

**Acceptance**:
- Document the extension-point ethos in CLAUDE.md next time the file is touched.
- Any future feature writing per-food scores uses the existing field, not a new one.

## Summary table

| ID | Attach to | Effort | Hot-path? | Save impact |
|---|---|---|---|---|
| O-1 | R1.0 | 1–2 h | yes (optimized) | none |
| O-2 | R1.0 | 2 h | yes (same patch as O-1) | none |
| O-3 | R1.1 | 1 d | no (bill logic) | none |
| O-4 | R1.1 | 2 h | cold (UI/API) | none |
| O-5 | R1.0 | 3 h | cold (settings UI) | none |
| O-6 | deferred | 3–4 h | n/a (catalog note) | none |

## Notes

- Total effort ~1.5 d across the six. Attaching to existing release milestones, not a standalone phase.
- O-5 should land early — unblocks downstream settings UI work without re-layouts per phase.
- O-1 + O-2 are the same patch site; implement together or not at all.
