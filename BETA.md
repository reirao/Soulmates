# Soulmates beta acceptance

## Current decision

**0.28.1 - A Little Flock is the current beta.** [Installed locally October 12](tests/CLIENT-INSTALL-0.28.1-2026-10-12.md). Matching vanilla pet items stack in her existing Pet bag; one separated harmless familiar per owned unit, twelve stack slots and 64 total pet items. Modded items keep native stacking rules. The exact installed package passes 167,156 native assertions, 519 independent audit expectations and 62,631 static/catalog assertions, with 1,070 keys in nine languages. [Release notes](releases/0.28.1.md) and [checks](tests/STACKED-PETS-0.28.1-2026-10-12.md). No new graphical, physical-input, connected-multiplayer or endurance acceptance is claimed. Upload, moderation and subscriber delivery remain separate.

**Development pause:** no new feature work or promised next release while the creator plays and gathers feedback. This is not full-release certification. Reports and translation corrections remain welcome; no automatic monitoring or uploads are enabled. Historical publication and installation reports retain their original dates and scope.

## Previous Publication

**0.28.0 - Careful Finds:** consent-first bounded salvage, conservative persistent construction protection, individual critter choices, temporary scorpion company and queued catch receipts. Unknown old objects are not proven natural; Company is not a persistent named-animal relationship simulation. Its Steam-managed package passed 117,074 native assertions and 516 audit expectations. See the [publication receipt](tests/PUBLICATION-0.28.0-2026-10-12.md).

The creator reports a positive playtest of the [installed 0.26.0 candidate](tests/CLIENT-INSTALL-0.26.0-2026-10-11.md) and requests publication. Its installation receipt verifies the file and preserved saves; the playtest is encouraging, not exhaustive graphical or public-delivery acceptance.

**0.26.0 - Little Company is the previous published beta.** It adds bounded personal idle undertakings and a real-item Retinue inventory embedded in the existing pet panel. AETHER may select all distinct gifted staff types; ordinary companions unlock 1/2/3 at levels 1/10/20. Combat uses native artwork with companion-owned behavior rather than player buffs or vanilla minion AI. Completed undertakings enter existing stories and evaluations. The [candidate report](tests/LITTLE-COMPANY-0.26.0-2026-10-11.md) records native regression, independent audit and nine-language checks. Graphical, physical input, two-client multiplayer and endurance acceptance remain open. Upload, Steam approval and subscriber delivery are separate checks.

**Historical 0.25.1 - Home Within Reach:** manual Home route, home/EVIL work and multiplayer compass repairs. See its [wiring report](tests/HOME-WIRING-0.25.1-2026-10-11.md) and [publication receipt](tests/PUBLICATION-0.25.1-2026-10-11.md); they do not certify 0.26.0 delivery.

**0.25.0 - A Place to Belong is a local beta candidate.** This preparation does not install a package, publish a GitHub release or update Workshop. [Release notes](releases/0.25.0.md) describe conservative home recognition, optional EVIL presentation and focused stabilization. MSBuild and native package compilation pass with zero warnings/errors; native regression and static/localization checks pass. All nine catalogs contain 991 complete keys. The independent audit also passes; exact package evidence is recorded in the [beta-depth report](tests/BETA-DEPTH-0.25.0-2026-10-11.md). No assertion totals, package hash or new playtest approval are claimed here. All peers testing the candidate require 0.25.0.

**V remains unverified in real play.** The player reported that it did not work in the real 0.23.1 client. Trigger injection established the handler, not physical keyboard acceptance. The Sigil reports its actual binding; use her wheel's Details/Pets entries. The [V key lesson](README.md#the-v-key-lesson) preserves the withdrawn claim. Earlier positive general playtests do not close this specific gate.

## Current scope

- Reusable Soulcore, unique Sigils, persistent identity, growth and exactly one active companion per player.
- Native-first Player/Companion wheels, protected input, captured targets and replies bound to the current companion/Sigil. Answer-wheel nodes fade at fixed hit positions.
- Safe directed and automatic gathering, mining, forestry and treasure inspection, with live-target checks, bounded timeouts, mood/energy recovery and real threats ahead of training dummies.
- Independent Ask/Always/Never permissions for all six registered initiative kinds, token-bound replies and Pause/Resume/Abort. Learning preferences do not grant permission.
- Salvage needs concrete single-object or bounded-area permission, never Always demolition. Unknown origins require an additional area confirmation; observed construction and conservative surroundings checks protect builds. Individual Collect encounters still ask even under Always. Native catches enter Pack, not Pets; both scorpion species can join temporary Company.
- Bounded place memory per Sigil and world: enclosed furnished rooms, an actual valid selected bed and assigned resident home anchors provide distinct evidence. Separated returns establish familiarity, never ownership. Standing still is not repeated return evidence.
- Home on her wheel opens the existing four-answer place conversation directly, even at Stay, with autonomy off or Quiet. Manual classification/reclassification needs no prior visits; automatic offers require familiarity. SharedHome, CompanionHome and NotHome assign roles; Later leaves them unchanged. Resident housing and unanswered questions are respected. Replies recheck world/room. Home chests are excluded from automatic rediscovery; explicit treasure search remains. No autonomous construction/remodeling.
- Open-ended exact XP and levels preserve the original curve; resource/ordinary retinue balance remains bounded at level 20. Old saves migrate without losing earned XP.
- EVIL is optional, off by default and saved per Sigil. The direct angry-face root, Commands or Details toggles normal theatrical presentation. Games separately offers a confirmed owner-only boss duel with real character damage, three phases, no loot/XP or permanent companion death. Abort/Stop, EVIL off, recall, owner loss, leash, timeout or another boss ends it; shots and debuffs clear, inventory survives and work stays paused until Resume.
- Watch, Company and native Collect retain net, protected-creature, distance, visibility, recovery and consent gates. Catching and death stay distinct; live visits and queued reactions reject stale creatures.
- Separate equipment/resources/wallet, conserved real-item transfers and twelve pet-stack slots for 64 owned units. One harmless familiar per unit validates its item/copy binding before drawing, with separated formation and native art/custom movement, not arbitrary player-pet AI or modded drawing. Native player inventory limits remain unchanged.
- Bounded learning, memories and resident relationships; nine complete language catalogs. Some multiplayer replies still use the server language; AI-authored translations need native-speaker feedback.
- Optional local Mailbox/Field Notes start off. Automatic notes omit names, chat and coordinates; typed notes retain their text. Place coordinates stay in the local game save. Nothing uploads automatically or reaches the developer without sharing. AI-generated original visuals and reused Terraria assets remain disclosed in [README](README.md#ai-assisted-development-disclosure).

## Release gates

| Gate | Evidence required | Current status |
| --- | --- | --- |
| Production build | Warning-free production build and exact package identity | Exact installed 0.28.1 has zero production warnings/errors; 167,156 native assertions, 519 independent expectations and 62,631 static/catalog assertions pass. See [0.28.1 checks](tests/STACKED-PETS-0.28.1-2026-10-12.md). Rebuilt Workshop binaries require separate verification. |
| Data safety | Mixed cargo/wallet, legacy saves, clone/transport, rejected/duplicate transfers, separate world/Sigil place roles | Native regression passes; independent audit passes. Coverage belongs in the beta-depth report. Real upgrade/reload acceptance remains open. |
| Behavior control | Consent, priority, recovery, timeout, stale targets/replies, pause/resume/abort and recall | Native regression passes; independent audit passes. Coverage belongs in the beta-depth report. Live timing and perceived usefulness remain open. |
| Places and EVIL | Room/actual-bed/resident distinctions, separated returns, all four home answers, changed room/world, automatic versus explicit treasure; persona off/on and fair RPS | Native regression passes; independent audit passes. Rendered answers, natural pacing and presentation remain open. |
| Single-player | Fresh Classic world/character and upgraded Sigil, every primary control and repeated save/reload | Creator reports a delighted installed 0.27.0 playtest. This is not exhaustive GUI, balance or upgrade/reload acceptance; no new agent-controlled graphical session is claimed. |
| Critters, forestry and pets | Visible greetings/loss, Company/Collect, Off/recall, pause, tree families and familiar visuals | Focused candidate checks do not certify every rendered family or live reaction. Client acceptance remains open. |
| Connected multiplayer | Two real clients, host-and-play/dedicated sessions, switching/transfers/reconnect | Open. Packet fixtures are not connected-client acceptance. All peers need 0.28.0. |
| Progression and pacing | Early tools through hardmode, protected terrain, capacity and work/recovery in actual play | Open. Controlled boundary fixtures do not replace a progression session. |
| Interface and physical keys | Creator, wheels, answers, cargo, typing and native interactions at common UI scales; physical shortcut | Real GUI acceptance remains open, including 800x600, 1280x720 and 1920x1080. V is unverified; use Details/Pets. |
| Endurance | A 60-minute single-player and a 60-minute connected multiplayer session, with saved/reloaded cargo | Open. Accelerated AI ticks do not replace these sessions. |
| Installation and public delivery | Separate install, upload, public package identity, approval and subscriber checks | Candidate installed with backups; Workshop upload recorded October 12 at 14:18 CEST. Steam-managed package independently retested. Moderation/subscriber delivery remain open; see [0.28.0 release](releases/0.28.0.md). |

A failed gate remains open until reproduced, repaired and retested. Older passes and package hashes do not close a candidate gate. These docs describe implemented scope, not a future feature roadmap.

## Historical evidence

**0.24.0 - Learning Together** was installed locally on October 9, 2026, followed by the creator's positive first playtest and publication approval. Steam recorded its Workshop update at 14:43 CEST; the GitHub beta followed at 14:47 CEST. The direct Workshop download was separately checked and retested; GitHub distributed that exact public package. The earlier installed candidate has a different package/assembly hash, with matching production sources and catalog values. Steam moderation was pending at the historical publication check, and subscriber delivery remained separate.

Preserved records: [0.24.0 release notes](releases/0.24.0.md), [learning checks](tests/INTENT-LEARNING-0.24.0-2026-10-09.md), [installation receipt](tests/CLIENT-INSTALL-0.24.0-2026-10-09.md) and [publication verification](tests/PUBLICATION-0.24.0-2026-10-09.md). Those receipts retain their original counts and hashes; none establishes a 0.25.0 install, test result or public delivery.

Earlier evidence remains in the [0.23.2 notes](releases/0.23.2.md), [0.22.2 critter repair report](tests/LIVING-CRITTERS-0.22.2-2026-10-08.md), [0.22.2 installation receipt](tests/CLIENT-INSTALL-0.22.2-2026-10-08.md), [genuine 0.22.0 Classic showcase](tests/LIVE-SHOWCASE-0.22.0-2026-10-05.md) and [0.22.1 showcase repairs](tests/LIVE-SHOWCASE-REPAIRS-0.22.1-2026-10-05.md). Version-specific pet restrictions and preparation states stay historical. [CHANGELOG.md](CHANGELOG.md) retains the development sequence; [VERSIONING.md](VERSIONING.md) defines maturity gates.

## Recording a playtest

Record package version/hash, loader version, mode, action, expected result, observed result and save/reload outcome. Use real unaltered screenshots for visual evidence. Optional local Field Notes and typed bug notes remain private until explicitly shared. Do not publish raw private notes or player/world saves.

## Module boundaries

CompanionAbilityRegistry owns the six ability definitions, permission tags, UI rule actions, task symbols and stamina thresholds. Profile clone/save/load/network operations use that registry. Place roles and EVIL presentation do not add an automatic ability or permission. Binary profile changes require matching peer versions and explicit compatibility checks.

CompanionActivityCoordinator dispatches Defense, Paused, Assignment, Critters, AutomaticWork, Residents, Stay, AwaitingReply and Follow. It selects at most one claiming task/movement module per tick; explicit commands own cancellation and cleanup. Activity adapters still share fields in the NPC partial class. Passive clocks, observations, approved nearby pickup and consumable checks remain before dispatch. This is not a fully pluggable behavior engine or a guarantee that every side effect becomes one world action per frame.
