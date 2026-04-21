# Phase 4 — Dinner Time Absorption

## Goal

Absorb best-of-breed features from the abandoned **Variety Matters Dinner Time** mod (last update Dec 2021, no 1.4/1.5/1.6). Market as replacement: "Subscribe to replace an abandoned mod with one maintained."

Source reference: https://steamcommunity.com/sharedfiles/filedetails/?id=2326581524

## Split post-review

Arch review: one-phase-for-a-whole-mod is a trap (7 stories → half-implemented). Codex review: over-scoped. Resolution: **split into 4a (parity) + 4b (Phase 3 synergies)**. Ship together in Release 1.2.

- **Phase 4a — Parity**: US-051, US-052, US-054, US-055, US-056. The "absorb the mod" bundle. Self-contained, no Phase 3 dependency.
- **Phase 4b — Synergies**: US-053, US-057. Requires Phase 3 (US-046 mastery) stable first.

## [BLOCKER-2] — RESOLVED (decompile verified 2026-04-20)

Tech review flag: US-051 vague on job prioritizer hook. **Verified via decompiler MCP: hook chain concretized.**

**Hook site confirmed**: `RimWorld.JobGiver_GetFood.TryGiveJob(Pawn)` exists in 1.6. Standard flow: hunger-gate → `FoodUtility.TryFindBestFoodSourceFor` → `JobMaker.MakeJob(JobDefOf.Ingest, ...)`. Patchable via Harmony prefix to inject meal-time delay.

**Additional finding**: `TimeAssignmentDefOf` in vanilla 1.6 has **only 5 slots**: `Anything`, `Work`, `Sleep`, `Joy`, `Meditate`. **No `Meal` slot exists** — we ship our own `TimeAssignmentDef VM_Meal` via XML.

**Runtime check points**:
- `Pawn_TimetableTracker.CurrentAssignment` — current slot (property).
- `Pawn_TimetableTracker.GetAssignment(int hour)` — lookahead (method).
- `Pawn_TimetableTracker.times : List<TimeAssignmentDef>` — per-hour assignment storage.

**US-051 design concretized** (effort unchanged, ~2–3 d):

1. **XML**: define `TimeAssignmentDef VM_Meal` (new color, icon, sort key).
2. **UI patch**: extend `PawnColumnWorker_Timetable` column rendering to show the new slot.
3. **Pre-meal delay**: Harmony prefix on `JobGiver_GetFood.TryGiveJob` — if `!starving && (currentAssignment == VM_Meal || nextHour within 2h == VM_Meal)` → return `null` (delay food seek).
4. **During VM_Meal recreation bias**: use `JobGiver_EatInGatheringArea` as template; patch recreation weight for VM_Meal slot to prefer dining-room social interactions.

The concretization adds no new effort — just removes ambiguity about hook identity.

## Stories

Each story is tagged **[4a]** (parity, no Phase 3 dep) or **[4b]** (synergy, Phase 3 dep).

### US-051 [4a] — Meal Time schedule slot (DT-L1)
**Effort**: 2–3 d ★ flagship
**Scope**:
- Add `Timetable` entry "Meal" via new `TimeAssignmentDef VM_Meal`.
- Pre-meal: non-starving pawns delay eating until slot starts (so the colony herds into shared dinner).
- During slot: non-eaters prefer "gluttonous recreation + social" (no TV). Hook = Harmony prefix on `JobGiver_GetFood.TryGiveJob(Pawn)` (verified in 1.6 — see [BLOCKER-2] resolution above). Recreation bias via `JobGiver_EatInGatheringArea` pattern.
- Toggleable via `mealTimeEnabled` setting (default ON; new save starts w/ default schedule including one Meal slot).
- Save safety: existing saves WITHOUT meal-time config get no new schedule entries; new save templates include one default meal slot (19:00–20:00).

**Acceptance**:
- Adding "Meal" to pawn schedule → pawn delays eating up to 2 h pre-slot unless starving.
- During slot, idle pawn favors dining-room social over other recreation.
- Removing mod mid-save (warned): schedules gracefully degrade (unknown assignments → Anything).
- Disable via setting: vanilla food-seeking resumes, timetable entry remains harmless.

### US-052 [4a] — Freshness stacking thoughts (DT-L2)
**Effort**: 1–2 d
**Scope**:
- Three stacking mood thoughts on meal consumption:
  - `VM_FreshlyCooked` +1 (meal age < warm threshold)
  - `VM_Leftovers` −1 (meal age past warm threshold)
  - `VM_FrozenMeal` −2 (meal was stored frozen)
- Cooling timers (all tunable):
  - Warm → normal: default 20 h.
  - Normal → leftovers: default 40 h.
  - Refrigerated meals cool 2× faster.
  - Frozen meals → instant leftover state on eat.
- New comp `CompVMMealAge` on meals (writes birth tick + first-freeze tick). Lightweight.
- **Signature synergy** (US-046): master-cooked meals get 1.5× cooling time — stays fresh longer, narrative payoff.

**Acceptance**:
- Just-cooked meal eaten → +1 thought fires.
- Meal stored 30 h at room temp → −1 leftover.
- Meal frozen any time → −2 on eat.
- Master-cooked meal retains "fresh" window 1.5× longer.
- Settings section tunes thresholds + mood magnitudes.

### US-053 [4b] — Quality Cooking failures + signature immunity (DT-L4)
**Effort**: 3 d ★ killer synergy w/ Phase 3
**Scope**:
- Skill-gated random poor outcomes on meal production.
- Failure types (6): undercooked, burnt, too-small (reduced nutrition), too-large (positive mood + `OverAte` hediff), weird-flavour, under/overseasoned.
- Probability curves (DT's):
  - Simple: 25% (skill 0) → 0% (skill 7+)
  - Fine: 24% (skill 6) → 0% (skill 12+)
  - Lavish: 45% (skill 8) → 0% (skill 18+)
- **Master cook w/ mastered recipe → 0% failure regardless of skill** (Phase 3 synergy).
- Hidden until consumed (like food poisoning); stacks don't contaminate siblings (per-meal flag).
- New thoughts per failure type, short-duration, non-stacking w/ `AteLavishMeal`.
- Setting: `qualityCookingEnabled` (default ON), `qualityCookingMasterImmunity` (default ON).

**Acceptance**:
- Skill-0 cook on Simple meal → ~25% produce a flagged meal.
- Skill-7 cook on Simple → 0% flagged.
- Master cook on mastered recipe → 0% even at low skill (sanity-test edge: cook LEVELED DOWN to mastered recipe). Acceptable.
- Failure thought visible on consumption, appropriate duration.
- Disable setting → no failures generated, existing flagged meals safe on consumption (no-op).

### US-054 [4a] — Dining room preference (DT-L3)
**Effort**: 1 d
**Scope**:
- Food-find: meals in rooms of type `Dining` / `Hospital` / `Prison` bypass distance penalty in `FoodUtility.FoodOptimality`.
- Inventory food and caravans unchanged.
- Royalty optional: dining-room impressiveness tier multiplies variety mood (Mundane 1.0 → Noble 1.3). Fold into this story.

**Acceptance**:
- Meal in dining room beats farther meal outside, ceteris paribus.
- Inventory eating unchanged.
- Royalty ON + impressive dining room → variety mood bonus visible in thought stack.

### US-055 [4a] — Lavish fading buff (DT-L5)
**Effort**: 0.5 d
**Scope**:
- Optional replacement for vanilla `AteLavishMeal` thought.
- Fading curve: +12 → +8 → +4 over 2 days.
- Setting `revisedLavishEnabled` (default ON).
- Implementation: subclass `Thought_Memory`, override `MoodOffset()` computing based on `Age`.

**Acceptance**:
- Eating lavish fires thought at +12.
- After 24 h: +8 visible in mood inspector.
- After 36 h: +4 visible.
- Disable setting → vanilla thought used.

### US-056 [4a] — Travel food category + exclusions (DT-L6)
**Effort**: 1 d
**Scope**:
- `FoodCategory` tagging (internal): `Normal` / `NoTableRequired` / `TravelFood`.
- `TravelFood` default-tagged: `Pemmican`, `Packaged_Survival_Meal`.
- `NoTableRequired`: skips `AteWithoutTable` penalty. Slight inventory packing preference bump.
- `TravelFood`: same + strongly packed for caravans.
- Exclusion-list integration (Phase 1 US-039): exclusion UI shows foodCategory column.
- Setting: per-def override (inline UI row to flip category).

**Acceptance**:
- Pemmican eaten without table → no penalty.
- Caravan packing prefers TravelFood-tagged items over normal meals.
- Per-def override persists save/load.
- Modded food defaults to `Normal` unless category override set.

### US-057 [4b] — Communal dinner + cook pride (new)
**Effort**: 2 d (depends on US-051 + Phase 3 US-047)
**Scope**:
- **Communal dinner thought**: 3+ pawns eating at same table during meal-time slot → shared `VM_CommunalDinner` +2 mood, 1 day. Stacks w/ individual variety mood.
- **Cook pride**: when a cook's beloved / family member eats the cook's signature → cook gets `VM_ServedBeloved` +1 mood 12 h. Rewards chef social currency.
- Setting: `communalDinnerEnabled` (default ON).

**Acceptance**:
- 3+ pawns dining together during meal slot → thought fires on each.
- Cook pride fires for cook on beloved-consumption of signature (not regular meals).
- Kill-switch preserves existing thoughts, stops new ones.

## Risks

- **Save migration**: US-051 adds Timetable data; US-053 adds hidden meal comp. Existing saves must default-safely on load (null guards, empty comp state).
- **Mod incompat**: Smarter Food Selection / Real Dining / Room Food overlap. Degrade gracefully — detect presence, defer to them where overlap, document precedence.
- **Gastronomy** — its waiter bypass skips our DT-L3. Cross-ref Phase 4 ↔ Gastronomy compat doc (future).
- **Cooking skill curve** — 25% failure at skill 0 is punishing. Validate w/ Lenient difficulty preset (halve rates).

## Notes

- Phase 4 is essentially a mod absorption. Consider splitting commits by sub-feature for bisect-ability.
- Marketing pitch: "VarietyMatters now absorbs Variety Matters Dinner Time — scheduled mealtimes, freshness decay, quality cooking, dining-room preference, all updated for 1.6 and integrated."
- Signature-dish immunity (US-053) is the killer synergy — advertise it as progression arc.
- DT-L7 (spoiling pref) and DT-L8 (revised AteWithoutTable) considered but deferred — our existing `preferSpoilingFood` covers most of L7; L8 is default-off incompat risk.
