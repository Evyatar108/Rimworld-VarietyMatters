# Cross-Cutting Architecture Refactors

Apply at scheduled points in the release cadence (NOT purely opportunistic — both R-001 and R-002 have dependency constraints that force sequencing). Track as R-### (refactor) tickets distinct from F-### / US-###.

## Summary

| # | Refactor | Target release | Why now |
|---|---|---|---|
| R-001 | `PalateProfile` unification | **1.1 (pre-Phase-5)** | Phases 5/6/9 each add Scribed fields; deferring = debt bomb |
| R-002 | `EventBus` (internal events) | **1.1 (w/ Phase 3)** | Fire-sites from mastery/ingest/crystallization need landing |

---

## R-001 — `PalateProfile` unification

### Scheduling (revised post-review)

**Lands: Release 1.1, after Phase 3, before Phase 5.**

Original roadmap scheduled this at the end. Review agents (arch + codex) independently flagged: Phases 5/6/9 each add new Scribed fields to `FavouriteFoodRecord` (bucket counter, allergen, hediff state). Deferring the consolidation = each phase bakes in stable Scribe labels that R-001 must later preserve → migration surface grows linearly.

Landing R-001 mid-roadmap costs one focused week + save-migration playtest now, vs. an unbounded refactor later touching 4+ schema generations.

### Motivation

Post-Phase-3, per-pawn palate data will be scattered across:

- `FavouriteFoodRecord.PawnFavData` — favourite slots, saturation, progress (F-007, US-037, US-038)
- Phase 5 `NutrientIntakeRecord` **never created — lands into `PalateProfile` directly**
- Phase 6 US-069 allergen **never added to `PawnFavData` — lands into `PalateProfile`**
- Future: signature affinities, flavour profile, nostalgia anchors

Each has its own Scribe layout, lifecycle cleanup. Consolidating early means later features write into a single stable schema.

### Design

```csharp
class PalateProfileRecord : GameComponent {
  Dictionary<Pawn, PalateProfile> _data;
}

class PalateProfile : IExposable {
  // favourites (F-007) — existing, migrated in from PawnFavData
  SlotState meatSlot, vegSlot, wildSlot;
  Dictionary<ThingDef, CandidateProgress> progress;
  bool lastMealWasIngestedAsFavourite;

  // Phase 5 bucket intake — lands here natively
  Queue<BucketCredit> bucketCredits;

  // Phase 6 US-069 allergen — lands here natively
  string allergenDefName;
  bool allergenRevealed;

  // future: signature affinities, flavour profile, nostalgia anchors
}
```

### Migration strategy

**CRITICAL per §8**: existing saves must load cleanly.

1. Rename `FavouriteFoodRecord` class → `PalateProfileRecord` via `Scribe_References`-compatible alias. Keep the old class as a thin `[Obsolete]` wrapper delegating to new during one release cycle.
2. `PawnFavData` → `PalateProfile` via same alias pattern. Scribe labels for existing fields (`meatSlot`, `vegSlot`, `wildSlot`, etc.) **unchanged**.
3. New Scribe labels (`bucketCredits`, `allergenDefName`, etc.) default-empty on old-save load.
4. Dedicated save-migration playtest on ≥10 existing saves before Release 1.1 ships. Rollback plan: revert R-001 commit, ship 1.1 without consolidation (accept the debt).

### Acceptance

- Pre-R-001 save loaded in post-R-001 build → favourites + saturation + progress intact.
- Post-R-001 save → all features write to `PalateProfile` directly, no parallel components.
- Lifecycle cleanup (§9) consolidated to one `RemoveTrackedPawn` call on the new component.

### Risks

- **Bad migration breaks every subscriber's save.** Highest-risk single change in the roadmap.
- Mitigation: playtest discipline, rollback plan, version-bump letter on first load ("VarietyMatters data migrated — save OK").

---

## R-002 — EventBus (internal events)

### Scheduling (revised post-review)

**Lands: Release 1.1, alongside Phase 3 US-046/047.**

**Internal fire-and-forget only for Release 1.1.** Codex review flagged: a `public` event marker is a forever compat contract. Ship internal fan-out first; mark public API only after 1.2 has road-tested signatures.

### Motivation

Local code audit: no public events — ingest fan-out is hard-coded in `VarietyRecord:86`. Blocks Phase 5 (diet-health cross-ref from favourite intake), Phase 8 (Menu Fatigue incident needs variety threshold watcher), Phase 9 (DLC hediff triggers).

### Design

```csharp
internal static class VarietyMattersEvents {
  internal static event Action<Pawn, EatenFoodSource> OnIngredientEaten;
  internal static event Action<Pawn, ThingDef, Bucket> OnFavouriteCrystallized;
  internal static event Action<Pawn, ThingDef> OnMasteryGained;
  // additions TBD per feature phase
}
```

- **Internal visibility** for 1.1. Public promotion deferred.
- Fire-and-forget — subscribers must not mutate firing-source state.
- Exception-swallowed — one bad subscriber doesn't break the fire.
- No-alloc in hot paths per §1.

### Fire sites (1.1 scope)

- `VarietyRecord.UpdateVarietyRecord` post `AddFoodSource` → `OnIngredientEaten`.
- `FavouriteFoodRecord.CreditIngredient` on crystallization threshold cross → `OnFavouriteCrystallized`.
- `CookMasteryRecord` on mastery count cross → `OnMasteryGained`.

### Consumers (1.1 scope)

- Phase 3 US-047 listens to `OnMasteryGained` to fire mastery letter.
- Future phases subscribe without touching fire-sites.

### Public API promotion (deferred)

After 1.2 + Phase 4 ships + signatures are stable:

1. Add `[VarietyMattersPublicApi]` marker attribute.
2. Promote chosen events to `public`.
3. Add an example consumer mod skeleton in `Mods/Examples/`.
4. Semantic-versioning commitment for promoted events only.

### Acceptance

- Phase 3 US-047 mastery letter fires via event subscription, not direct call.
- Event arg types are value types or existing refs — zero alloc per fire.
- No subscriber throwing an exception breaks the originating code path.

### Risks

- **Accidentally promoting to public** via reflection or a modder patching our private field. Not a real blocker — any mod doing that owns the consequence.
- **Subscriber proliferation** slows hot paths. Mitigation: keep event count small (3 at launch); add new events conservatively.

---

## Refactor backlog (future consideration, not ticketed)

- **Settings structure tightening** — currently ~40 knobs; projected >100 post-Phase-7. Collapse into preset tiers + advanced fold-out. O-5 provides initial space reclaim; long-term consolidation needed.
- **Async `ReCountAll`** — long-running post-load migration blocks main thread. Offloading is tricky (ThingDef deref in background) but doable.
- **Shared pool-cache module** — `FavouriteFoodRecord.GetPool` + future allergen pool share filtering logic. Extract once patterns crystallize.
- **Debouncer module reuse** — `Mod_VarietyMatters.cs` debouncer (per `DEBOUNCED_SETTINGS_THREAD_SAFETY_BUG.md`). Ensure all settings writes use it.
