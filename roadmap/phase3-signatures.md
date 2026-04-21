# Phase 3 — Signature Dishes

## Goal

Flagship differentiator. Ties cook identity to colony memory. None of Redux / VFVE / Improved touch cook-attribution gameplay.

## [BLOCKER-1] — RESOLVED (decompile verified 2026-04-20)

Tech review flag: US-046 claims `CompFoodPoisonable.cookingPawn` exists in RimWorld 1.6. **Verified via decompiler MCP: NO such field exists.** `CompFoodPoisonable` members are only `cause` (FoodPoisonCause enum) and `poisonPct` (float). The cook pawn is passed transiently to `Notify_RecipeProduced(Pawn pawn)` and consumed for poison-chance roll, then discarded:

```csharp
public override void Notify_RecipeProduced(Pawn pawn) {
  if (Rand.Chance(pawn.GetRoom()?.GetStat(RoomStatDefOf.FoodPoisonChance) ?? ...))
    SetPoisoned(FoodPoisonCause.FilthyKitchen);
  else if (Rand.Chance(pawn.GetStatValue(StatDefOf.FoodPoisonChance)))
    SetPoisoned(FoodPoisonCause.IncompetentCook);
}
```

**Resolution**: must ship own `CompVMCookAttribution` (new sub-task **US-046a**). Meal-attached comp, stores `WeakReference<Pawn>` or `int pawnId` for cook.

**Hook sites (both viable)**:
- **Option A**: Harmony postfix on `CompFoodPoisonable.Notify_RecipeProduced(Pawn)` — reuses vanilla's cook-pawn signal, minimal surface. Preferred.
- **Option B**: Postfix on `GenRecipe.MakeRecipeProducts` — broader but works without `CompFoodPoisonable` present on the meal def.

**US-046 effort**: bumped **3 d → 4–5 d** (comp + Scribe + lifecycle attach). Still fits Release 1.1 window.

## Stories

### US-046 — Cook recipe mastery tracking
**Effort**: 4–5 d (bumped from 3 d; includes US-046a custom comp)
**Scope**:
- New `GameComponent CookMasteryRecord`. Mirrors `FavouriteFoodRecord` architecture (§13).
- Per-pawn dict: `Dictionary<Pawn, CookData>` where `CookData : IExposable` holds `Dictionary<ThingDef, int>` (mealDef → cumulative cooked count).
- Hook: attribute produced meal to cook pawn via **US-046a CompVMCookAttribution** (see sub-story below).
- Scribe: nested dict w/ `LookMode.Reference` for outer key (pawn), `LookMode.Def` for inner key, `LookMode.Value` for count.
- Lifecycle cleanup: mirror §9 — postfixes on `Pawn.Kill` + `Pawn.Destroy` both call `CookMasteryRecord.RemoveTrackedPawn(pawn)`. Idempotent.
- Threshold: `signatureMealsThreshold` setting (default 50 cooked) → cook "masters" that mealDef.

### US-046a — `CompVMCookAttribution` (new sub-story post-BLOCKER-1 resolution)
**Effort**: 1.5–2 d (included in US-046 total)
**Scope**:
- New `ThingComp CompVMCookAttribution` attached to meal defs via XML patch on meal `ThingDef`s.
- Field: `int cookPawnId` (use `Pawn.thingIDNumber`) + optional `string cookPawnName` for UI fallback when pawn is gone.
- Scribe: `Scribe_Values.Look(ref cookPawnId, "cookPawnId", -1)` + matching name string. Never `Scribe_References<Pawn>` — pawn may be destroyed before meal is eaten.
- Hook: **Option A (preferred)** — Harmony postfix on `CompFoodPoisonable.Notify_RecipeProduced(Pawn pawn)`. Set `cookPawnId = pawn.thingIDNumber` on the sibling `CompVMCookAttribution` of the same `ThingWithComps`.
- **Option B fallback** — postfix on `GenRecipe.MakeRecipeProducts` for meal defs without `CompFoodPoisonable`. Broader coverage; use only if meals ship without `CompFoodPoisonable` in some modded food def.
- XML: apply `CompVMCookAttribution` to meal ThingDefs via `PatchOperationAddModExtension`-style patch or `CompProperties` addition.
- Consumer (US-047): read cook id at consumption, resolve via `Find.WorldPawns` or map-pawn scan; fall back to `cookPawnName` for UI if resolution fails.

**Acceptance**:
- Cooked meal carries cook id through save/load.
- Cook dies before meal eaten → UI shows "cook: Alice (deceased)" via stored name.
- Save/load resilient; default id `-1` for pre-US-046a saves (existing meals have no attribution, treated as "unknown cook").
- Mod-removal resilient: remove mod → meals have no CompVMCookAttribution comp, vanilla behavior resumes cleanly.

**Acceptance**:
- Cook A makes 50 FineMeals → mastery flag on pawn A for FineMeal.
- Save/load preserves.
- Cook dies → entry removed same tick.
- Mastery threshold tunable via settings.

### US-047 — Signature mood + variety bonus
**Effort**: 2 d (depends on US-046)
**Scope**:
- When meal `M` consumed AND meal was cooked by pawn who has mastered `M`:
  - Eater receives new `Thought_Memory VM_AteSignatureDish` (+2 mood tunable, 12 h tunable).
  - Variety score for that meal gets × 1.15 multiplier (stacks w/ recipe tier bonus).
- Requires producer tracked on meal via US-046a `CompVMCookAttribution` (shipped as part of this phase — see US-046a above, vanilla `CompFoodPoisonable` has no cook field).
- Multiplier stacking order w/ Phase 2 sensitivity: apply **signature multiplier first** (meal property), then **sensitivity** (eater property). Document in both files.
- Thought subclass pattern mirrors §13 `Thought_Memory_VMAteFavourite` — runtime-tunable mood + duration.

**Acceptance**:
- Cook A masters FineMeal → any pawn eating A's FineMeal gets +2 mood; cook B's FineMeal does not.
- Variety score for signature meal visible w/ × 1.15 bonus in Details window tooltip.
- Setting changes take effect on NEW thoughts; existing memories retain prior values.
- Back-to-back suppression mirrors favourite-thought ethos.

### US-048 — Signature dish UI
**Effort**: 2 d (depends on US-046 + US-047)
**Scope**:
- Cook pawn inspect → Bio tab → new row:
  ```
  Signature dishes: FineMeal (87 cooked), LavishMeal (62 cooked)
  ```
  Show only mealDefs the cook has mastered.
- Details window (§13 UI) — eaten meal row shows "🍳 Master-cooked by Alice" badge if signature.
- Letter on mastery cross: `VarietyMatters.MasteryAchieved` — "Alice has mastered Fine Meals" (non-intrusive, auto-archive after 5 days).
- Cook progress bar: optional stretch — shows closest-to-mastery recipe ("FineMeal 42/50") when not mastered yet.

**Acceptance**:
- Cook w/ mastery → Bio tab row renders, w/ tooltip listing all mastered mealDefs.
- Letter fires exactly once per (pawn × mealDef) crossing threshold.
- Details window badge shows only for signature meals.
- Non-cook pawns don't see the Bio row.

### US-049 — Signature settings + kill-switch
**Effort**: 0.5 d
**Scope**:
- New settings section "Signature Dishes":
  - `signatureDishesEnabled` (master toggle, default ON).
  - `signatureMoodBonus` (default +2, range 0–10).
  - `signatureMoodDurationHours` (default 12, range 1–72).
  - `signatureMealsThreshold` (default 50, range 10–500).
  - `signatureVarietyMultiplier` (default 1.15, range 1.0–2.0).
- Kill-switch semantics per §13 — toggle off freezes side effects but **preserves** `CookMasteryRecord` data.
- Excluded from `Profile` / `ApplyProfile` / `MatchesProfile` (§13 ethos; signatures is feature-flavour not difficulty).

**Acceptance**:
- Disable → no new mastery letters fire, no new signature thoughts; CookData preserved.
- Re-enable → mastery + thoughts resume immediately on next ingest.
- Preset switch doesn't flip active-preset indicator when tweaking these knobs.

## Risks

- Cook attribution via `CompVMCookAttribution` (US-046a) adds a per-meal Scribed field — verify 0-impact on save size for existing stacks (default id `-1` serializes cheaply).
- Multiplier stacking (US-047 signature × US-042 sensitivity) — compound effect on extreme pawns. Test w/ Supertaster + Venerated precept + mastered lavish — aggregate should still read reasonable.
- UI real estate: Bio tab has limited space — may need to collapse under a tooltip if many mealDefs mastered.

## Notes

- `CookMasteryRecord` is a separate GameComponent from `FavouriteFoodRecord` — don't fold. Same reasoning as §13 — save-format stability trumps code DRY.
- Consider later: apprentice mastery transfer (cook adjacent to master → bonus mastery credit). Scope for Phase 3.5 if demand arises.
- Cross-ref in CLAUDE.md post-impl: new §14 "CookMasteryRecord" mirrors §13 exactly.
- **Integration w/ Phase 4 DT-L4 Quality Cooking**: master cooks have 0% failure chance on mastered recipes regardless of skill. Document bidirectional cross-ref.
