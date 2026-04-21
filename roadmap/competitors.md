# Competitor landscape

Snapshot as of 2026-04-20. Drives differentiation decisions in [ROADMAP.md](ROADMAP.md).

## Variety Matters Redux — main competitor
- Steam: https://steamcommunity.com/sharedfiles/filedetails/?id=3044059819
- GitHub: https://github.com/Evyatar108/Rimworld-VarietyMatters
- **Features**: variety need bar, 3 tracking modes (Ingredients / MealName / Both), autonomous cooking AI (skill-scaled memory, proximity tiebreak), meal-quality merging toggle, ingredient stacking prevention, Ascetic/Gourmand/Cannibal handling, insect-meat despised precept, prisoner/slave reduced requirement toggles.
- **Aug 2025 changelog**: large-colony perf improvements, settings-menu freeze fix.
- **Pitched as**: cooking AI + perf for large colonies + UI tooltip richness.

## Vanilla Food Variety Expanded (VFVE)
- Steam: https://steamcommunity.com/sharedfiles/filedetails/?id=3334272487
- **Features**: recent meals + ingredients memory (tunable sizes), variety mood bonus, monotony penalty, **per-colonist favorite meal/ingredient counts as double variety**.
- **Pitched as**: "no patch to food-searching job → almost no perf cost". The lightweight alternative.
- **Requires**: Harmony + Vanilla Expanded Framework.
- **Overlap**: single flat-flag favourite — we ship a richer 3-slot system w/ saturation, crystallization, back-to-back suppression.

## Variety Matters Improved
- Steam: https://steamcommunity.com/sharedfiles/filedetails/?id=2353847457 (+ continued fork: 2585707684)
- **Features**: memory = 2× expectation, pawns prefer novelty over quality when net-positive, expectation scales w/ thought level, sickness skips need, rotten excluded, seasonal crop availability consideration.
- **Status**: **No 1.4/1.5/1.6 update.** User complaints dominate.

## Variety Matters Dinner Time
- Steam: https://steamcommunity.com/sharedfiles/filedetails/?id=2326581524
- Updated Dec 2021, **abandoned** (no 1.4/1.5/1.6), flagged "incompatible with RimWorld" ToS on store page but still subscribable.
- **Features absorbed in Phase 4** — see [phase4-dinnertime.md](phase4-dinnertime.md).
- **Opportunity**: absorb abandoned mod's user base; pitch as replacement.

## Nearby mods (adjacent niches, non-compete)

- **Gastronomy** — restaurant/waiter/table system. Compat target, not competitor.
- **Variety Matters - More Compat** — patch addon for modded foods. Merge candidate.
- **Food Texture Variety** — cosmetic-only sprites.
- **Smarter Food Selection** — food-picking QoL, no variety need.
- **Real Dining** / **Room Food** / **Common Sense** — overlap w/ Dinner Time features (dining room pref, spoiling pref). Phase 4 notes incompat risk.

## Differentiation openings (summary)

1. **Biotech genes** — no competitor ships gene-level opt-out / sensitivity modifiers. Phase 2 US-044.
2. **Per-pawn favourite slots w/ saturation** — richer than VFVE flat flag. Shipped in F-007.
3. **Modded-category-aware classifier** (`ThingCategoryDef.parent` walk) — no competitor advertises.
4. **No-alloc cooking AI** — harder perf claim than Redux's "we optimized it".
5. **Ideology precept-level sensitivity** — Phase 2 US-045.
6. **Signature dishes** — Phase 3 flagship. None of the competitors touch cook-identity gameplay.
7. **Exclusion lists** — universally requested, none ship it well. Phase 1 US-039.
8. **Dinner Time absorption** — abandoned mod, active user demand.
9. **Diet-health hediffs** — zero competitor coverage. Phase 5.

## User complaints (harvest list — drives backlog)

- Exclusion lists missing (kibble, insect jelly, corpses, nutrient paste). → US-039.
- Dev-mode favourites editor absent. → US-040.
- Character-Editor / PrepareCarefully hooks absent. → US-041.
- Insect jelly shown as "unknown" in Redux UI. → covered by our classifier + Phase 1 exclusion default.
- Settings-menu lag (Redux, since fixed). → our debounced pattern already addresses.
- Mood asymmetry complaint on VFVE (−12 / +2). → our tunable `maxNegative`/`maxPositive` already cover.
- GitHub mirror for PRs. → already have one.
