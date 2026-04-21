# Phase 7 — Power-User UI

## Goal

Two workflow features that turn VarietyMatters' tracked data into colony-management tooling. Appeals to optimizer players.

## Stories

### US-070 — Cookbook UI (cook inspect tab) [absorbed into Phase 3 / Release 1.1]

**Status**: Per revised roadmap, US-070 ships with Phase 3 (Release 1.1) — not deferred to a standalone Phase 7. Rationale: local audit confirms `VarietyCookingRecord.recentByPawn` already tracks per-chef data; Cookbook UI is 3 d on top of Phase 3 infrastructure.

**Effort**: 3 d (depends on Phase 3 US-046)
**Scope**:
- New inspect tab "Recipes" on cook pawns (pawns w/ Cooking skill enabled).
- Shows list of mealDefs w/ per-cook metrics:
  - Cook count (from `CookMasteryRecord`).
  - Mastery progress bar (X / threshold, green when mastered).
  - Last-cooked tick ("12 hours ago").
  - Badge "🍳 Mastered" for crossed-threshold entries.
- Sortable: by count desc, by mealDef name, by last-cooked.
- Modded recipes show up automatically via `RecipeDef` enumeration.

**Acceptance**:
- Cook pawn → Recipes tab visible; non-cook pawns → hidden.
- Sort works on all three axes.
- Mastered recipes sorted-to-top by default w/ badge.
- Save/load preserves; mod add/remove resilient.

### US-071 — Meal planner (stockpile directive) [STRETCH — downgraded post-review]

**Status**: Moved to stretch per review round. Codex flagged: "B2B tooling" — power-user optimizer features on a mood mod are niche. Vanilla bill system is hairy; auto-queue UX is error-prone.

Re-evaluate post-1.1. If Workshop comments ask for favourite-aware bills, ship the manual-suggest MVP. Auto-queue is permanently on ice unless demand is explicit.

**Effort**: 4 d (depends on US-070 and F-007 favourites)
**Scope**:
- New stockpile attribute "Prefer favourites of:" → select list of colonists.
- Bills-per-workbench directive "Auto-queue from planner" → reads stockpile-attached pawn list, queues appropriate bills:
  - E.g. if Alice's favourite meat = `Cow_Meat` and stockpile tagged w/ Alice → stockpile filters for meals containing cow meat; adjacent workbench auto-adds "Cook Fine Meal x10 while < 5 stock" bills using that ingredient.
- New "Meal Planner" window accessible from Architect menu:
  - Per-colonist row: favourites + saturation + "at-risk" variety flag.
  - Bill-suggestion button: "Queue 10 cow-meat meals for Alice".
- Setting: `mealPlannerEnabled` (default ON; auto-queue behavior default OFF — manual suggestions only; auto-queue is the opt-in).

**Acceptance**:
- Tag stockpile w/ "Prefer Alice's favourites" → filter reflects Alice's current favourite slots.
- Meal Planner window opens from Architect submenu.
- Bill-suggestion button creates draft bill at nearest valid workbench.
- Auto-queue mode creates bills automatically; disable = manual only.
- Removing pawn from colony → stockpile tag clears gracefully.
- Modded food slots (e.g. RimCuisine-added favourite) supported.

## Risks

- **US-071 bill integration** — vanilla bill system is hairy (RecipeDef + IngredientFilter interaction). Timebox bill-auto-queue to stretch; ship manual-suggest first, expand if demand.
- **Planner window** risks duplicating Architect-tab real estate. Consider minimalist approach — single-window popup rather than persistent panel.
- **Cookbook UI** tab real-estate: Bio tab already has Phase 3 US-048 row; ensure Recipes tab doesn't duplicate.

## Notes

- Phase 7 depends on Phases 3 + F-007 data — don't start before those are stable.
- Consider Gastronomy compat: waiter-remembered-favourites ties nicely into meal planner ecosystem. Future phase.
- Meal planner is the most speculative feature — validate demand via Workshop comments before committing full auto-queue.
- MVP for US-071: just the window + manual bill suggestion. Auto-queue is v2.
