# Phase 8 — Storyteller / Social (trimmed)

## Goal

Add storyteller-surface integration so variety isn't invisible. **Trimmed to 2 stories per Codex review warning**: thought-spam risk on social events + letter fatigue if we add 7 stories at once. Ship 2; iterate based on Workshop feedback.

Full brainstorm list of 15 candidates archived in §Future candidates below.

## Stories

### US-080 — Share Meal interaction
**Effort**: S (~2 d)
**Scope**:
- New `InteractionDef VM_ShareMeal` + `InteractionWorker_ShareMeal`.
- Trigger condition: two pawns eat the same `mealDef` within 90 ticks at the same table, neither currently on a hostile stance.
- Effect: minor opinion bump (+2, decaying) scaled by meal's variety score (higher-variety meals → larger bump).
- Respect §12 `Disabled` gate — opted-out pawns don't initiate.
- Cooldown: once per pawn pair per day (prevent spam).
- Setting: `shareMealInteractionEnabled` (default ON).

**Acceptance**:
- Two colonists eating same Fine Meal at table → interaction fires, +opinion both sides.
- Ascetic pawn → no interaction.
- Same pair dining again within 24h → no re-fire (cooldown).
- Hostile stance (combat / downed) → suppressed.
- Performance: 0 alloc in the trigger check hot path.

### US-081 — Menu Fatigue crisis incident
**Effort**: S (~2 d)
**Scope**:
- New `IncidentDef VM_MenuFatigue` + `GameConditionDef VM_MenuFatigueCondition`.
- Trigger condition: ≥3 colonists at variety score < 0.3 for continuous 5 days.
- Effect: 2-day game condition → variety decay ×2, letter on fire.
- Resolution: cooking any meal with ≥3 distinct ingredients while condition active cancels it (storyteller-surfaces the fix).
- Incident firing gated by IncidentCategoryDef `Misc` with standard weight — storyteller picks when colony is healthy and wants a nudge.
- Setting: `menuFatigueIncidentEnabled` (default ON, fold out of default incident list).

**Acceptance**:
- Low-variety colony ≥3 pawns → incident eligible; storyteller fires within 2 in-game days.
- Cooking a varied meal during condition → cancels condition w/ resolution letter.
- Disabled setting → incident never fires, condition never applies.
- No stacking — only one `VM_MenuFatigueCondition` active at a time.

## Risks

- **Thought/letter spam.** `Share Meal` could spam on large colonies (N^2 pairs eating). Mitigation: per-pair cooldown + per-map daily fire cap (e.g. max 10/day).
- **Incident doesn't fire** if storyteller weights overweight existing Misc incidents. Tune weight conservatively; flag if telemetry shows no fires.
- **Resolution ambiguity.** "Cooking a varied meal" — what counts? Tie to existing variety-score threshold (≥0.6 at cook time).

## Notes

- Codex: "high thought-spam risk, keep exactly one social hook + one story event." Exactly what we ship.
- Arch review: Phase 8 after Phase 5, not before. Reflects in revised sequence.
- Rest of the 15-candidate list archived below. **Do not implement without Workshop demand signal** — codex warns against systems that "sound good in docs."

## Future candidates (ice-box)

All from the storyteller brainstorm agent. Revisit per Workshop signal. Do not build speculatively.

- Travelling Chef visitor quest
- Catering Contract delivery quest
- Royal Banquet Request (Royalty)
- Harvest Festival game-phase incident
- Food Critic visitor
- Spoiled Larder incident
- Recipe Swap interaction
- Food Gift interaction
- Dining Hall situational mood (`Thought_Situational`)
- Same-meal-as-rival `Thought_SituationalSocial`
- Caravan cuisine shock incident
- Cooking Competition two-faction quest
- Insect Jelly Black Market orbital trader
