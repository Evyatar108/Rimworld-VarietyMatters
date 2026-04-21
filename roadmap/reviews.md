# Roadmap review findings

Three review agents critiqued the roadmap (tech feasibility, architecture/sequencing, codex external). Summary archived here. Resolutions are reflected in `ROADMAP.md`, `architecture.md`, and phase files.

## Consensus (all 3 agents agreed)

1. **R-001 PalateProfile must land earlier.** Phases 5/6/9 each add Scribed fields to `FavouriteFoodRecord`. Deferring = debt bomb touching 4+ schema generations.
2. **Phase 2 before Phase 3.** US-042 sensitivity pipe is a 2-day foundation. Phase 3 US-047 applies signature × sensitivity — reversed order forces retrofit.
3. **Phase 4 is over-scoped.** Absorbing Dinner Time as one phase was a trap. Split into 4a (parity) / 4b (Phase 3 synergies) — or cut aggressively.
4. **Missing compat / polish discipline.** Add Phase 0.5 compat matrix (blocks Phase 5) + Phase 11 release polish (localization, thumbnail, Workshop comment SLA).
5. **Release cadence.** Three releases (1.0 / 1.1 / 1.2) at ~6–8 wk cadence beats batching.

## Blockers

### [BLOCKER-1] ✅ RESOLVED 2026-04-20 — US-046 cook attribution
**Finding**: `CompFoodPoisonable` has NO `cookingPawn` field in 1.6 (decompile verified). Only `cause` + `poisonPct`. Cook pawn passed transiently to `Notify_RecipeProduced(Pawn)` and discarded.

**Resolution**: ship own `CompVMCookAttribution` (new sub-task US-046a). Meal-attached comp, stores cook ref. Hook options:
- Option A (preferred): Harmony postfix on `CompFoodPoisonable.Notify_RecipeProduced(Pawn)` — reuses vanilla cook signal.
- Option B: Postfix on `GenRecipe.MakeRecipeProducts` — broader coverage if meal lacks `CompFoodPoisonable`.

**US-046 effort**: 3 d → 4–5 d. Still fits Release 1.1 window. Details in [phase3-signatures.md](phase3-signatures.md).

### [BLOCKER-2] ✅ RESOLVED 2026-04-20 — US-051 job prioritizer
**Finding** (decompile verified):
- `JobGiver_GetFood.TryGiveJob(Pawn)` is the canonical food-seek job-giver in 1.6. Patchable via Harmony prefix.
- `TimeAssignmentDefOf` has 5 vanilla slots only: Anything / Work / Sleep / Joy / Meditate. **No Meal slot.** We ship `TimeAssignmentDef VM_Meal` via XML.
- Runtime checks: `Pawn_TimetableTracker.CurrentAssignment` / `GetAssignment(hour)` / `times : List<TimeAssignmentDef>`.

**Resolution**: US-051 design concretized:
1. XML def for `VM_Meal`.
2. Patch `PawnColumnWorker_Timetable` for UI.
3. Harmony prefix on `JobGiver_GetFood.TryGiveJob` — delay if not starving and VM_Meal current or within 2h lookahead.
4. During VM_Meal: recreation bias via `JobGiver_EatInGatheringArea` pattern.

**US-051 effort**: unchanged (~2–3 d). Details in [phase4-dinnertime.md](phase4-dinnertime.md).

### [RISK-1] US-042 multiplier cache policy — still open
CLAUDE.md §12 forbids caching `Disabled` bool. Roadmap proposes `_version`-bump invalidation but doesn't clarify whether the multiplier itself is cached.
**Action**: add explicit note "multiplier recomputed each call; version cache is UI aggregates only" when Phase 2 begins.

### [RISK-2] R-001 migration untested — still open
Consolidating scattered GameComponents into `PalateProfile` requires live save testing.
**Action**: dedicated playtest + rollback plan. Test on ≥10 existing save files before rolling out.

## Controversial findings (agent disagreement)

| Question | Arch agent | Codex | Resolution in roadmap |
|---|---|---|---|
| R-002 EventBus priority | Ship w/ Phase 3 | Cut — premature public-contract debt | **Compromise**: internal fan-out events land w/ Phase 3; `[VarietyMattersPublicApi]` marker deferred to 1.2+ |
| DLC hooks (Phase 9) | Accept all | Cut Anomaly + Odyssey (15–25% audience) | **Compromise**: Biotech stays, 3 Anomaly quick wins ship, Odyssey deferred to ice-box |
| Phase 5 diet-health | Keep scaled Path C | Cut — "punishment features, weak marketing" | **Keep** — default-OFF + scaled version is cheap (3–4 d). Validate post-1.1 Workshop signal before building further |
| US-069 Allergies | Keep, default-OFF | Cut — hidden RNG punishment is complaint archetype | **Downgrade** — move to stretch, re-evaluate post-1.2 |
| Phase 7 UI | Keep both | Cut — B2B tooling | **Split**: US-070 Cookbook UI keeps (3 d, folds into Phase 3). US-071 Meal Planner → stretch |

## Codex-only orthogonal flags

- **Localization gap**: 30–40% audience cut from EN-only. → Phase 11 US-110.
- **Bug-triage SLA**: Redux had "settings freeze" complaints for months. → Phase 11 US-112.
- **Workshop comment harvest cadence** — need regular, not one-shot. → Phase 11 US-112.
- **ID collision policy** — 38 reserved US-### live in docs, not master. → Convention update in ROADMAP.md.
- **Settings will exceed 100 knobs** post-Phase-7 — we become what we critiqued Redux for. → O-5 settings reclaim + discipline in each phase.
- **Segments that uninstall**:
  - Perf-sensitive large-colony (hot-path stacking).
  - Narrative-pure (allergies + hediffs = medical sim).
  - Modpack curators (R-002 API break ripples).

## Resolved changes to the roadmap

- Added phases: `phase0.5-compat.md`, `phase8-storyteller.md` (trimmed to 2 stories), `phase9-dlc-hooks.md`, `phase10-hedonic.md` (stretch), `phase11-release-polish.md`.
- Added opportunistic wins doc: `opportunistic-wins.md`.
- `ROADMAP.md` updated with release cadence, blocker callouts, revised sequence.
- `architecture.md` updated: R-001 repositioned mid-roadmap (before Phase 5), R-002 internal-first, public API marker deferred.
- `phase3-signatures.md` annotated w/ BLOCKER-1 on US-046.
- `phase4-dinnertime.md` split into 4a / 4b w/ BLOCKER-2 on US-051.
- `phase6-themed-extras.md` moves US-069 Allergies to stretch.
- `phase7-ui-features.md` moves US-071 Meal Planner to stretch.

## Single most important recommendation (Codex + arch aligned)

Freeze ambition at Phase 3 for the first public release. Land R-001 before Phase 5. Bookend with Phase 0.5 (compat) + Phase 11 (polish). Everything past Phase 4b is speculative until Workshop comment signal dictates direction.
