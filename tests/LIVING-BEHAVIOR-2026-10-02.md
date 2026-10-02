# Living Behavior Revision: Soulmates 0.19.0

Local growing-alpha candidate, October 2, 2026. This is not a public release or a graphical-playtest claim.

## What Was Found

- The creator's opt-in local journal showed actual mining/pickup activity, but the most recent snapshots had a retained mining assignment and only 3 energy. This explains one plausible reason for unavailable visits; it does not establish the cause of every reported idle moment.
- Critter context previously used the automatic idle/energy gate even for explicit invitations. An accepted-looking preference could stay behind ongoing work. Direct eligible invitations now replace the assignment; automatic visits retain their work/energy gates. Disabled autonomy, active defense and unanswered prompts receive a distinct explanation rather than an inaccurate saved-choice response.
- Targeted catching previously swept all nearby critters. It now asks the native catch pipeline for the selected NPC only, checking its original type, real carried net, native lava restrictions, capacity, range and line of sight. A changed target yields rather than silently redirecting that click.
- Personal recall previously returned one chronicle fact. It now composes bounded chapters from real saved events plus personality/voice reflections. No remembered event is fabricated. Empty or unrelated categories use present observations instead of a generic missing-story line.
- The larger Commands fan needed a little more radius to separate its hit targets. All eight actions are checked for non-overlapping click areas.

## Implemented Scope

- Items conversation categories use Terraria item icons for food, ores, tools, weapons, recovery, materials, critters and other cargo. Replies inspect actual carried or held items. They do not consume items, create cargo, reward XP or increase bond.
- Confirmed non-coin pickups, first critter greetings and nearby losses can enter the existing bounded eight-event chronicle. Item memories are rate-limited and deduplicated. Ambient observations inspect visible nearby NPCs/drops and real weather, time, location layer or owner condition. They wait behind speech, questions and defense. Long dialogue scrolls in Talk Mode; world text has bounded reading time and viewport-aware wrapping.
- Pause retains the current assignment, holds work/pickup/item use and recovers energy. Resume continues retained work. Abort clears it and holds new automatic work until Resume or a new explicit command. Defense/healing remain available. The hold survives save/load; older Sigils default to unpaused.
- Exact live directed targets are not saved across recall or restarting the game. A paused ordinary routine can be resumed after reload, but a removed live NPC/drop/branch target is not resurrected.
- Dry side branches of vanilla `TileID.Trees` can be pruned with a real axe in the owner's inventory or companion cargo. Strict native frame and neighboring-stem checks reject leafy branches, central stems, ground, gem trees and unsupported tree families. Direct Forest accepts supported branches; Forester/AETHER can choose them through the existing initiative scheduler. Native `WorldGen.KillTile` supplies drops, which enter the normal collection flow without an extra pickup sweep or synthetic wood.
- New enums append rather than renumber existing save values. All nine catalogs contain 810 keys. Profile/extra-AI transport includes the work hold and selected visit. Multiplayer participants must run the same version.
- README, current changelog and local English/German Workshop descriptions match the candidate. The current changelog contains no historical updates. Workshop text is 7,568 English / 7,739 German UTF-8 bytes, below Steam's 8,000-byte limit. Public pages were not changed.

## Verification

- Main mod and regression probe compile against the installed tModLoader references with zero warnings/errors.
- Isolated tModLoader 2026.8.3.0 loads packaged Soulmates **0.19.0** and passes **35,867 native-engine assertions, zero failures**.
- Static localization, source-reference, text-layout, UI-transform and description-size checks pass **45,957 assertions**, covering 810 keys in nine languages and 410 source references.
- New full companion AI tick sequences exercise paused mining over 240 ticks, continuing the retained mining target, aborted work over 240 ticks, low-energy directed critter visits, preserving a paused visit timeout, exact native net catching, actual owner axe-use learning, ambient speech and its no-interruption gate, directed branch work and autonomous permitted Forester/AETHER branch work over 600 ticks. Movement is integrated by the fixture between native AI calls.
- Native branch fixtures check both side orientations, rooted central wood and protected ground. Pruning removes only the selected dry twig, retains the central tree, produces native wood and produces nothing on a repeated call. The first test wrongly assumed exactly one wood unit: installed native `KillTile_GetItemDrops` can add one bonus wood through `KillTile_GetTreeDrops`. The corrected check verifies the real 1-2-unit native range, no dirt/acorns and unchanged repeated-call totals; gameplay drops were not rewritten to fit the test.
- Existing checks remain for pickup/cargo/wallet conservation, native item hooks, creation, binding, combat, healing, learning, saplings, task permissions, relationships, input priority, forged/stale requests and rock/paper/scissors. Single-player and server authority paths are exercised. All nine actual cultures are selected and registered texts compared with the packaged catalogs.
- A denied optional registry association and intentional malformed-packet warnings appear during the headless run. The successful test process exits 0; those warnings are not counted as passing gameplay evidence.

## Remaining Limits

No live graphical client was controlled or new screenshots produced in this run. Wheel readability at different UI scales, long story scrolling, critter traversal over uneven terrain, generated tree variants, long progression sessions and live multiplayer still need real playtesting. Full native AI decisions are exercised, not a complete world simulation.

Critter company remains temporary native NPC companionship, not a new pathfinder or a permanent extra vanilla vanity-pet slot. Its collision, catching, damage and despawning rules remain native. Gold/statue/released creatures retain protection.

Some multiplayer replies, including composed stories, retain authority-formatted text and NPC/item-name arguments in the server language. Key-based environmental remarks use the client's catalog. New translations are AI-authored and need native-speaker review.

No chat, save, world or character data was uploaded or edited. The journal is local opt-in evidence. Test packages use a separate save directory; the regression probe is not installed in the normal client. Installation hash and backup are recorded under `outputs/living-behavior-0.19.0/installation.json`.
