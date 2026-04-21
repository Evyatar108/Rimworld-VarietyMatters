# Phase 1 — Tier-S Quick Wins

## Goal

Direct response to repeated user complaints across Redux / VFVE / Improved comment sections. Low effort, instant positive reviews.

## Stories

### US-039 — Ingredient / meal exclusion lists
**Effort**: 2 d
**Scope**:
- New settings section "Exclude from variety tracking"
- Two persisted sets: `excludedIngredients : HashSet<string>`, `excludedMeals : HashSet<string>` (both keyed by `defName`)
- Pre-populated defaults: `Kibble`, `MealNutrientPaste`, `Corpse_*`, `InsectJelly` (toggleable per def, not hardcoded)
- Gate sites:
  - `DietTracker.AddFoodSource` — skip add if meal def OR all ingredients excluded
  - `Patch_FoodUtility.FoodOptimality` — skip scoring hit for excluded
  - `FavouriteFoodRecord.OnMealIngested` — skip credit for excluded ingredients
  - `VarietyCooking.BestVariety_AllowMix` — de-prioritize excluded ingredients (cook won't rotate them through variety loop)
- UI: scrollable two-column list w/ checkbox per def, search box, "reset to defaults" button
- Save: `Scribe_Collections` both lists, `LookMode.Value`, preserve empty default on load

**Acceptance**:
- Kibble meal ingested → variety need untouched, favourites never fire, Details window row shows gray w/ "Excluded from variety" tooltip.
- Reset button restores curated defaults, preserves user's additions confirmation (or separate nuke).
- Modded ingredient addable via search; persists across save/load.
- Excluded ≠ hidden: pawn still eats the food, only the scoring hook is suppressed.

### US-040 — Dev-mode favourites editor
**Effort**: 1 d
**Scope**:
- Right-click pawn in dev mode → float-menu entry "VarietyMatters: Edit favourites"
- Modal dialog: 3 rows (Meat / Veg / Wild) each w/ dropdown listing eligible defs (pulled from §13 seed pools), "Clear slot" button per row, "Clear all" at bottom
- Writes directly to `PawnFavData.slots`; NO side effects (no mood fire, no crystallization event, no displacement letter)
- Gated by `Prefs.DevMode`

**Acceptance**:
- Set meat = `Cow_Meat`; next meal containing cow meat fires `VM_AteFavourite` mood (respects back-to-back + freshness gates).
- Dialog visible only w/ DevMode on.
- Clearing a slot sets `SlotState.current = null` + resets `lastTriggerTick` + `lastDecayTick` to current.
- Dialog opens per-pawn; no global state.

### US-041 — CharacterEditor / PrepareCarefully integration
**Effort**: 1 d (timeboxed; fallback to US-040 if APIs absent)
**Scope**:
- Detect `ModsConfig.IsActive("EdB.PrepareCarefully")` + `"VOID.charactereditor"`.
- Research: check each mod's extension API for `GameComponent` / pawn-data-tab hook.
- If API exists: register `PawnFavData` panel for display/edit during pawn gen + mid-save edit.
- If API absent: document limitation, rely on US-040.
- Public setters on `PawnFavData.slots` (already public-ish; verify).

**Acceptance**:
- CharacterEditor opens on saved pawn → favourites slots visible + editable.
- Changes persist across save/load.
- Scene where neither mod is active → no regression.

**Risk**: Scope-creep if APIs are undocumented. Hard-timebox 1 day; defer w/ explicit note otherwise.

## Risks

- Exclusion list UI could become overwhelming with large mod lists — require search box.
- Default exclusions might surprise users who DO want kibble tracked → add "reset to empty" alongside "reset to curated defaults".
- PrepareCarefully API volatility — mod has changed hands; fallback is the plan.

## Notes

- All three settings live in a new "Data" or "Advanced" fold-out (not a difficulty knob — exclude from `Profile` per §13 ethos).
- Exclusion lists are `defName`-keyed, not `ThingDef`-keyed (robust across save/load, survives mod removal gracefully).
- Dev-mode edits intentionally skip events — the feature is a debug/test tool, not a gameplay path.
