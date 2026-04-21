# Phase 11 — Release Polish

## Goal

Codex + arch reviews flagged: roadmap is a feature map, not a ship plan. Phase 11 closes that gap with localization, Workshop-page polish, comment-response discipline, and compat matrix maintenance. Runs in parallel across release cycles; not a sequential bottleneck.

## Stories

### US-110 — Localization setup
**Effort**: 2 d + ongoing per release
**Scope**:
- Audit all user-facing strings across `Source/`, `Defs/`, settings UI.
- Extract to `Languages/English/Keyed/` + `Languages/English/DefInjected/` per RimWorld convention.
- Create placeholders for RU / ZH / KO (top community-contributed languages for food-mod audience).
- Document string-contribution workflow in `CONTRIBUTING.md`.

**Acceptance**:
- Every settings label, letter, thought label, tooltip line is Keyed.
- Running w/ `Prefs.LangFolderName = "Russian"` → missing strings fall back to English gracefully, no ITAB broken labels.
- README links to translation contribution flow.

**Notes**: Redux ships RU/ZH/KO; missing ours = 30–40% audience cut on a mood mod where tooltips matter (codex flag).

### US-111 — Workshop page refresh per release
**Effort**: 0.5 d per release
**Scope**:
- Pre-1.0 release baseline: rewrite Workshop description with feature list, differentiator pitch (three-slot favourites, cook mastery, no-alloc cooking AI), exclusion list teaser.
- Per-release: regenerate thumbnail variant, rotate screenshots, post changelog with emoji bullet headers.
- Coordinate w/ `rimworld-tools:thumbnail-spec` skill.

**Acceptance**:
- Description has "current as of version X.Y" footer.
- Screenshots rotate at least every minor release.
- Changelog in Workshop + GitHub + `ChangeLog.txt` stay in sync.

### US-112 — Workshop comment-response SLA
**Effort**: ongoing (~20 min/week)
**Scope**:
- Target: acknowledge new Workshop comment within 7 days.
- Categorize: bug (file GitHub issue) / feature request (add to "harvest" list) / compat question (link to matrix) / thanks (thumbs-up).
- Harvest compat issues into Phase 0.5 `classifier-matrix.csv` updates.
- Quarterly: re-read last 100 comments, update FAQ section in Workshop description.

**Acceptance**:
- No comment goes unanswered past 7 days for 4 consecutive weeks (measurement window).
- At least 3 feature-request threads surface in the public issue tracker per quarter.

### US-113 — Compat matrix publish + maintenance
**Effort**: 0.5 d per release
**Scope**:
- Publish `classifier-matrix.csv` (from Phase 0.5 US-073) as Workshop-page table.
- Per release: re-run matrix against target modded-food mods, update CSV + Workshop description.
- Badge each mod: ✅ works / ⚠️ needs override / ❌ incompat.

**Acceptance**:
- Matrix timestamp stays within 30 days of latest release.
- GitHub issue template `compat-fix.md` exists and is linked from Workshop description.

### US-114 — In-game tip letter onboarding
**Effort**: 1 d
**Scope**:
- First-load tip letter when a player enables VarietyMatters on a new save: "Welcome — here's what this mod does, here's how to customize, here's where to find settings."
- Dismissable; gated by `PerLifetimeFirstTime` dict in `ModSettings_VarietyMatters`.
- Also: on major version bump (1.0 → 1.1 etc.), fire a "What's new" letter summarizing changes.

**Acceptance**:
- Fresh save → letter fires once on first tick after load.
- Existing save → no letter (unless version bumped).
- Dismissed letter does not re-fire.

## Risks

- **Translator dropout.** Volunteer translations go stale when strings change. Mitigation: Keyed key names stable across features, translation-friendly placeholders.
- **Comment fatigue.** SLA lapses when life intervenes. Mitigation: weekly 20-min block on calendar; not a deal-breaker if missed occasionally.
- **Thumbnail churn** becomes visual noise. Keep aesthetic consistent across release cycle — only content changes.

## Notes

- Phase 11 is infrastructure, not features — treat it as "always on" across the whole roadmap rather than a sequential milestone.
- First milestone: ship US-110 (localization scaffold) + US-111 (Workshop refresh) BEFORE Release 1.0 so language support is advertisable from the start.
