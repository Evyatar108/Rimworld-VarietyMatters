# VarietyMatters — Roadmap

Post-US-038 roadmap, **revised post-review** (see [reviews.md](reviews.md)). Differentiates vs competitors (Redux, VFVE, Improved, Dinner Time). Phases ship across 3 Workshop releases; cross-cutting refactors tracked separately.

> **Current state**: US-034 → US-038 + F-001/F-003/F-007 merged to master (2026-04-21). Next reserved IDs start at **US-039**.

## Blockers

See [reviews.md](reviews.md) § Blockers for full context.

- **[BLOCKER-1]** ✅ **RESOLVED 2026-04-20** — decompile verified `CompFoodPoisonable.cookingPawn` does NOT exist. Must ship own `CompVMCookAttribution` (new sub-task US-046a). US-046 effort bumps 3 d → 4–5 d. Details in [phase3-signatures.md](phase3-signatures.md).
- **[BLOCKER-2]** ✅ **RESOLVED 2026-04-20** — decompile verified `JobGiver_GetFood.TryGiveJob(Pawn)` is the hook; custom `TimeAssignmentDef VM_Meal` required (no vanilla Meal slot). US-051 effort unchanged. Details in [phase4-dinnertime.md](phase4-dinnertime.md).
- **[RISK-1]** US-042 multiplier cache policy — still open. Clarify "multiplier recomputed each call" per §12 before starting Phase 2.

## Context

- **[competitors.md](competitors.md)** — competitor feature inventories + differentiation openings.
- **[reviews.md](reviews.md)** — consensus findings + controversial-decision log from the 3-agent review.

## Release cadence

| Release | Target | Headline pitch | Contents |
|---|---|---|---|
| **1.0** | ~2–3 wk | "Quick wins + polish" | Phase 1 (US-039/040/041) + O-1/O-2/O-5 + Phase 11 scaffold |
| **1.1** | ~6–8 wk | "Sensitivity + Signatures" | Phase 2 + Phase 3 + R-002 (internal events only) + O-3/O-4 + R-001 (PalateProfile) |
| **1.2** | ~10–12 wk | "Dinner Time absorbed" | Phase 0.5 + Phase 4a (parity) + Phase 4b (synergies) |
| **1.3+** | post-signal | TBD by Workshop demand | Phase 5 (diet-health), Phase 6 (themed), Phase 8 (storyteller), Phase 9 (DLC hooks) |

Don't batch releases — each ships = fresh Workshop surfacing + fresh comment cycle.

## Phase sequence

| # | Title | Doc | US / O range | Effort | Release |
|---|---|---|---|---|---|
| 1 | Tier-S Quick Wins | [phase1-quickwins.md](phase1-quickwins.md) | US-039 → US-041 | ~1.5 wk | 1.0 |
| — | Opportunistic wins | [opportunistic-wins.md](opportunistic-wins.md) | O-1, O-2, O-5 | ~8 h | 1.0 |
| 0.5 | Compat Matrix | [phase0.5-compat.md](phase0.5-compat.md) | US-073 → US-076 | ~1 wk | 1.2 (pre-Phase-4) |
| 2 | Sensitivity (traits / genes / precept) | [phase2-sensitivity.md](phase2-sensitivity.md) | US-042 → US-045 | ~1.5 wk | 1.1 |
| 3 | Signature Dishes | [phase3-signatures.md](phase3-signatures.md) | US-046 → US-049 + US-070 | ~1.5 wk | 1.1 |
| — | R-002 EventBus (internal) | [architecture.md](architecture.md) | R-002 | ~2 d | 1.1 |
| — | Opportunistic wins | [opportunistic-wins.md](opportunistic-wins.md) | O-3, O-4 | ~1.5 d | 1.1 |
| — | R-001 PalateProfile unification | [architecture.md](architecture.md) | R-001 | ~1 wk | 1.1 (pre-Phase-5) |
| 4a | Dinner Time parity | [phase4-dinnertime.md](phase4-dinnertime.md) | US-051, US-052, US-054, US-055, US-056 | ~2 wk | 1.2 |
| 4b | Dinner Time synergies | [phase4-dinnertime.md](phase4-dinnertime.md) | US-053, US-057 | ~1 wk | 1.2 |
| 11 | Release Polish | [phase11-release-polish.md](phase11-release-polish.md) | US-110 → US-114 | ongoing | all |
| 5 | Diet-Health (scaled) | [phase5-diet-health.md](phase5-diet-health.md) | US-058 → US-062 | ~3–4 d dev + 2 wk playtest | 1.3 |
| 6 | Themed Extras | [phase6-themed-extras.md](phase6-themed-extras.md) | US-067, US-068 (US-069 stretch) | ~2 d | 1.3 |
| 8 | Storyteller / Social (trimmed) | [phase8-storyteller.md](phase8-storyteller.md) | US-080, US-081 | ~4 d | 1.3 |
| 9 | DLC Hooks (Biotech + 3 Anomaly) | [phase9-dlc-hooks.md](phase9-dlc-hooks.md) | US-095, US-096, US-097 | ~5 d | 1.3 |
| 7 | Cookbook UI | [phase7-ui-features.md](phase7-ui-features.md) | US-070 (absorbed into Phase 3); US-071 stretch | ~3 d | 1.1 |
| 10 | Hedonic Palate | [phase10-hedonic.md](phase10-hedonic.md) | US-100, US-101 | stretch | deferred |

## Cross-cutting refactors

- **[architecture.md](architecture.md)** — `R-001 PalateProfile` consolidation, `R-002 EventBus` (internal-first, public API marker deferred to 1.2+).
- **Key reposition from v1 of this doc**: R-001 lands **mid-roadmap (Release 1.1, pre-Phase-5)**, NOT at the end. Rationale: Phases 5/6/9 each add Scribed fields to `FavouriteFoodRecord`; deferring consolidation = multi-generation migration debt bomb.

## Stretch / deferred

- **Phase 10 Hedonic palate** — [phase10-hedonic.md](phase10-hedonic.md). Do not build until 1.0–1.3 ship + Workshop signal + balance bandwidth available. May be cut permanently if variety feel is "deep enough" after Phase 5.
- **Phase 9 Odyssey hooks** — all US-090–US-094 in ice-box. Revisit if post-1.2 Workshop comments mention Odyssey.
- **US-069 Procedural allergies** — downgraded per review (codex: "hidden RNG punishment is complaint archetype"). Re-evaluate post-1.2.
- **US-071 Meal planner** — stretch. MVP is manual-suggest only (codex: "B2B tooling"). Re-evaluate post-1.1.
- **Full nutrient system** (protein / vitamins / carbs / fats / fibre / micros) — documented in [phase5-diet-health.md](phase5-diet-health.md) §Stretch. High compat cost. Revisit only if community explicitly asks.
- **Gastronomy waiter-favourites** deeper integration beyond compat shim (US-075) — low priority.

## Phase sequencing rationale

- **Phase 1 + O-1/O-2/O-5 → Release 1.0 first** — instant positive reviews (exclusion lists address universal complaint), zero dependencies, pays for the roadmap's ambition.
- **Phase 2 BEFORE Phase 3** (reversed from v1 brainstorm) — US-042 sensitivity pipe unblocks both. Arch review: reversed order forces retrofit.
- **Phase 3 US-070 Cookbook UI folds in** — local audit: `VarietyCookingRecord.recentByPawn` already tracks per-chef recipes, faster than estimated.
- **R-001 mid-roadmap (Release 1.1, pre-Phase-5)** — consensus agent finding. Avoids multi-generation schema creep.
- **Phase 4 split into 4a (parity) / 4b (Phase 3 synergies)** — arch review: 7 stories in one phase rots half-implemented. US-053 + US-057 need Phase 3 stable first.
- **Phase 11 starts early, runs continuous** — localization scaffold (US-110) + Workshop refresh (US-111) ship w/ Release 1.0. Comment SLA (US-112) runs continuous from then.
- **Phase 5 / 6 / 8 / 9 deferred to 1.3** — only build what Workshop signal requests. Phase 5 diet-health ships first among these (most systemic gameplay).
- **Phase 10 deferred permanently** pending bandwidth signal.

## Conventions

- Ticket format per CLAUDE.md §2: `feat: [US-###] - short description` / `fix: [F-###] - ...`.
- **ID collision policy**: US-### reserved here (US-039 → US-123) live only in doc, not master. Check this file's phase tables before claiming a new US-###. Check `git log --oneline master | grep -E '\[F-'` before claiming F-###.
- Each story lists acceptance criteria concrete enough for `ralph-orchestration:create-prd`.
- Settings additions default-OFF when they change gameplay meaningfully. Kill-switches preserve state (§13 ethos).
- Settings discipline: O-5 reclaims ~200 px space in 1.0; don't squander it by Phase 5.

## Open coordination

- If two phases touch the same hot path (e.g. `FoodUtility.FoodOptimality` — O-1/O-2/US-039/US-042/US-054/US-069 all target it), sequence them; consolidate concerns into one postfix per arch review note.
- Settings UI real estate is tight — batch additions, reuse fold-out sections. O-5 + future discipline.
- Compat matrix (Phase 0.5) re-runs once per release — budget 0.5 d in Phase 11 US-113.
