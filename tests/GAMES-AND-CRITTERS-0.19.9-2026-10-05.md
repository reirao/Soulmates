# Soulmates 0.19.9: Games, Critters and Consistent Consent

## Status

Local growing-alpha candidate, built against tModLoader 2026.8.3.0. This pass has not installed it in the normal player client, published it, committed or pushed Git changes. The previously installed client is 0.19.8. Existing unrelated work was preserved. All multiplayer peers must use 0.19.9 because the profile transport now includes two additional consent fields.

## Implemented

- Move the existing Rock, paper, scissors game to Companion Soulwheel > Games. Keep six Bond gestures separate, native move symbols, independent choices and stepwise Back. No other minigames are claimed.
- Give every companion a cached basic Bug Net capability. It is not an inventory item, uses no cargo slot and cannot be withdrawn. Real carried lava-proof nets remain upgrades. Native catching and exact-drop cargo transfer remain authoritative; protected creatures, capacity and client restrictions still apply.
- Give automatic Company and Collect separate saved Ask / Always / Never policies, defaulting to Ask on legacy Sigils. Both use the existing token-bound Yes / No / Always / Never answer wheel. Yes approves one target, No defers that ability, Always affects only that ability, and Never blocks its automatic selection.
- Revalidate original NPC reference, type, mode, room, Company quota, mood and energy before accepting consent. Reject forged, expired, duplicate or stale answers. An unanswered permission cannot be replaced by another offer. Client-only calls cannot change these policies.
- Reset restores Ask and cancels active automatic work/visits. A direct target order is explicit consent for that visit, not future authorization. Remove forestry's separate idle-insect capture path; automatic captures now use the critter consent flow.
- Fail closed for an unregistered initiative policy. Regression checks enumerate autonomous activities and require explicit policy mapping, independent persistence, binary transport and actual rule buttons. Future additions must extend that contract; tests do not themselves implement future abilities.
- Queue passive observations behind existing questions, speech and expressions, ahead of new ambient remarks and crafting suggestions. Show native creature > greeting symbols and a native critter reply only when the greeting is delivered. Watch never captures or guides; invalid/departed encounters are discarded.
- Recognize native tree families, explicit palms and mushroom trees for context. Keep Forestry reachable during shake cooldowns or unsupported actions, with feedback on selection. Reject unsupported shakeable-tree/soil combinations before scheduling automatic work. Pruning is still limited to verified bare vanilla side-twig frames.
- Update all nine catalogs and current descriptions without copying historical release notes into the next Workshop upload text.

## Verification

| Check | Final result |
| --- | --- |
| Native production compiler and packager | 0 errors, 0 warnings |
| MSBuild production check | 0 errors, 0 warnings |
| General native regression mod | 42,922 assertions, 0 failures |
| Independent native safety audit | 145 expectations pass, 0 defects, 0 harness errors |
| Static/localization/UI arithmetic harness | 48,366 assertions pass; 853 keys in nine languages; 442 source references |
| Staged source identity | 93 packaged input files match the checkout |
| Description size | 7,887 UTF-8 bytes, below the 8,000-byte limit |
| Whitespace diff check | Pass |

The isolated native suites load finalized Terraria/tModLoader hooks and then exit before loading a real world. They exercise single-player and server-authority branches with synthetic owners, real native NPC defaults/catches/deaths and disconnected sockets. They are not connected multiplayer or graphical gameplay certification. The regression probe has 34 test-only nullable/Hjson-reference warnings; the independent probe and production mod have none.

New controls cover all four consent responses for both critter abilities, follow-up opportunities, independent policy persistence, legacy defaults, stale identities, mode changes, reset, authority rejection and real native captures. Rule hit-tests and clicks run in all nine languages at 640x360, 800x600, 1280x720 and 1920x1080 with 0.85, 1 and 1.5 UI scales. Games navigation also has native hit-tests at three resolutions.

Native death controls cover 16 natural critter types in both single-player and server authority: rabbits, squirrels, penguins, birds, insects, ducks, a worm and a golden rabbit. Nearby deaths produce localized grief without XP. The player's reported missing visible death reaction was not reproduced in these controls; graphical timing, cause and visibility in that real session remain to be retested. Existing catch-not-death, player-credit, queued-grief and witnessed-range controls remain enabled.

## Critical Rechecks

The first extended run exposed a palm-family context omission and missing centralized safety guards; those were corrected. Its newly added fixtures also had a missing client view transform, an unexpired question cooldown and unacknowledged server inventory transfers. Those harness problems were corrected without removing production authority, cooldown or transaction protections. Old automatic-Company network controls now explicitly approve the visit before checking synchronization.

The next full run passed all new consent, observation, death and tree checks, leaving two old unapproved network-fixture expectations. After adapting those controls to the new consent contract, the full regression suite passed. The separate audit then added nine independent consent boundary checks and passed all previous safety checks as well. No failing results were presented as a successful graphical playtest.

## Exact Artifact

Package: `outputs/games-and-critters-0.19.9/Soulmates.tmod`

Header: `TMOD`, loader `2026.8.3.0`, internal name `Soulmates`, version `0.19.9`.

Size: 1,516,886 bytes.

SHA-256: `4CC565F89413225D8669B7C4EA921E9B71269860CE02FBD301FC80863336209D`.

Raw isolated reports are alongside the package. No player journal, private remarks, character/world saves or test-only mods are included in the production artifact.
