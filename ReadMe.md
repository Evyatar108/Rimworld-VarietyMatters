[img]https://imgur.com/PZEKJnd.png[/img]
[h3] 2026-04-20 [/h3]
[list]
[*][b]Favourite foods:[/b] pawns now discover personal favourite ingredients from what they actually eat — no spawn-rolled rolls. Discovery is freshness-gated (meal must score ≥ 80% of the pawn's baseline variety), siloed by food category (one meat slot + one veg slot, plus a third "anything" slot for Gourmands covering eggs / milk / fungus / modded non-MeatRaw/non-PlantFoodRaw foods), and gated behind a 15-eligible-meals floor so day-1 chance meals don't lock in anything.
[*][b]Day-spread credit:[/b] a candidate ingredient only accrues credit when eaten across at least 2 in-game days. Three identical meals in one day don't count — a single farm-haul feast can't monopolize a slot.
[*][b]Mood thought:[/b] eating a fresh favourite fires a new "ate favourite food" thought (+3 mood, 1 day, stack 1). Back-to-back suppression means two consecutive identical favourite meals only fire once, but rice between two cow-meat meals fires twice — the reward tracks active rotation, not passive repetition.
[*][b]Saturation + rotation:[/b] each favourite slot accumulates a saturation counter (cap 4) every time the mood thought fires. Saturation lowers the candidate-credit count a rival needs to take over that slot (sat 0-1 → 3 credits, sat 2-3 → 2, sat 4 → 1), and decays by 1 per 2 in-game days without a triggering meal — so monoculture self-rotates without punishing genuine preference.
[*][b]Visible in UI:[/b] the Food Variety need tooltip and the Variety Details window now surface a "Favourites: cow meat, corn" line. Saturation 3 hints "becoming more open to other foods"; saturation 4 hints "getting tired of this as a staple" so the rival-threshold shift is legible.
[*][b]Notifications:[/b] first-ever crystallization in a slot fires a non-intrusive Message (positive event); a rival replacing an existing favourite fires a Letter (neutral event) with old / new labels.
[*][b]Opt-outs:[/b] Ascetic trait and the Biotech "no taste" gene (VM_NoVarietyNeed) pawns never record favourites and never fire the mood thought — the same gate that already hides their variety need also hides favourites.
[*][b]One kill-switch + full tuning:[/b] a new "Favourite Foods" section in Mod Settings exposes a master enable toggle plus 7 more knobs — Auto-assign favourites (see below), Mood bonus (0-20, default +3), Mood duration in hours (1-72, default 24), Discovery floor in meals (0-50, default 15), Freshness threshold % (0-100, default 80), Day-spread in days (0-10, default 2), and Saturation cap (1-10, default 4). Off on the master toggle freezes all state (preserves slots and saturation for re-enable) and skips mood / letter / message side effects. Mood bonus + duration are runtime-driven via a custom Thought_Memory subclass — changes take effect on the next fired thought; existing mood memories retain their original values. Deliberately excluded from the difficulty presets so toggling any of them doesn't flip the active-preset indicator.
[*][b]Auto-assign mode:[/b] if you want VFVE-style "favourites just appear" instead of the earn-through-eating default, flip the Auto-assign favourites toggle. Each pawn's empty meat / veg / (gourmand) wild slots get seeded with a random ingredient from the appropriate pool on their first eligible meal, skipping the 15-meal floor and the day-spread wait. The freshness-gated mood reward and saturation-driven rival rotation still apply — only the initial origin changes. Seed pool filters ingestibles above the DesperateOnlyForHumanlikes preferability bar so pawns don't get auto-assigned corpse meat.
[*][b]Modded food compatibility:[/b] the category classifier walks ThingCategoryDef parent chains, so ingredients under modded child categories (like PlantFoodRaw_Grain, MeatRaw_Fish, etc.) classify correctly into the right slot instead of falling through to the Gourmand-only wild bucket.
[*][b]Save compat:[/b] old saves load with the feature ON by default and all tuning knobs at MVP defaults; existing pawns accumulate toward the 15-meal floor from the next meal onward. No migration required.
[/list]
[h3] 2026-04-19 [/h3]
[list]
[*][b]Mod Settings redesigned:[/b] three difficulty presets (Easy / Default / Hardcore) at the top of the settings window batch-set all gameplay-affecting knobs with one click. Remaining controls are grouped into collapsible sections (Basics, Difficulty, Pawn Expectations, Meal Type Bonus, Cook Behavior, How Scoring Works) — all collapsed by default so new players see the preset row first. Renamed jargony labels; added tooltips to every control; made the active preset visually highlighted.
[*][b]Direct tier tuning:[/b] each expectation tier now has two independently tunable knobs — "Memory" (how many past meals that tier averages into mood, set in meals) and "Palate" (how fresh a meal must be before it counts, set as a percentage 0–50%). Previously these were derived from a single tier integer multiplied by a hidden factor; now you set what you see. Noble defaults: 34-meal memory with a 48% palate threshold.
[*][b]Tier-change resilience:[/b] per-pawn meal memory now stores up to the max tier's capacity plus a scoring buffer at all times, regardless of the pawn's current tier. Wealth swings, royal title changes, and mid-game setting edits no longer lose memory history — mood aggregation just shrinks its window instead.
[*][b]Asymmetric mood range:[/b] the "Halve variety mood effect" checkbox is replaced by two direct mood-point sliders — max negative (default -12) and max positive (default +12). Tune penalties and rewards independently: -8/+12 for soft penalties with full rewards, -14/+12 for harsher starvation, 0/+12 to disable negative variety mood entirely. Gourmand trait still doubles the whole range on top.
[*][b]Per-slot meal bonus sliders:[/b] the previously hardcoded meal-type-novelty bonus (simple/fine/lavish/modded) is now exposed as five direct percent sliders in a "Meal Type Bonus" section. Set any slot to 0 to disable that tier's bonus or raise them to reward complex cooking more.
[*][b]Balancing pass — Noble reachable:[/b] palate threshold formula softened so Noble pawns at moderate pantries (~30 unique ingredients) now sit at roughly 40% variety mood instead of near zero. Perfect rotation 100% still requires effort (14 unique ingredients cycled through fine meals). Lower tiers unchanged.
[*][b]New gene (Biotech) — "no taste":[/b] carriers have their variety need fully disabled. Auto-applied to vanilla Sanguophages so vampires no longer get "barren menu" mood hits from their hemogen-focused diet. Add the gene to any custom xenotype through the normal gene picker. Fully Biotech-optional — no effect on vanilla installs.
[*][b]Recalculation coverage expanded:[/b] editing tier values, the floor, slave/prisoner toggles or %, mood sliders, or bonus sliders now recalculates pawn variety mood immediately. Previously some edits took effect only on the next meal.
[*][b]Removed:[/b] the per-race enable/disable panel (right side of the settings window) — rarely used and cluttered the UI. Race-variety dict still auto-generates with every humanlike race enabled by default. Save-file editing remains an option for niche cases.
[*][b]Save compat:[/b] existing saves load with new defaults. Previously-tuned tier values and the old halve-mood checkbox no longer drive gameplay (the Scribed fields remain for backward parsing). Click a preset or manually tune the new controls to restore custom feel.
[/list]
[h3] 2026-04-18 [/h3]
[list]
[*][b]Compatibility — Meals Over Preserved Foods:[/b] the variety food-scoring postfix now runs at a lower Harmony priority, so MOPF's adjustments stack cleanly with the variety bonus instead of being overridden.
[*][b]Compatibility — Human Alien Races:[/b] runtime-generated races no longer throw a NullReferenceException when a ThingDef has no mod content pack. Existing saves may need to click "Reset Current Races" once in Mod Settings to repopulate the race list after upgrading.
[*][b]Compatibility — Vanilla Food Variety Expanded:[/b] VarietyMatters now warns once at startup if VFVE is loaded alongside it. Both mods modify the same ingredient-stacking code, which causes undefined stacking behavior — disable one of them.
[*][b]New setting — Low Expectation Floor:[/b] clamps the minimum variety expectation (default 3). Tribal and Naked-Brutality starts no longer crater to a -7 variety mood on day 1 under the vanilla 1/2/3 expectation tiers. Adjustable in Mod Settings.
[*]Large performance pass across the cooking, food-scoring, and need-tooltip hot paths. Late-game colonies should see noticeably lower per-tick allocation overhead.
[*]Food-scoring postfix now early-exits on mod-settings gates before fetching the pawn's diet tracker — saves a lookup on every meal evaluation for every pawn.
[*]Diet trackers are no longer created during read-only food scoring. Trackers appear only when a pawn actually ingests something.
[*]Cooking ingredient search: scratch collections reused across bills instead of allocated per slot; per-def penalty map keyed by reference instead of label string.
[*]Food-variety need tooltip caches its description until diet, clustering setting, or tracking-type setting changes — no more per-frame rebuild.
[*]Need_FoodVariety Disabled check reordered for cheap early-exit; race-enablement lookup backed by a shared cache invalidated only when race settings change.
[*]Ingredient stacking merge changed from O(n*m) Contains loops to O(n+m) hash set.
[*]Trait lookups (Gourmand) now via DefOf instead of string-based TraitDef.Named — one-time cost at load instead of per mood recalc.
[*]Fixed a latent save-load bug where dead pawns were removed from the tracker dictionary while iterating over it (could throw InvalidOperationException on load with dead pawns).
[*]Dead pawns now have their tracker entries freed the same tick they die instead of waiting for the next save-load cycle.
[*]ExposeData: invalid food entries on load are now summarized as a single warning per pawn instead of spamming one log per entry.
[*]Fine meal (and other multi-slot recipes) now honor the "num of ingredients" setting via an adaptive water-fill distribution — no more underusing ingredient variety when category stocks are lopsided. Soft-cap max(setting, variable slot count) prevents modded 3+-slot recipes from starving.
[/list]
[h3] 2026-04-17 [/h3]
[list]
[*]Major fix: cooks now actually use multiple ingredients per meal when varied stockpile is available. The previous single-ingredient bug is fixed along with a large batch of related cooking AI improvements.
[*]Each cook has independent variety memory — multiple chefs no longer share one ingredient-rotation history.
[*]Meal ingredient proportions now vary between cooks (jittered ±30%) instead of producing identical splits every time.
[*]Distinct ingredient types per meal are capped at the "ingredients when stacking" setting, so meals stay recognizable even when many food types are allowed.
[*]Multi-slot recipes (fine/lavish meals) distribute the ingredient-type budget across slots.
[*]New setting: "cooking memory depth" controls how many recent meals each cook remembers.
[*]Fixed kibble detection breaking on translated installs (was comparing localized text, now uses stable internal name).
[*]Performance: allocations in the cooking ingredient search loop eliminated for large colonies.
[*]Legacy saves: pre-fix ingredient-memory data gracefully discarded; variety rebuilds within ~10 cooks.
[/list]
[h3] 2025-08-04 [/h3]
[list]
[*]Major performance improvements for large colonies! Cooking operations are now much faster, especially noticeable with diverse food varieties.
[*]Fixed settings menu freezing - you can now adjust mod settings smoothly without lag.
[*]Optimized memory usage to reduce game stuttering and improve overall responsiveness.
[*]Enhanced cooking AI - chefs now select ingredients more efficiently while maintaining variety preferences.
[*]Fixed a bug where ingredient spoilage preferences weren't working correctly during cooking.
[/list]
[h3] 2023-12-05 [/h3]
[list]
[*]Improved the food searching algorithm to locate items not currently stored in the pawn's recent meal memory to reduce micro-management. The new algorithm also has superior performance.
[/list]
[h3] 2023-12-04 [/h3]
[list]
[*]Variety presence is now affected by whether a pawn experiences a negative mood debuff from consuming the food. This enhancement ensures better compatibility with mods that introduce new foods, precepts, genes, or traits.
[*]Lack of variety associated with Insect and Fungus meat is now contingent on whether the pawn holds the Despised precept for insect meat.
[*] General bug fixes.
[/list]
[h3] 2023-11-23 [/h3]
[list]
[*]Better visualization of recent meals, their variety, and reasons for lack of variety.
[*]Enhanced compatibility with popular cooking and food-related mods.
[*]Reduced variety need for prisoners and slaves.
[*]Improved performance.
[*]Enabled variety need for Gourmand pawns with specific requirements and mood impacts.
[*]Option to adjust the mood impact of the variety need.
[*]New setting to treat all meal qualities (Simple, Fine, Lavish, Gourmet) as the same type for variety calculations, integrating seamlessly with mods like Vanilla Cooking Expanded. Enabled by default, can be toggled in settings.
[*]Nutrient paste meals are now so unappetizing that they do not contribute to dietary variety, unless a pawn has the appropriate precepts.
[/list]


[img]https://imgur.com/Y1esBQc.png[/img]
Pawns now have a new need: variety in their diet. This mod introduces a unique gameplay dynamic where pawns require different types of food to stay mentally healthy and happy. Instead of just eating for survival, pawns will now seek out a diverse range of meals to satisfy their craving for variety. This new need adds an engaging layer of complexity to the colony's food management.

[img]https://imgur.com/qn0EDiK.png[/img]
[list]
[*][b]Diverse Diet Requirement:[/b] Pawns will experience mood penalties if they repeatedly consume the same type of food. A varied diet is key to keeping their spirits high.
[*][b]Enhanced Cooking Gameplay:[/b] Cooks in your colony are now more autonomous and will prepare meals with a variety of ingredients without the need for player micromanagement, ensuring a diverse diet for the pawns.
[*][b]New Challenges and Strategies:[/b] Players will face new challenges in food procurement and meal planning, leading to more strategic gameplay when it comes to managing resources and keeping the colony thriving.
[*][b]Detailed Needs Visualization:[/b] The mod includes a needs bar for each pawn. Hovering the mouse over this bar provides insights into their variety requirements, current satisfaction level, recent meals, and the diversity contribution of each meal.
[/list]

By integrating this mod, you're not just adding a feature; you're enhancing the depth and realism of your colony's daily life. It encourages players to think more critically about their food sources and meal choices, turning the act of feeding your pawns into a more immersive and rewarding experience.


[h2]What Counts as a Variety?[/h2]
A food must provide nutrition and either joy or be at least tasty raw to count as a variety. Rotten foods do not count. Cultural and preference variations apply, such as cannibals treating human flesh differently.
[h3]How Variety Is Scored[/h3]
Each meal is scored continuously between 0 and 1 based on ingredient overlap with the pawn's last 6 meals. Trivial swaps (rice+chicken+carrot then rice+chicken+lettuce) give diminishing variety, while wholly different meals (rice+chicken+carrot then bison+potato+egg) count as fresh. Eating a new meal type also adds a step-scaled bonus on top — simple meals get nothing, fine/lavish meals earn +5%, and modded 3/4/5-slot recipes earn +12% / +20% / +30%. Fine-grained, no yes/no thresholds.

[h2]How Much Variety Do Pawns Need?[/h2]
The base variety expectation depends on a pawn's current thought expectation and can be adjusted in settings.

[h2]How do Pawns Track Varieties?[/h2]
Each expectation tier has a configurable memory depth (in meals) and a palate threshold (how fresh a meal needs to be to count). Defaults: Extremely Low remembers 9 meals with a 4% palate; Noble remembers 34 meals with a 48% palate. Both are directly tunable per tier in Mod Settings → Pawn Expectations.

[h2]Will Pawns Seek New Varieties When Eating?[/h2]
Pawns have a preference for new varieties and will choose meals accordingly, requiring less micromanagement.

[h2]Will Pawns Use a Variety of Ingredients When Cooking?[/h2]
Cooks will use a variety of ingredients, with better cooks having a longer memory for ingredient diversity. Note: cooks prioritize ingredients based on proximity to the stove.

[h2]Are There Any Other Features?[/h2]
Options include preventing meals with different ingredients from stacking and adjusting the number of ingredients tracked in meal stacks. These features vary based on the selected variety option.

[h2]Additional Notes:[/h2]
[list]
[*]Prisoners and slaves have a reduced variety need.
[*]Gourmand pawns have unique variety requirements and mood impacts.
[*]Ascetic pawns do not require variety.
[/list]

[img]https://imgur.com/PpOrKfc.png[/img]
[list]
[*] Github repository is [url=https://github.com/Evyatar108/Rimworld-VarietyMatters] here [/url] 
[*] The original mod this mod is based on is [url=https://steamcommunity.com/sharedfiles/filedetails/?id=2353847457] here [/url] 
[/list]

[img]https://imgur.com/rEGGf6D.png[/img]
Banners: Mewn Goddess
Mod: HTxL