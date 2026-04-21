# Phase 6 — Themed Extras

## Goal

Three opinionated flavour features that tie existing RimWorld systems into VarietyMatters. None are large; bundle for a single release.

## Stories

### US-067 — Cannibal meme interaction
**Effort**: 1.5 d
**Scope**:
- If pawn's ideo has `Cannibal` meme OR precept `Cannibal: Preferred/Required`:
  - Human meat counts as its own "meta-bucket" (no variety repetition penalty for eating human).
  - Human meat ingredient doubles as variety credit (comfort food).
  - Favourite crystallization allowed for `Meat_Human` def (normally filtered by `DesperateOnlyForHumanlikes` pool — override for cannibals).
- Non-cannibal pawns: existing behavior preserved (human meat grants neutral/negative variety depending on precept).
- Setting: `cannibalMemeSupport` (default ON, gated on `ModsConfig.IdeologyActive`).

**Acceptance**:
- Cannibal pawn eats same human meat meal 10 days → no variety mood penalty.
- Cannibal pawn eats varied human meat ingredients (leg / heart / organ) → credits stack normally.
- Human meat def surfaces as crystallizable favourite for cannibals only.
- Non-cannibal pawn eating human → existing `AteHumanlikeMeatDirect` fires as vanilla.

### US-068 — Mechanoid / non-eater indifference
**Effort**: 0.5 d
**Scope**:
- Exempt from variety need + favourites + diet-health + sensitivity pipeline:
  - Mechanoids (`RaceProps.IsMechanoid`).
  - Non-eaters (`RaceProps.EatsFood == false`).
  - Any pawn w/ `VM_NoVarietyNeed` gene (already implemented).
- Add early-exit gate in `Need_FoodVariety.Disabled` chain (§12 — preserve ordering-first principle).
- Add early-exit in `FavouriteFoodRecord.OnMealIngested` gate 1 (§13).
- Phase 5 US-058 intake tracker also skips.

**Acceptance**:
- Mechanoid pawn (captured / player-built) → no variety need in UI, no favourite data materialized.
- Non-eater modded race → no regression, no tracker rows.
- Performance: early-exit saves hot-path work.
- Feature advertisable: "zero-overhead support for mechanoid colonies".

### US-069 — Procedural allergies [STRETCH — downgraded post-review]

**Status**: Moved to stretch per review round. Codex flagged: "hidden RNG punishment on pawn gen is the complaint archetype (see Psychology's psychic sensitivity blowback)." Default-OFF mitigates but doesn't eliminate — even opted-in players report mid-colony allergy reveal feels bad.

Re-evaluate post-1.2 based on Workshop comment signal. If players ask for allergies, ship the spec below. Otherwise, cut permanently.

**Effort**: 2 d
**Scope**:
- Each pawn at gen: 5% chance to have hidden allergen (one random ingredient def from safe pool, same pool as favourites seed).
- Allergen ingredient stored on `PawnFavData` (or new `PalateProfile` — see architecture.md R-001): `string allergenDefName`.
- First consumption → triggers `VM_AllergicReaction` hediff (mild: rash, moderate: vomiting, severe: anaphylaxis ~15% of allergens).
- After first trigger: hediff `VM_KnownAllergy_X` permanent flag (visible in Health tab).
- AI avoids known allergen in food selection (Harmony on `FoodUtility.WillEat` post-reveal).
- Cook AI avoids allergen for known-allergy colonists when preparing bills-per-pawn (stretch, may require bill UI work — defer).
- Setting: `allergiesEnabled` (default OFF — opt-in, story-rich but disruptive).

**Acceptance**:
- 5% of gen-rolled pawns have hidden allergen.
- First consumption triggers hediff + letter "Alice is allergic to corn".
- Post-reveal: AI prefers non-allergen food.
- Save/load preserves allergen + known-state.
- `allergiesEnabled = false` → new pawns don't get allergens; existing allergen states frozen (don't trigger).

## Risks

- **US-069** feels punishing on small colonies (1/20 pawns has allergy — 5%). Validate 5% default w/ small-colony playtest; may drop to 3%.
- **US-068** mechanoid exempt may already be covered by vanilla `Need.ShowOnNeedList` or `CompProperties_Needs` filters. Audit before implementing to avoid duplicate work.
- **US-067** cannibal override on human-meat favourite crystallization — verify pool filter skip doesn't leak human meat into non-cannibal favourite seeds via cached pool. Pool cache must key on `(bucket, cannibalPawn)`.

## Notes

- Allergies (US-069) is a strong story hook. Consider promoting to Phase 1 if a playtest reveals it's "the feature" — but default-OFF keeps discovery organic.
- Cannibal meme support is essentially a compat patch; advertise it explicitly on Workshop page.
- Mechanoid support is the cheapest "compat story" — zero gameplay changes, just correct early-exit. Worth documenting as feature.
- All three bundle into one commit cycle — share docs + testing pass.
