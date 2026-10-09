# Soulmates beta acceptance

## Current decision

**0.24.0 - Learning Together supersedes 0.23.2.** The creator reports a positive first playtest and requests publication. [Release notes](releases/0.24.0.md) describe bounded contextual preferences and evaluations integrated into existing abilities and the answer wheel. This is not free-building AI or a new task executor. Native checks pass 94,235 assertions, the independent audit 198 expectations and static/localization checks 55,855 assertions. Production builds have no warnings/errors; the installed package matches all 83 source files. All multiplayer peers require 0.24.0. Actual publication and subscriber delivery are separate from this acceptance decision.

**V remains unverified in real play, not declared fixed.** The player reports that it did not work in the real 0.23.1 client. Earlier trigger-injection tests were not physical keyboard acceptance. Fixed-V claims are withdrawn and preserved as [the V key lesson](README.md#the-v-key-lesson); the Sigil reports the actual binding. Use her wheel's Details/Pets entries. A positive general playtest does not close this specific gate or exhaustive graphical/connected multiplayer acceptance.

## Earlier Preparation History

These paragraphs preserve decisions at the time of preparation. They are not current installation or pet-support instructions.

**Local 0.23.0 - Pet Inventory is installed in the normal client, not publicly published.** [Installation was verified October 9](tests/CLIENT-INSTALL-0.23.0-2026-10-09.md); the loaded version still needs its startup check. It restores direct ore-search/area-mining entries beside Config/Last and adds twelve separate real-item pet slots. Registered reusable vanity/light summon items supply the visual; one harmless familiar follows the companion with custom movement. Native player buffs, special utility abilities and arbitrary modded drawing are not cloned. Existing Sigil tags migrate without deleting cargo; the network profile now includes pet inventory, so all peers must use 0.23.0. Graphical pet-family acceptance and connected multiplayer remain open. Details: [candidate notes](releases/0.23.0.md).

**0.22.2 - Living Critters is the public baseline.** Normal-client installation was verified on October 8, 2026. On October 9 the creator confirms a successful playtest and Workshop publication; Steam records the matching patch at 08:05 CEST with review pending at the publication check. This does not close individual acceptance gates or establish universal subscriber delivery. The existing maturity criteria in VERSIONING.md still apply.

The core scope focuses on correctness, balance, usability and compatibility. The requested 0.21.0 refinement added bounded work controls and two item-backed pets; its normal-client installation is verified separately. Local 0.22.0 repairs critter scheduling and introduces a character/diagnostic view without adding abilities or profile fields. Additional minigames, generic pet-buff slots, combat critter swarms, procedural art and wider autonomous systems are not required for beta or promised for 1.0.

**0.20.1 - Words in Circles is a local usability candidate**, not installed or published. It organizes existing emotes and Bond actions into semantic categories without changing behavior permissions, persistent fields or packet formats. It does not close the graphical acceptance gate.

**0.21.0 - Clear Intentions supersedes that local candidate.** Config/Last, the bounded mining compass, manual critter corrections and two item-backed flying companion pets remain inside Soulmates; no mini-mods are introduced. A pet is a harmless custom follower using native animated art, not the vanilla player-pet AI or an extra player buff slot. Only Zephyr Fish/Nectar are supported. Work and pet fields deliberately extend save/network data: legacy Sigil tags default safely; the old wire prefix is preserved but mixed-version multiplayer is unsupported. Graphical and connected multiplayer gates remain open. This refinement does not expand the six automatic ability kinds or grant automatic consent.

**0.22.0 - Character and Company is installed locally**, not a verified public update. Company/Collect share attention and Always visits can coexist with personal questions. Details/V separates Conversation, Equipment and Diagnostics; tool displays are capabilities, not generic armor. Explicit debug export is read-only. The later genuine Classic run and 20 unaltered captures are recorded in tests/LIVE-SHOWCASE-0.22.0-2026-10-05.md, including failed cargo controls and incomplete critter coverage. Installation is separate in tests/CLIENT-INSTALL-0.22.0-2026-10-05.md.

**0.22.1 supersedes the earlier candidates.** It repairs hidden Equipment initialization and event-coordinate withdrawal, question fading, Soulcore icon animation and diagnostic version/rejection labels, with corner-minimap placement checks. tests/LIVE-SHOWCASE-REPAIRS-0.22.1-2026-10-05.md preserves the failed baseline and repeated checks; tests/CLIENT-INSTALL-0.22.1-2026-10-06.md records installation. The historical candidate descriptions above refer to their preparation passes. Their implemented refinements are included in this release, not separate mod packages. Live critter/pet, connected multiplayer, progression and endurance gates remain open.

## Current Core Scope

0.24.0 includes the historical repairs above, the pet inventory and the requested small intent/reward refinement. All peers require 0.24.0. Steam records its Workshop update October 9 at 14:43 CEST; GitHub's beta release followed at 14:47 CEST. Steam moderation is still pending at the publication check. Upload does not establish subscriber delivery. Earlier preparation states are historical, not current installation instructions.

- Reusable Soulcore creation, unique Sigils, persistent identity and progression; exactly one active companion per player.
- Native-first input, separate Player/Companion Soulwheels, captured world contexts, item topics, the existing Games branch and mailbox.
- Bounded follow/Stay/defense/healing, safe directed and automatic mining, gathering, forestry and treasure inspection.
- Independent Ask/Always/Never permissions for all six registered initiative kinds, token-bound replies and Pause/Resume/Abort.
- Watch, Company and native Collect with the basic net capability, protected creatures, visible greetings and witnessed-loss reactions.
- Separate equipment/resources/wallet, capacity rules, exact transfer conservation and acknowledged multiplayer inventory conflicts.
- Local learning, bounded memories and resident relationships, emote expressions, personal answers and optional local feedback.
- Nine complete key catalogs. English/German usability and terminology need client acceptance; other translations need native-speaker feedback. Server-language response limitations remain disclosed.

## Release gates

| Gate | Evidence required | Current status |
| --- | --- | --- |
| Production build | MSBuild and native package compiler without warnings/errors; exact package identity | 0.24.0 passes: 83 source hashes, 94,235 native assertions, 198 independent expectations and 55,855 static/localization assertions; see tests/INTENT-LEARNING-0.24.0-2026-10-09.md |
| Data safety | Mixed cargo, wallet, legacy saves, clone and binary transport; rejected and duplicate transactions | Automated pass, including the hand-written 0.19.9 wire fixture; graphical upgrade/reload still pending |
| Behavior control | Priority combinations, instance isolation, consent, pause/resume/abort, recovery, target replacement and recall | Automated pass; visual timing and perceived usefulness still pending |
| Single-player | Fresh Classic character/world plus an upgraded existing Sigil; every primary control and three save/reload cycles | Genuine 0.22.0 Classic showcase plus creator's positive 0.22.2/0.23.2 and first 0.24.0 playtests; full action coverage and reload cycles remain pending |
| Critters and forestry | Visible greeting/loss, Company/Collect, Off/recall, pause/resume; normal/snow/palm/gem/mushroom tree contexts and supported actions | Expired-spawn rejection reproduced and repaired in 0.22.2; normal NPC/projectile updates and creator playtest pass. Individual live loss timing, tree families and every pet combination still need focused coverage |
| Connected multiplayer | Two real clients in host-and-play and a dedicated server; switching, simultaneous pickup, transactions, disconnect/reconnect | Pending; disconnected packet fixtures are not this test |
| Progression and pacing | Early tools, stronger tools and hardmode; protected terrain; resource limits at levels 1/10/20; work and recovery | Native boundary fixtures pass; actual progression session pending |
| Interface | Creator, every wheel branch, answer selection, pack, mailbox typing, native right-click and speech/emotes at common UI scales | Layout/input fixtures pass; physical V key reported failing and not certified fixed. Use Details/Pets. Rendered 800x600, 1280x720 and 1920x1080 checks pending |
| Endurance | At least one 60-minute single-player and one 60-minute connected multiplayer session with saved/reloaded cargo | Pending; accelerated AI ticks are not a substitute |
| Public delivery | Maintainer approves candidate; one current Workshop log, matching Git release, verified subscriber delivery | Creator approves 0.24.0. Steam records the update October 9 at 14:43 CEST; GitHub beta followed at 14:47 CEST. Its asset is aligned to the separately retested Workshop download; the earlier installed candidate stays separately identified. Steam moderation and per-subscriber delivery remain separate checks |

A failed acceptance gate remains open until reproduced, repaired and retested. Passing older versions or another package's hash does not close a gate. Freeze new functionality while these checks are being completed.

## Recording a playtest

Record package version/hash, loader version, mode, tested action, expected result, observed result and save/reload outcome. Use real unaltered screenshots for visual evidence. Optional local Field Notes record the selected activity lane and safe context; typed bug notes remain separate. Do not publish raw private notes or player/world saves.

## Module boundaries

CompanionAbilityRegistry owns the six ability definitions, existing permission tags, UI rule actions, task symbols and stamina thresholds. Profile clone/save/load/network operations use that registry. New abilities must explicitly register a kind, activity code, policy, key and native symbols, implement target validation and execution, and extend the compatibility/consent tests. Changing binary fields requires a deliberate versioned protocol change, not merely another registry entry.

CompanionActivityCoordinator owns ordered dispatch: Defense, Paused, Assignment, Critters, AutomaticWork, Residents, Stay, AwaitingReply, Follow. It selects at most one claiming task/movement module per tick, without recreating or cancelling its retained state. A finished or cancelled task owns its last movement frame. Explicit commands remain responsible for cancellation and cleanup.

The NPC activity adapters still share existing fields in the partial class; individual work implementations are not independently pluggable yet. Passive clocks, observations, approved nearby pickup and consumable-use checks remain before task dispatch. The coordinator is not a claim that every side effect is serialized into one world action per frame. Keeping those limitations explicit avoids another disruptive rewrite during stabilization.
