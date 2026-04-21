# Phase 2 — Variety Sensitivity (Traits / Genes / Precept)

## Goal

One shared pipeline that modulates variety mood impact. Three consumer stories wire into it: trait spectrum, Biotech genes, Ideology precept.

## Stories

### US-042 — Variety sensitivity multiplier pipeline
**Effort**: 2 d (foundation; all other Phase 2 stories depend on this)
**Scope**:
- New static class `VarietySensitivity` w/ `GetMultiplier(Pawn) : float`.
- Aggregates: gene contribution × trait contribution × precept contribution × baseline 1.0.
- Clamped `[0.1, 3.0]` — never fully cancel. Full cancel = `VM_NoVarietyNeed` gene (unaffected, still uses existing `Disabled` path).
- Consumers (all scale by multiplier):
  - `Thought_NeedFoodVariety.MoodOffset` — subclass pattern, mirror `Thought_Memory_VMAteFavourite` §13.
  - Favourite thought `Thought_Memory_VMAteFavourite.MoodOffset` — already tunable; multiply further.
  - Optional: palate freshness threshold scaling (TBD during impl — may be too much).
- Cache: version-invalidated like `DietTracker._version`. Bumps on:
  - `Pawn_GeneTracker.Notify_GenesChanged` postfix
  - Settings change (ideology precept selection is settings-adjacent)
  - Pawn ideo swap — hook `Pawn_Ideo.SetIdeo` postfix
- **Do NOT cache raw bool** per §12 principle — runtime-mutable gene/precept state.
- Traits are stable post-pawn-gen → trait component resolved once (can cache), but pipeline aggregates lazily.

**Acceptance**:
- Multiplier read at same sites as §12 `Disabled` — same gate-ordering principles (null-safe chains).
- Gene add/remove reflects on next scoring tick.
- Trait is immutable — no test for mid-save flip.
- Precept swap via ideo change reflects on next scoring tick.
- Clamp floor `0.1` enforced — aggregate can't zero out.

### US-043 — Pickiness trait spectrum
**Effort**: 1.5 d (depends on US-042)
**Scope**:
- New `TraitDef VM_Pickiness` w/ 4 `DegreeData`:
  - Degree **−2 Plain Eater**: sensitivity × 0.5, palate freshness threshold × 0.6 (tolerates repetition)
  - Degree **−1 Easygoing**: × 0.75
  - Degree **+1 Refined**: × 1.25
  - Degree **+2 Connoisseur**: × 1.5
- Trait conflict tags: Plain Eater × Gourmand exclusive, Connoisseur × Ascetic exclusive.
- XML: `Defs/TraitDefs/Traits_VarietyMatters.xml`.
- Optional (stretch): Connoisseur unlocks 4th favourite slot (grain/spice bucket). **Cut if `FavouriteFoodRecord` refactor is invasive** — keep sensitivity-only at first.

**Acceptance**:
- Refined pawn eats repeated meal → larger negative thought than baseline (visible in mood inspector).
- Plain Eater eats same → smaller penalty.
- Trait spawner includes VM_Pickiness in regular gen.
- Trait conflicts enforced on gen + PrepareCarefully.

### US-044 — Supertaster / Bland-palate genes (Biotech)
**Effort**: 1 d (depends on US-042)
**Scope**:
- `VM_Gene_Supertaster` — sensitivity × 1.5, metabolism −1, complexity 2.
- `VM_Gene_BlandPalate` — sensitivity × 0.5, metabolism +1, complexity −1.
- Mutually exclusive via `exclusionTags: ["VM_VarietySensitivity"]`.
- `VM_NoVarietyNeed` joins the same exclusion tag (refactor existing def to share).
- XML: `Defs/GeneDefs/Genes_VarietyMatters.xml`, `Defs/GeneCategoryDefs/` — reuse existing VarietyMatters category or add "Palate".
- Gated by `ModsConfig.BiotechActive`.
- Icons: placeholder → normalize via `rimworld-tools:gene-icon` skill post-impl.

**Acceptance**:
- Supertaster xenotype pawn shows 1.5× mood swings on variety thoughts.
- Save/load preserves; existing saves w/o Biotech unaffected.
- Gene exclusion tag prevents Supertaster + BlandPalate + NoVarietyNeed combos.
- Pawn generator respects metabolism + complexity costs.

### US-045 — Precept: Variety Sensitivity (Ideology)
**Effort**: 1.5 d (depends on US-042)
**Scope**:
- New `IssueDef VM_Issue_VarietySensitivity`.
- Three `PreceptDef` options:
  - `VM_VarietyMundane` — sensitivity × 0.7 ("Food is fuel")
  - `VM_VarietyBalanced` — × 1.0 (default, neutral)
  - `VM_VarietyVenerated` — × 1.4 ("Feast is sacred")
- Thematic associations in MemeDefs (suggestions, not hard-bound):
  - Ascetic meme → Mundane
  - Rancher / Cannibal / Gluttonous → Venerated
- XML: `Defs/IssueDefs/`, `Defs/PreceptDefs/`.
- Gated by `ModsConfig.IdeologyActive`.
- **Scope-cut**: precept only modulates the existing variety need. It does NOT add new "ate plain food" thought categories.

**Acceptance**:
- Ideoligion w/ Venerated → variety mood swings amplified 1.4× (visible in thought stack tooltip).
- Ideo swap reflects next scoring tick (US-042 invalidation hook).
- Non-Ideology save → precept absent, no regression.
- Existing saves — default precept is Balanced, no migration needed.

## Risks

- Multiplier stacking: Supertaster (1.5) + Connoisseur (1.5) + Venerated (1.4) = 3.15 → clamp to 3.0. Acceptable; document in tooltip.
- Precept UI takes real estate in Ideology editor — verify rendering w/ multiple PreceptDefs.
- Connoisseur 4th slot stretch: `FavouriteFoodRecord` slot model is meat/veg/wild — adding 4th requires schema change. **Defer unless trivial.**

## Notes

- US-042's invalidation hook list is the single point of truth — any future sensitivity source (e.g. hediff?) must bump the same version.
- Settings UI shouldn't surface US-042 internals; users see trait/gene/precept only.
- Cache invalidation follows §12 principle: compute is cheap, just bump version and let consumer re-read.
