# VarietyMatters — project invariants

Short reference for recurring conventions. Read before touching cooking / variety code.

## 1. Scratch-field ethos

Per-call allocation is a perf regression. New scratch goes in the catalog at the top of the owning class:

```csharp
private static readonly Dictionary<T, U> _nameScratch = new Dictionary<T, U>();
```

`.Clear()` at the entry of the consumer method (or at the top of `BestVariety_AllowMix` when shared across a bill). Name `_fooScratch` so greps find all of them. Document shared scratch in the catalog comment — if multiple methods touch one field, list them.

Example catalog: `Source/VarietyCooking.cs:15-27`.

## 2. Commit style

Bracketed ticket IDs, matching recent history:

- `fix: [F-###] - short description` — bug fixes
- `feat: [US-###] - short description` — feature work (user stories)
- `chore: ...` / `docs: ...` — no ticket

Check `git log --oneline | grep -E "\[F-00"` before picking a new F-### — IDs can be reused across branches but not across master. Latest: F-006 (`8cd12cf`).

## 3. Soft-cap pattern

When a setting acts as a *target* over variable structural input, use `max(setting, structural_minimum)` not raw `setting`. E.g. `effectiveBudget = max(numIngredients, variableSlotCount)` so modded 3+-slot recipes never starve a slot.

Pure `setting` creates footguns with modded inputs. Add a comment explaining the soft-cap direction.

## 4. `BestVariety_AllowMix` gate chain

The prefix at `Source/VarietyCooking.cs:53` has four sequential gates before the distribution loop. Any change to cooking behavior must respect all four:

1. `foodTrackingType == ByMealNames || !preferVariety` → vanilla path
2. Recipe gate: non-cooking skill OR no `ingestible` OR `specialProducts != null` → vanilla path
3. `cookingRecord == null` → vanilla path
4. Reaches distribution loop → E' adaptive water-fill runs

If your change should also skip under gate N, mirror the gate; don't invent a new one.

## 5. `TryFillIngredientSlotWithCap` pass semantics

`Source/VarietyCooking.cs:TryFillIngredientSlotWithCap` runs up to three passes per slot:

- **Pass 1** (top-K cap): collects first `slotMaxDefs` distinct defs in sort order, jitter-normalizes per-def caps summing to `slotBaseCount`, fills from stacks. Enforces `≤ slotMaxDefs` distinct defs.
- **Pass 2a** (top-K residual): drains residual nutrition restricted to Pass-1's top-K set. Preserves the cap.
- **Pass 2b** (unrestricted): relaxes cap only when top-K stacks can't satisfy the slot. Grabs defs outside top-K → may overshoot `slotMaxDefs` by ≤1 per slot. Safety net for stack-depth / cross-slot contention; near-dead under E' but not removable without a quantity-aware pre-scan.

When adding a new cap concern, thread it through Pass 1 + Pass 2a identically; leave Pass 2b as the escape hatch.

## Deploy / test

- Build: `dotnet build Source/VarietyMatters.csproj -c Debug`
- Deploy to Steam: invoke the `rimworld-tools:deploy` skill
- In-game dev verify: `Prefs.DevMode` on, inspect `CompIngredients.ingredients.Count` on produced meal (via dev inspector). Do NOT add permanent `Log.Message` for feature validation — remove after confirming.

## Do NOT

- Add per-call `new T[]` / `new List<T>()` in cooking hot path (US-004 no-alloc ethos).
- Add permanent `Prefs.DevMode`-gated logs that fire per-meal or per-pawn-death (too noisy; see `9ad6656`).
- Touch `Patch_IngredientStacking.cs numIngredients` when fixing cooking-cap issues — that's the stack-cap, different concern.
