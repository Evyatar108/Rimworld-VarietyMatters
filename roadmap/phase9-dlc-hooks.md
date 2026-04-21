# Phase 9 — DLC-Native Hooks

## Goal

Per Codex review: **Biotech-only first wave** (70% audience). Anomaly (25%) + Odyssey (<15%) gated on Workshop demand signal post-Release 1.2. Do not build speculatively for low-adoption DLC.

## Stories — Biotech first wave

### US-095 — Twisted-meat favourite lockout (Anomaly seed)
**Effort**: S (~1.5 d)
**Scope**:
- Prevent twisted-meat ingredient (`ThingDef: TwistedMeat`) from crystallizing into a meat favourite.
- Exception: pawn is Inhumanized OR Cannibal (ideology) OR Ghoul (Anomaly). Override allows crystallization; grants +6 "Dark craving satisfied" mood thought on match (runtime-tunable per §13 pattern).
- Gated by `ModsConfig.AnomalyActive` — stub-safe without DLC.
- Setting: `twistedMeatLockoutEnabled` (default ON when Anomaly active).

**Acceptance**:
- Non-Inhumanized pawn eating twisted-meat meal 10× → no meat-slot favourite crystallization; existing `AteTwistedMeat` thought fires as vanilla.
- Inhumanized pawn eating 10× → crystallizes; next twisted-meat meal fires +6 mood.
- Anomaly off / missing → no regression.

### US-097 — Harbinger-tree monotony hediff (Anomaly)
**Effort**: M (~2 d)
**Scope**:
- Tag meat sourced from Harbinger tree corpse-conversion at harvest tick: `CompHarbingerSourced` (lightweight flag).
- Track per-pawn rolling 7-day harbinger-meat ratio via `FavouriteFoodRecord` intake counter extension.
- Threshold: ≥50% of last-7d meat intake harbinger-tagged → `VM_UniformFleshFatigue` hediff (minor work-speed ↓).
- Blocks twisted-meat crystallization while hediff active (prevent 200-meat/day trivialization).
- Gated by `ModsConfig.AnomalyActive`.

**Acceptance**:
- Colony running harbinger factory → hediff accumulates on meat-eaters after 7 days.
- Mix in normal meat → ratio drops, hediff regresses.
- No Anomaly → no regression.

### US-096 — Shambler-meat revulsion (Anomaly)
**Effort**: S (~1 d)
**Scope**:
- Tag meat from shambler corpses at butcher tick (deadlife-tainted).
- Eating → `VM_AteReanimatedFlesh` thought (−6 mood, 2 days, stack limit 1).
- Immune: Inhumanized only.
- Gated by `ModsConfig.AnomalyActive`.

**Acceptance**:
- Non-Inhumanized eats shambler meal → −6 thought.
- Inhumanized eats → no thought.
- No stacking beyond 1.

## Stories — gated on Workshop signal

### Odyssey stories — defer until signal
Not building speculatively. Revisit after Release 1.2 if Workshop comments request.

Candidates archived in [§Odyssey ice-box](#odyssey-ice-box):
- US-090 Voyage monotony (gravship no-landing decay)
- US-091 Fresh-catch bonus
- US-092 Nutrifungus cliff
- US-093 Exotic fauna first-bite
- US-094 Biome-specific wild favourites

### Additional Anomaly — defer
- US-098 Ghoul 1-slot favourite (work-speed tied)
- Monolith-dread appetite decay
- Metalhorror hidden-appetite diagnostic

## Risks

- **DLC gating fragility.** Missing `ModsConfig.AnomalyActive` null-check = null ref on non-Anomaly saves. Defensive check per story entry point.
- **Harbinger tagging** via Harmony on Harbinger-tree conversion — verify hook stability across Anomaly updates.
- **Inhumanized check** — `HediffDefOf.Inhumanized` ref. Verify present in 1.6 Anomaly.

## Notes

- Codex explicitly flagged Anomaly/Odyssey as wrong tier for roadmap priority. We keep **3 quick-win Anomaly hooks** because they're S-effort and address real gameplay holes (harbinger trivialization, free shambler food). Odyssey deferred.
- Biotech-specific genes already covered by Phase 2 US-044 (Supertaster / BlandPalate) — no Phase 9 addition needed.
- Royalty-specific features (title banquet quotas, dining-room impressiveness tier) already covered by Phase 4 US-054.

## Odyssey ice-box

Revisit if post-Release 1.2 Workshop comments mention Odyssey + food.

- **US-090 Voyage monotony** — gravship sealed + no-landing N ticks → decay ×2 + "Shipboard diet" thought.
- **US-091 Fresh-catch bonus** — Odyssey fishing-zone catch tick → +3 thought + distinct favourite-credit.
- **US-092 Nutrifungus cliff** — >X% nutrition from nutrifungus + >Nd → "Fungus fatigue" hediff.
- **US-093 Exotic fauna first-bite** — DLC-exclusive species butcher → one-shot +4 "Tried exotic meat" + auto-seed favourite.
- **US-094 Biome-specific wild favourites** — wild bucket auto-seeds from current biome's forageables.
