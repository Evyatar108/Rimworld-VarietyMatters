# Phase 10 — Hedonic Palate (stretch)

## Status

**Deferred / stretch.** Do not implement until: (a) Phases 1–5 shipped + stable, (b) Workshop signal confirms demand, (c) balance bandwidth available.

Codex review: "balance tarpit that cannibalizes drugs/ideology/traits/food." Agreed — this is a risk, not a slam-dunk. Ship only when the mod's differentiation budget has been spent on higher-signal features first.

## Why stretch

- Touches mood balance pervasively — a single wrong curve slider spirals into global mood feel.
- Overlaps semantically with vanilla Gourmand trait + Phase 2 Pickiness spectrum.
- Adds a fifth axis to an already-complex variety/favourites/signatures/sensitivity stack.
- Low incremental signal vs. polish investment elsewhere.

## Goal (if revisited)

Port Oxygen Not Included-style hedonic adaptation: **a pawn's effective variety expectation rises after sustained high-quality eating and falls after simpler food**. Fine meals stop feeling "fine" after a month of nothing else.

## Stories (design sketch only)

### US-100 — Hedonic expectation drift
**Effort**: M (~2 d dev + 2 wk balance playtest)
**Sketch**:
- New per-pawn floor: `hedonicFloor : float` on `PalateProfile` (requires R-001 first).
- Drift up by 0.01/day while eating above-tier meals; drift down by 0.02/day while eating at/below baseline.
- Variety mood multiplier scales *inversely* to `hedonicFloor` — high floor = smaller positive impact from lavish.
- UI: "Pampered" / "Resilient" / "Neutral" tooltip chip.

### US-101 — Consolidation w/ Phase 9 voyage monotony
**Note**: voyage monotony (US-090, Odyssey ice-box) and hedonic drift overlap thematically — cabin-fever is hedonic drift accelerated by gravship environment. If both ship, share the `hedonicFloor` field.

## Risks

- **Balance spiral.** One bad curve → every Workshop review mentions "lavish feels useless now."
- **Trait conflict** with Gourmand + Pickiness. Explicit interaction table required.
- **Save migration** if `hedonicFloor` is added late. R-001 must land first.

## Notes

- Keep under "stretch" banner. Do not ticket for execution without explicit re-evaluation.
- If the mood/variety experience already feels "deep enough" after Phases 1–5, consider **cutting Phase 10 permanently**. A simpler mod is a better mod.
