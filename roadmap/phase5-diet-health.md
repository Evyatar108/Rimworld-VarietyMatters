# Phase 5 — Diet-Health (Scaled Path C)

## Goal

Low-variety diets → reversible health consequences. Zero competitor coverage. **Scaled to reuse existing meat/veg/wild bucket classifier from `FavouriteFoodRecord` (§13)** — no new per-mod ingredient bucket mapping needed. Full nutrient system deferred (see §Stretch).

## Stories

### US-058 — Bucket intake tracker
**Effort**: 1 d
**Scope**:
- Extend `FavouriteFoodRecord` w/ per-pawn rolling 15-day bucket intake counter (meat / veg / wild credits).
- Counter: `Queue<(int tick, Bucket bucket)>` trimmed on each mutation to `tick > currentTick - 15*60000`.
- Scribe: nested list w/ `LookMode.Value` for tick + enum.
- Bucket classification reuses existing `ClassifyIngredient` — no new classifier code.

**Acceptance**:
- Meat meal eaten → tick logged in `meatCredits` queue.
- After 15 days idle, counter drains to empty.
- Save/load preserves queue.
- `FavouritesEnabled = false` freezes the counter (existing ethos).

### US-059 — Three launch hediffs
**Effort**: 1 d (depends on US-058)
**Scope**:
- Below-threshold N credits / 15 days → severity ramp on hediff, +0.05/day while under, −0.15/day while over.
- Hediff defs:
  - `VM_ProteinStarved` (below meat threshold, default 8 credits / 15 d): ↓ melee DPS 15% at Notable, 30% at Severe; global work speed ↓ 5–15%.
  - `VM_Scurvy` (below veg threshold, default 12 credits / 15 d): ↓ immunity gain 10–25%, mood thought `VM_FeelingWeak` at Notable+.
  - `VM_MonotonyFatigue` (all eaten buckets = 1 distinct bucket last 15 d, regardless of count): ↓ global work speed 10–20%, ↓ rest fall rate.
- 3 stages: Minor (0.1–0.3, hidden), Notable (0.3–0.7, mood + letter), Severe (0.7–1.0, bleeding/collapse chance, capped non-lethal).
- Tended by Doctor w/ medicine — medicine potency accelerates recovery while diet corrects.
- Wild bucket skipped (too situational, Gourmand-only).

**Acceptance**:
- Pawn eating meat-only 15 d → `VM_Scurvy` accumulates; fixing diet regresses it.
- Severity capped below 1.0 (no direct kill).
- Existing saves loaded → no retroactive punishment (empty queue → zero severity).
- `dietHealthEnabled = false` → no accumulation; existing hediffs frozen at current severity (don't auto-heal).

### US-060 — Doctor diagnosis + letter alerts + Need tooltip row
**Effort**: 1 d (depends on US-059)
**Scope**:
- Doctor `Inspect` on diagnosed pawn → appends diet cause line: "Likely cause: protein deficiency — serve more meat".
- Letter fires on Notable stage entry (once per hediff lifecycle): "Alice shows early scurvy symptoms".
- Need tooltip extension (§13 UI): row "Diet balance: 🥩 4/8 · 🥬 12/12" w/ colored bars.

**Acceptance**:
- Doctor treats → hediff tend success visible in Health tab.
- Letter auto-archives after 5 days.
- Tooltip shows current counter vs threshold w/ color (red < 50%, yellow 50–75%, green ≥ 75%).

### US-061 — Settings + default-OFF gate
**Effort**: 0.5 d
**Scope**:
- New settings section "Diet Health":
  - `dietHealthEnabled` (master, **default OFF** — opt-in, too punitive for casual).
  - Window length (days, default 15, range 5–30).
  - Per-bucket credit threshold (int, default 8 meat / 12 veg).
  - `dietHealthSeverityRate` multiplier (default 1.0, range 0.25–2.0) — Lenient/Realistic/Hardcore presets.
- Enable warning letter on first toggle-on.
- Excluded from `Profile` (§13 ethos — orthogonal feature).

**Acceptance**:
- Default load → no hediffs ever fire, zero perf cost from system.
- Toggle on mid-save → warning letter, tracker begins accumulating from current tick (no retroactive).
- Setting change takes effect on next ingest tick.

### US-062 — Playtest + balance pass
**Effort**: 1–2 wk live
**Scope**:
- Playtest on three difficulty settings + three colony archetypes (naked brutality / tribal / crashlanded).
- Validate: first hediff appears after ~3 in-game weeks of poor diet (not instant).
- Validate: Lenient preset extends to ~6 weeks.
- Validate: severity recovery feels proportional (2 weeks proper diet → fully healed).
- Tune thresholds based on telemetry / playtest feel.

**Acceptance**:
- All three presets produce distinct gameplay feel.
- Crashlanded scenario doesn't fire hediff in first week (defensive default).

## Risks

- **Default-OFF means discoverability** — users may miss the feature. Mitigate: one-time tip letter on load ("Try diet health in settings").
- **Feels punishing** — Severe stage + mood penalty + stat debuff stacks. Playtest tight.
- **Mod food compat** — bucket classifier walks `ThingCategoryDef.parent`. Modded meat-but-not-categorized-as-meat could slip through. Validate w/ RimCuisine / VCE-Cuisine.
- **Mechanoid / non-eaters** — need explicit exempt (ties to Phase 6 US-068).

## Notes

- `FavouriteFoodRecord` is the natural home — favourite already walks buckets. Extending here keeps save format contiguous (one component, one new field).
- Kill-switch semantics (§13): disable freezes hediff severity; does NOT wipe tracker queue. Re-enable resumes from current state.
- Name `VM_MonotonyFatigue` intentionally distinct from vanilla `Malnutrition` — orthogonal axis.

---

## Stretch — Full Nutrient System

Deferred. Documented for future revisit if player demand emerges. High compat cost (per-mod ingredient bucket mapping).

**Delta vs scaled path**:
- 5–6 nutrient buckets (protein / vitamins / carbs / fats / fibre / micronutrients) instead of 3 broad meat/veg/monotony categories.
- Per-ingredient override table in settings (UI for users to tag modded foods).
- Community override file — curated per-mod bucket maps for RimCuisine / VCE-Cuisine / Medieval Overhaul etc.
- Additional hediffs: `VM_FatDeficiency` (cold intolerance), `VM_DigestiveIssues` (fibre), `VM_Anemia` / `VM_Beriberi` (micros).
- Supplement craftable items — drug lab, late-game convenience + caravan trade good.
- Research tree node: `Nutritional Science` unlocks supplements + per-bucket insight.

**Why deferred**:
- Every modded food mod needs bucket mapping to feel right. Community override file model is maintenance-heavy.
- Scaled Path C already ships the core feel (reversible diet-driven hediffs) w/ zero compat debt.
- Revisit when:
  - Scaled version ships + is well-received.
  - Community asks (comment volume on Workshop).
  - A standard modded-food taxonomy emerges (unlikely).

**Architecture note**: if revisited, would replace `FavouriteFoodRecord` bucket classifier extension w/ a dedicated `NutrientIntakeRecord : GameComponent`. Mirrors the same §13 lifecycle/gate patterns. Settings move from "Diet Health" fold-out to a dedicated "Nutrition" section w/ advanced per-ingredient overrides.
