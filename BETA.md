# Soulmates beta acceptance

## Current decision

**0.20.0 - Shared Instincts is a published beta candidate.** Its number starts the beta stabilization line; it does not certify the outstanding playtests below. The normal-client installation was verified on October 5, 2026. The creator then reported a broadly working first playtest and Workshop publication. That report does not close individual acceptance gates or establish universal subscriber delivery. The existing maturity criteria in VERSIONING.md still apply.

The core feature scope is frozen. Work now focuses on correctness, balance, usability and compatibility. Additional minigames, new pet slots, combat critter swarms, procedural art and wider autonomous systems are not required for this beta and are not promised for 1.0.

## Frozen core scope

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
| Production build | MSBuild and native package compiler without warnings/errors; exact package identity | Automated pass recorded in tests/BETA-STABILIZATION-0.20.0-2026-10-05.md |
| Data safety | Mixed cargo, wallet, legacy saves, clone and binary transport; rejected and duplicate transactions | Automated pass, including the hand-written 0.19.9 wire fixture; graphical upgrade/reload still pending |
| Behavior control | Priority combinations, instance isolation, consent, pause/resume/abort, recovery, target replacement and recall | Automated pass; visual timing and perceived usefulness still pending |
| Single-player | Fresh Classic character/world plus an upgraded existing Sigil; every primary control and three save/reload cycles | Creator reports a broadly working first pass; action-by-action evidence and reload cycles remain pending |
| Critters and forestry | Visible greeting/loss, Company/Collect, Off/recall, pause/resume; normal/snow/palm/gem/mushroom tree contexts and supported actions | Native fixtures pass; the reported missed live death reaction needs reproduction or visual verification |
| Connected multiplayer | Two real clients in host-and-play and a dedicated server; switching, simultaneous pickup, transactions, disconnect/reconnect | Pending; disconnected packet fixtures are not this test |
| Progression and pacing | Early tools, stronger tools and hardmode; protected terrain; resource limits at levels 1/10/20; work and recovery | Native boundary fixtures pass; actual progression session pending |
| Interface | Creator, every wheel branch, answer selection, pack, mailbox typing, native right-click and speech/emotes at common UI scales | Layout/input fixtures pass; rendered 800x600, 1280x720 and 1920x1080 checks pending |
| Endurance | At least one 60-minute single-player and one 60-minute connected multiplayer session with saved/reloaded cargo | Pending; accelerated AI ticks are not a substitute |
| Public delivery | Maintainer approves candidate; one current Workshop log, matching Git release, verified subscriber delivery | Workshop upload at 11:46 CEST on October 5 is visible; Steam moderator approval is pending at this documentation update; per-subscriber delivery remains a separate check |

A failed acceptance gate remains open until reproduced, repaired and retested. Passing older versions or another package's hash does not close a gate. Freeze new functionality while these checks are being completed.

## Recording a playtest

Record package version/hash, loader version, mode, tested action, expected result, observed result and save/reload outcome. Use real unaltered screenshots for visual evidence. Optional local Field Notes record the selected activity lane and safe context; typed bug notes remain separate. Do not publish raw private notes or player/world saves.

## Module boundaries

CompanionAbilityRegistry owns the six ability definitions, existing permission tags, UI rule actions, task symbols and stamina thresholds. Profile clone/save/load/network operations use that registry. New abilities must explicitly register a kind, activity code, policy, key and native symbols, implement target validation and execution, and extend the compatibility/consent tests. Changing binary fields requires a deliberate versioned protocol change, not merely another registry entry.

CompanionActivityCoordinator owns ordered dispatch: Defense, Paused, Assignment, Critters, AutomaticWork, Residents, Stay, AwaitingReply, Follow. It selects at most one claiming task/movement module per tick, without recreating or cancelling its retained state. A finished or cancelled task owns its last movement frame. Explicit commands remain responsible for cancellation and cleanup.

The NPC activity adapters still share existing fields in the partial class; individual work implementations are not independently pluggable yet. Passive clocks, observations, approved nearby pickup and consumable-use checks remain before task dispatch. The coordinator is not a claim that every side effect is serialized into one world action per frame. Keeping those limitations explicit avoids another disruptive rewrite during stabilization.
