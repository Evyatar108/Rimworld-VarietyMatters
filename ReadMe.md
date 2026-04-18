[img]https://imgur.com/PZEKJnd.png[/img]
[h3] 2026-04-18 [/h3]
[list]
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
[h3]Meal Variety Options[/h3]
[b]Option 1: Ingredients Only[/b]
This mode focuses exclusively on the variety of ingredients. Meals are considered varied based on the different ingredients they contain, irrespective of the meal type. It's ideal for players who play without many mods that add additional meal types.
[b]Option 2: Meal Name Only[/b]
This option simplifies the variety mechanic by considering only the type of meal consumed, disregarding the ingredients used. It's particularly well-suited for gameplay with a wide array of meal types introduced by mods, where the meal itself brings the variety.
[b]Option 3: Both Ingredients and Meal Name[/b]
The most player-friendly option, this mode tracks both the meal's name and its ingredients. A meal is considered a new variety if either its name is different, or it contains a different ingredient. This approach is perfect for those seeking a challenge that is not too demanding, allowing for varied gameplay without extensive micromanagement.

[h2]How Much Variety Do Pawns Need?[/h2]
The base variety expectation depends on a pawn's current thought expectation and can be adjusted in settings.

[h2]How do Pawns Track Varieties?[/h2]
Pawns remember foods eaten up to 2.25x their current variety expectation, requiring around half of their diet to be new varieties to meet this need.

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