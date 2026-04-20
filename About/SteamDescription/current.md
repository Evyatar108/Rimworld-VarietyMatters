# Steam Workshop Description — current (2026-04-19)

The copy below is the intended Steam Workshop page body. Paste the fenced block (BBCode) into the Workshop editor when updating the Steam page. Agents + Codex validated this draft against the live codebase on 2026-04-19; see `archive/2025-08-04.md` for the prior version.

---

```
[img]https://imgur.com/PZEKJnd.png[/img]

[b]A diet need that makes your cooks actually cook.[/b]

[img]https://imgur.com/Y1esBQc.png[/img]
Your colonists are tired of eating the same gruel. VarietyMatters adds a diet-variety need: pawns remember recent meals, crave something new, and reward your cooks when the menu actually rotates. Every meal scores continuously between fresh and repetitive based on ingredient overlap — no arbitrary thresholds, no yes/no flags. Three difficulty presets ship out of the box; every knob is exposed if you want to tune further.

[img]https://imgur.com/qn0EDiK.png[/img]
[list]
[*][b]Diet-variety need:[/b] each meal is graded at the moment a pawn eats it, comparing its ingredients against the pawn's last 6 meals. Rice+chicken+carrot then rice+chicken+lettuce? Slight score drop. Bison+potato+egg after? Full credit. No thresholds, no yes/no flags — a continuous 0–100% per meal.
[*][b]Smarter cooks:[/b] each chef remembers the ingredients they recently used and rotates across meals. Fine / lavish meals distribute the ingredient-type budget across recipe slots so both meat and veg components stay varied.
[*][b]Tunable difficulty:[/b] one-click Easy / Default / Hardcore presets set 21 knobs at once. Or drill in: per-tier memory depth, palate pickiness, max mood penalty / reward, per-recipe-size bonuses, prisoner/slave percentages.
[*][b]Biotech "no taste" gene:[/b] auto-applied to vanilla Sanguophages so vampires skip variety mood entirely (they'd rather drink hemogen anyway). Drop the gene on any custom xenotype via the normal gene picker to opt mechanoid-adjacent or lore-specific colonists out.
[*][b]Detailed inspection:[/b] hover the need bar for a food-icon strip of recent meals; open the details window for per-meal scores, ingredient repeat vs fresh breakdown, meal-age labels, and colorblind-friendly status glyphs.
[/list]

[h2]What counts as a variety?[/h2]
A food must provide nutrition and not be actively disliked by the eater. Rotten food, disgusting-to-ingest food, and per-ideology-despised foods (insect meat, human meat, fungus — depending on the pawn's precepts) short-circuit to a "no variety" status that contributes neither positively nor negatively. Nutrient paste doesn't count for pawns who find it disgusting; raw / kibble foods typically don't count either.

[h2]How does it score?[/h2]
Each meal is graded against the pawn's last 6 meals: fully-fresh ingredients → 100%, full repeats → 0%. On top of that, a meal whose recipe type hasn't appeared in the pawn's memory queue earns a bonus (simple meals earn nothing by default; fine / lavish +5%; modded 5-slot recipes up to +30%). Mood averages across the pawn's current tier's memory window, so smoothing kicks in automatically.

[h2]Tune it your way[/h2]
Every expectation tier has two independent knobs in Mod Settings:
[list]
[*][b]Memory[/b] — how many past meals that tier averages into mood. Defaults: 9 at Extremely Low, 34 at Noble. Directly editable (no hidden multipliers).
[*][b]Palate[/b] — freshness threshold. Defaults: 4% at Extremely Low, 48% at Noble. Adjustable per-tier.
[/list]
Two more sliders control [b]max negative[/b] (default -12) and [b]max positive[/b] (default +12) variety mood independently — set -8/+12 to soften penalties while keeping rewards, or 0/+12 to disable negative mood entirely. Five per-slot sliders tune the meal-type-new bonus per recipe complexity.

[h2]Additional notes[/h2]
[list]
[*]Prisoners (default 50% of tier) and slaves (default 65%) have scaled variety expectations AND palate thresholds. Tune or disable per role.
[*]Gourmand trait: +25% expected variety and doubles the mood-range extremes (so the ±12 defaults become ±24).
[*]Ascetic trait: opted out of the variety need and the mood thought entirely.
[*]Tribal / Naked Brutality starts: a "minimum expected variety" floor keeps the early game from collapsing under vanilla's low-tier expectations.
[/list]

[h3]Recent Updates[/h3]

[h3] 2026-04-19 [/h3]
[list]
[*][b]Mod Settings redesigned[/b] — three difficulty presets at the top, collapsible sections beneath, full-width layout, jargon-free labels, tooltips on every control.
[*][b]Direct tier tuning[/b] — per-tier memory (in meals) and palate threshold (as %) are now set directly, no hidden × 2.25 multiplier.
[*][b]Tier-change resilience[/b] — per-pawn memory now stores up to the max tier's capacity + scoring buffer, so wealth swings or title changes don't lose history.
[*][b]Asymmetric mood sliders[/b] — separate max-negative and max-positive mood endpoints.
[*][b]Per-slot meal bonus sliders[/b] — previously-hardcoded bonus values are now five direct sliders.
[*][b]Balancing pass[/b] — Noble pawns reachable at ~30-ingredient pantries (was near-zero mood below ~50 ingredients).
[*][b]Biotech "no taste" gene[/b] — auto-applied to Sanguophages; drop on any xenotype to opt out.
[*][b]Recalc coverage expanded[/b] — tier / mood / bonus / prisoner / slave edits now take effect mid-game immediately.
[*][b]Removed:[/b] per-race enable panel (rarely used; cluttered the window).
[/list]

[h3] 2026-04-18 [/h3]
[list]
[*]Compatibility hardening: Meals Over Preserved Foods (Harmony priority), Human Alien Races (null-safe race key), Vanilla Food Variety Expanded (startup dual-load warning).
[*]Large performance pass across cooking, food-scoring, and need-tooltip hot paths.
[*]New "Minimum expected variety" floor for tribal / Naked Brutality day-1 starts.
[/list]

[h3] 2026-04-17 [/h3]
[list]
[*]Major fix: cooks now use multiple ingredients per meal when varied stockpile is available.
[*]Each cook has independent ingredient-rotation memory.
[*]Fine / lavish recipes distribute the ingredient-type budget across slots.
[*]New "Cooking memory depth" setting; ±30% ingredient-proportion jitter between meals; numerous latent cooking-AI bug fixes.
[/list]

Full history on [url=https://github.com/Evyatar108/Rimworld-VarietyMatters]GitHub[/url].

[img]https://imgur.com/PpOrKfc.png[/img]
[list]
[*] GitHub repository is [url=https://github.com/Evyatar108/Rimworld-VarietyMatters] here [/url] 
[*] The original mod this mod is based on is [url=https://steamcommunity.com/sharedfiles/filedetails/?id=2353847457] here [/url] 
[/list]

[img]https://imgur.com/rEGGf6D.png[/img]
Banners: Mewn Goddess
Mod: HTxL
```
