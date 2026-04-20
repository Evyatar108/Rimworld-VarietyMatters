# Steam Workshop Description — current (2026-04-19)

The copy below is the intended Steam Workshop page body. Paste the fenced block (BBCode) into the Workshop editor when updating the Steam page. Agents + Codex validated this draft against the live codebase on 2026-04-19; see `archive/2025-08-04.md` for the prior version.

---

```
[img]https://imgur.com/PZEKJnd.png[/img]

[b]A diet need that makes your cooks actually cook.[/b]

[img]https://imgur.com/Y1esBQc.png[/img]
Your colonists are tired of eating the same gruel. VarietyMatters adds a diet-variety need: pawns remember their last 6 meals and reward cooks who rotate the menu. Each meal gets a continuous 0–100% variety score. Three difficulty presets let you tune everything in one click. Optimized for late-game colonies. Safe to add to existing saves, safe to remove.

[img]https://imgur.com/qn0EDiK.png[/img]
[list]
[*][b]Diet-variety need:[/b] each meal is graded the moment a pawn eats it, comparing its ingredients to their last 6. Rice+chicken+carrot then rice+chicken+lettuce? Slight drop. Bison+potato+egg after? Full credit.
[*][b]Smarter cooks:[/b] each chef keeps their own ingredient-use history; the cook AI favors ingredients they haven't used recently when picking what to drop in the pot. Multi-slot recipes (fine / lavish) share the budget across slots so both meat and veg sides stay varied.
[*][b]Tunable difficulty:[/b] three one-click presets — Easy / Default / Hardcore. Or drill into 20+ individual knobs if you want to tune.
[*][b]Biotech "no taste" gene:[/b] auto-applied to vanilla Sanguophages so vampires skip variety mood entirely (they'd rather drink hemogen anyway). Drop the gene on any custom xenotype via the normal gene picker.
[*][b]Detailed inspection:[/b] hover the need bar for a food-icon strip of recent meals; open the details window for per-meal scores, ingredient repeat-vs-fresh breakdown, meal-age labels, and colorblind-friendly status glyphs.
[/list]

[h2]Is this mod for you?[/h2]
VarietyMatters is built for players who want diet variety to be a [b]real planning challenge[/b] — one that pushes you to grow a diverse pantry, hunt for more than one meat, and queue more than one recipe. If you'd rather variety happen passively as a mild immersion layer (mood nudges based on whatever pawns happen to eat, no planning required), [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3334272487]Vanilla Food Variety Expanded[/url] is a lighter alternative and a solid pick for casual or roleplay-focused playthroughs.

VarietyMatters keeps the challenge at the strategy layer — what you plant, what you hunt, which recipes you queue, how you tune the tiers — because the built-in cook AI handles ingredient rotation without you touching each food bill. And because the difficulty scales with how repetitive your pantry is, it gives the content mods you already have a real gameplay reason to exist: expanded plant packs, animal packs, and cooking packs stop being decorative stockpile and become a measurable mood payoff.

[h2]How it works[/h2]
A food must provide nutrition and not be actively disliked. Rotten, disgusting-to-ingest, and per-ideology-despised foods (e.g. insect meat, human meat) short-circuit to "no variety" — they neither raise nor lower mood. Nutrient paste doesn't count for pawns who find it disgusting; raw / kibble generally doesn't count either.

Each meal is graded against the pawn's last 6 meals: fully-fresh ingredients → 100%, full repeats → 0%. A meal whose recipe type hasn't appeared in the pawn's memory queue earns a bonus on top (simple meals earn nothing by default; fine / lavish +5%; modded 5-slot recipes up to +30%). Mood averages across the pawn's current tier's memory window, so a single bad meal doesn't crater mood and a single great meal doesn't spike it.

[h2]Tune it your way[/h2]
Every expectation tier has two independent knobs in Mod Settings:
[list]
[*][b]Memory[/b] — how many past meals that tier averages into mood. Defaults: 9 at Extremely Low, 34 at Noble.
[*][b]Palate[/b] — how picky that tier is. Meals above this score get full credit; below it they still contribute a fraction (not a hard cutoff). Defaults: 4% at Extremely Low, 48% at Noble.
[/list]
Two more sliders control [b]max negative[/b] (default -12) and [b]max positive[/b] (default +12) variety mood independently — set -8/+12 to soften penalties while keeping rewards, or 0/+12 to disable negative mood entirely. Five per-slot sliders tune the meal-type-new bonus per recipe complexity.

[h2]Additional notes[/h2]
[list]
[*]Prisoners (default 50% of tier) and slaves (default 65%) have scaled variety expectations AND palate thresholds. Tune or disable per role.
[*]Gourmand trait: +25% expected variety and doubles the mood-range extremes (so the ±12 defaults become ±24).
[*]Ascetic trait: opted out of the variety need and the mood thought entirely.
[*]Tribal / Naked Brutality starts: a "minimum expected variety" floor keeps the early game from collapsing under vanilla's low-tier expectations.
[/list]

[h2]Recent Updates[/h2]

[h3] 2026-04-20 [/h3]
[list]
[*][b]Favourite foods:[/b] pawns now discover personal favourites from what they actually eat, not a spawn-roll.
[/list]
Discovery is freshness-gated, siloed by food category (meat / veg, plus an "anything" slot for Gourmands), and gated behind a 15-meal floor so day-1 luck can't lock anything in. Eating a fresh favourite fires a +3 mood thought for 1 day with back-to-back suppression, and a saturation counter rotates stale favourites out so monoculture self-corrects — all of it toggled by one kill-switch in a new "Favourite Foods" settings section.

[h3] 2026-04-19 [/h3]
[list]
[*][b]Mod Settings redesigned[/b] — three difficulty presets at the top, collapsible sections beneath, full-width layout, jargon-free labels, tooltips on most controls.
[*][b]Direct tier tuning[/b] — per-tier memory (in meals) and palate threshold (as %) are now set directly, no hidden × 2.25 multiplier.
[*][b]Tier-change resilience[/b] — per-pawn memory now stores up to the max tier's capacity + scoring buffer, so wealth swings or title changes don't lose history.
[*][b]Asymmetric mood sliders[/b] — separate max-negative and max-positive mood endpoints.
[*][b]Per-slot meal bonus sliders[/b] — previously-hardcoded bonus values are now five direct sliders.
[*][b]Balancing pass[/b] — Noble pawns reachable at ~30-ingredient pantries (was near-zero mood below ~50 ingredients).
[*][b]Biotech "no taste" gene[/b] — auto-applied to Sanguophages; drop on any xenotype to opt out.
[*][b]Live recalc[/b] — tier / mood / bonus / prisoner / slave edits now trigger a debounced recalc so changes take effect without waiting for the next meal.
[*][b]Removed:[/b] per-race enable panel (rarely used; cluttered the window).
[/list]

[h3] Earlier 2026 [/h3]
[list]
[*]Compatibility hardening: Meals Over Preserved Foods, Human Alien Races, Vanilla Food Variety Expanded (startup dual-load warning).
[*]Large performance pass across cooking, food-scoring, and need-tooltip hot paths.
[*]"Minimum expected variety" floor for tribal / Naked Brutality day-1 starts.
[*]Major cooking-AI fix: cooks now actually use multiple ingredients per meal, with independent per-chef memory, multi-slot recipe budget distribution, and ±30% ingredient-proportion jitter between meals.
[/list]

Full history on [url=https://github.com/Evyatar108/Rimworld-VarietyMatters]GitHub[/url].

[img]https://imgur.com/PpOrKfc.png[/img]
[list]
[*] GitHub repository is [url=https://github.com/Evyatar108/Rimworld-VarietyMatters]here[/url].
[*] The original mod this mod is based on is [url=https://steamcommunity.com/sharedfiles/filedetails/?id=2353847457]here[/url].
[/list]

[img]https://imgur.com/rEGGf6D.png[/img]
Banners: Mewn Goddess
Mod: HTxL
```
