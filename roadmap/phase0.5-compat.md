# Phase 0.5 — Compat Matrix

## Goal

Pre-Phase-5 gate. Validate `FavouriteFoodRecord.ClassifyIngredient` parent-chain walk (§13) against major modded-food mods. Publish per-mod override XML. Blocks Phase 5 (diet-health buckets would otherwise misclassify modded foods and frustrate players).

## Why a phase, not a chore

Phase 5's bucket classifier reuses the favourites classifier. A misclassification becomes a hediff misfire — worse than a mood misfire. Testing surface is wide: 5+ major food mods × dozens of ingredient defs. Treating compat as code-adjacent discipline (not a playtest afterthought) forces the matrix to ship as a doc + override XML rather than rotting in comments.

Also addresses codex review's "mod-compat test matrix" gap and arch review's "add Phase 0.5 compat" directive.

## Stories

### US-073 — Mod compat test matrix
**Effort**: 2 d
**Scope**:
- Install + load-test each on a clean colony:
  - Vanilla Cooking Expanded (VCE)
  - RimCuisine 2 (+ addons)
  - Medieval Overhaul
  - Gastronomy
  - Smarter Food Selection
- Per mod, enumerate all ingestible defs; walk `ThingCategoryDef.parent` chain for each; record the resolved bucket (Meat / Veg / Wild / None).
- Flag any def that classifies surprisingly (e.g. "insect jelly" landing in Wild vs None; "raw rice" landing in None vs Veg).
- Output: `roadmap/compat/classifier-matrix.csv` — one row per `(modName, defName, expectedBucket, actualBucket, status)`.

**Acceptance**:
- CSV committed.
- Every flagged misclassification has a fix (see US-074) or an explicit "acceptable" note.
- Load-order resilience: classifier does not throw on mod absence.

### US-074 — Per-mod bucket override XML
**Effort**: 1 d
**Scope**:
- Support override table in settings: `Dictionary<string, Bucket> bucketOverrides` keyed by defName.
- Ship curated default overrides for each mod in `Common/Defs/VarietyMattersOverrides_<ModName>.xml` (gated by `<li Class="PatchOperationConditional">` → mod-active check).
- Override precedence: explicit user setting > XML default > classifier walk.

**Acceptance**:
- Loading RimCuisine + VarietyMatters: RimCuisine "fruit jam" correctly lands in Veg bucket.
- Disabling RimCuisine mid-save: override entries become dormant, no errors.
- User can override any def via settings UI, value persists across load.

### US-075 — Gastronomy compat shim
**Effort**: 1 d
**Scope**:
- Gastronomy's waiter bypasses `FoodUtility.FoodOptimality` — our patches miss it.
- Option A: Harmony patch Gastronomy's equivalent method (brittle, updates coupling).
- Option B: On meal delivery by waiter, fire the same ingest hook we use elsewhere (cleaner, detaches us from Gastronomy internals).
- Prefer B. Detect Gastronomy presence, subscribe to its meal-delivery hook if it exposes one, else fall back to noop w/ warning letter on load.

**Acceptance**:
- Patron eating Gastronomy-served meal → `DietTracker.AddFoodSource` fires, variety + favourites update.
- Without Gastronomy: no regression, no log spam.
- Warning letter fires once if Gastronomy loaded but hook point unavailable.

### US-076 — Compat matrix publish
**Effort**: 0.5 d
**Scope**:
- Publish `classifier-matrix.csv` summary as a Workshop-page section (rendered from the CSV).
- Per-mod "Works out of the box / Works with shipped override / Requires user tweak" badge.
- Link to "How to submit a compat fix" — Workshop comment template + GitHub issue template.

**Acceptance**:
- Workshop description updated with compat table.
- GitHub issue template `compat-fix.md` exists.
- Comment-response SLA documented (7-day turnaround target for compat reports).

## Blockers

None — prerequisites are only owning the mods (free on Workshop).

## Risks

- **Mod updates break fix XML.** Each target mod ships updates independently. Mitigation: per-mod override file + `PatchOperationConditional` gating means a broken override doesn't crash load — only that mod's fix silently fails. Document the matrix as "current as of YYYY-MM-DD" and re-test per release.
- **Time sink.** Five mods × curated XML × re-testing per our release = non-trivial maintenance. Budget one day every release for matrix re-validation.

## Notes

- This phase is calendar-time short (3–4 d) but lead-time long — owning + loading 5 mods takes patience, not keystrokes.
- Publish matrix **before** Phase 5. Post-Phase-5 misclassification triggers hediffs — much worse player-feel than mood.
- Feeds directly into Phase 11 release polish (matrix is a long-lived artifact).
