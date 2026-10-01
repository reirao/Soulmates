# Soulmates

Soulmates is a tModLoader mod about creating companions that feel personal. The reusable **Soulcore** opens a small companion creator. Each finished companion is stored in its own **Soulbound Sigil** with a persistent name, appearance, personality, talent, bond, mood, and energy.

## AI-assisted development disclosure

Soulmates is an experimental project created with extensive AI assistance across programming, writing, interface work, and visual development. The Workshop icon, mod icon, and custom Soulkin sprite were AI-generated and then integrated into the mod. Bunny, Blue Slime, Bird, Squirrel, item, inventory, and emote visuals reuse Terraria assets. The released build was reviewed and tested in-game by the creator.

## Features

- Craft a reusable Soulcore and Blank Sigils.
- Receive one Soulcore and three Blank Sigils once per character as a starter kit.
- Create multiple unique companions from fully localized presets.
- Shape a companion through an animated live preview with an original eight-frame Soulkin sprite, including compatibility for existing Sigils.
- Choose a name, form, essence color, aura, personality, and one of five visible starting-talent classes.
- Choose a visual Bestiary Muse: Soulkin, Bunny, Blue Slime, Bird, or Squirrel.
- Summon exactly one active companion at a time; switching Sigils cleanly retires the previous companion, assignment, and projectiles.
- Right-click the summoned companion or your own character to open the compact Soulwheel.
- Hold Up and right-click the Sigil to recall it.
- Companion identity and progression data are saved on the Sigil. Keep the active Sigil in the player's inventory; moving it out recalls the companion. In single-player, holding it on the cursor while rearranging inventory preserves the binding.
- Personality-driven idle, wander, inspect, follow, and catch-up behavior.
- Persistent per-companion autonomy, switchable from the action wheel.
- Talent-driven initiative: every companion retrieves nearby usable drops, Gatherers do so faster and farther, Miners help in short bursts and collect what they mine, Treasure Seekers investigate nearby chests, Guardians intercept danger, and Healers react to injuries.
- Persistent learning insights adapt to the owner's gathering, mining, forestry, combat, and exploration habits.
- All vanilla and modded ore families are recognized; companions remember safe natural materials they watch the owner mine and reuse discovered pickaxes for later mining assignments.
- Right-click clear air for a world mode wheel. Right-click again cycles between Point tools and Area work; the native X exits. Tiles, NPCs, inventory clicks, and held-item alternate actions keep their normal interactions.
- Choose Look, Gather, Mine, or Forest to point at nearby targets repeatedly. Right-click an active tool opens the Area wheel; Escape exits targeting. Look reports an opportunity without changing the assignment, cargo, or terrain.
- Opening the mining target reveals nearby ore families as their real Terraria item icons; choose one immediately or keep the pickaxe node for a manual world target.
- Choose a persistent mining approach per companion: Adaptive priorities, a narrow Tunnel to ore or the next opening, ore-only Vein work, or exposed-only Surface work. Unstable sand-like materials are skimmed only from open edges.
- Useful pack contents matter in the world: weapons strengthen companion attacks, torches add light, and food or recovery items can be used at an appropriate moment.
- Near a crafting opportunity, a companion may occasionally suggest a recipe inspired by carried materials.
- Contextual initiative prompts let you answer opportunities with Terraria emotes: Yes, No, Always, or Never. Commands > Initiative Rules also lets you cycle Ask, Always, and Never separately for gathering, mining, forestry, and treasure hunting.
- Nearby opportunities share an attention scheduler weighted by proximity, talent, recent observed habits, and waiting time. Repeated drops cannot indefinitely starve other actionable tasks; cooldowns and unanswered questions affect only their own task kind.
- The learned Forester perk lets a companion shake trees, clear natural fallen logs, collect seeds, and carefully replant carried acorns.
- Initiative answer wheels open at the player; the companion shows the task symbol, then a question mark. Their positions stay still while choosing.
- A companion named **AETHER** is an Omni Soul with every starting talent instinct and learned perk available.
- Give AETHER a bug net in her cargo. Approved forestry can catch nearby natural butterflies, fireflies, and lightning bugs; Forestry > Always also permits idle catches. Up to six carried insects form a cosmetic native-sprite flock, disappearing when withdrawn. No free critters, combat swarm, or extra pet slots are created.
- Personality-driven autonomous moments add small surprises without overriding combat, Stay, explicit assignments, or low-energy recovery.
- Persistent companion experience with 20 levels earned from combat, healing, work, and shared moments.
- Use the compact Companion Soulwheel for commands, work, bonding, direct pack access, and details.
- Right-click your own character for a separate Player wheel with Emotes, Point, and Mailbox buttons. Emotes unfolds all 151 vanilla Terraria emotes in crescent categories. The configurable **Player Emote Wheel** key (`G` by default) opens Emotes directly.
- Native Terraria emote bubbles for shared gestures, with occasional context-aware speech instead of constant text.
- Social encounters include a greeting, a native NPC emote, and a personality-aware reply. Resident relationships remember world, NPC type, and name, with wary, new, familiar, and friend states. Combat and explicit work interrupt the visit.
- Talk Mode > Bond > "Who have you made friends with here?" cycles local resident relationships. Meeting someone and making a friend add chronicle memories; NPC conversation does not generate experience.
- Personality-specific reactions to normal victories, bosses, and creature deaths.
- Talk Mode with Care, Commands, Work, Bond, Voice, and Pack conversations.
- Mood-, energy-, bond-, and personality-aware replies, including clearly explained refusals.
- Five visible bond ranks with growing work radius, role strength, and pack capacity.
- Every companion continuously defends against nearby threats without spending work energy, while staying leashed to its owner or Stay anchor; Guardians react faster, reach farther, and hit harder, while Healers provide energy-limited support.
- Energy returns naturally outside work and combat, with faster recovery while waiting; the Care > Rest conversation is an optional boost rather than a required chore.
- Area assignments: locate nearby chests, clear every reachable ore vein and learned material in range, and retrieve every loose item in range.
- Right-clicking a workbench opens Terraria's inventory and refreshed crafting list as a small quality-of-life interaction.
- A persistent memory chronicle covering creation, work, protection, healing, equipment, and bond milestones.
- Craftable Starfinder Bell, Delver Charm, and Hearth Ribbon companion trinkets.
- Resting companions recover mood and energy over time.
- Cargo has three persistent compartments: Equipment, Resources, and Wallet, selected using Terraria item icons in the Pack view.
- Equipment retains the 8-slot pack, bond upgrades, Hearth Ribbon expansion to 12 slots, and 99-unit reserve per item type.
- Resources have 60 stack slots, displayed in pages of 12. Their reserve grows by 50 units per companion level and item type: level 1 holds 50, level 10 holds 500, and level 20 holds 1,000. Native item stack limits still apply.
- The separate wallet stores collected copper, silver, gold, and platinum as one exact balance with no amount or level cap. Money does not occupy cargo slots or generate free currency.
- The Pack conversation can inspect cargo, store the selected hotbar item in its appropriate compartment, or return cargo and wallet coins to the owner.
- Cargo slots show tooltips; left-click returns a stack and right-click one item. Wallet coin icons withdraw that denomination, converting from the shared balance when needed. A withdrawal is bounded by Terraria's coin stack size, and anything the player's inventory cannot accept stays with the companion.
- Existing Sigils migrate materials and coins into their new compartments. Resources exceeding the companion's level reserve are returned on summon, rather than silently deleted.
- Companions illuminate dark spaces and reveal the nearby world map as they explore.
- Work refusals are deterministic and explain whether mood or energy is too low.
- Active assignments survive recalls and summons, end when the area is clear, and report when the companion needs a new assignment.
- Press the configurable **Talk to Companion** hotkey (`V` by default) to open Talk Mode without selecting the Sigil.
- Choose **Details** on the companion action wheel, or press `V`, to open the resizable Talk Mode with a real level bar, mining approach, bond, mood, energy, pack, memories, and conversations.
- Stay is a true world anchor: companions defend that location without drifting back to the player.
- Play in English or German; Soulmates follows Terraria's selected language automatically.
- Optional local **AETHER Field Notes** record anonymized gameplay events and compact behavior signals for playtesting; nothing is uploaded. Use `/soulfeedback on` to opt in, `/soulfeedback note <text>` for an observation, and `/soulfeedback bug <what happened>` to save a bug with its recent gameplay context.
- Open the in-game **AETHER Mailbox** from the paper-plane icon on the Companion Soulwheel, or with `/soulfeedback`, to send ordinary feedback or a contextual bug report without leaving Terraria.

The current release supports single-player and server-authoritative multiplayer companions. Summoning, recalling, conversations, jobs, pack actions, and trinkets are validated by the server and synchronized back to the owning player.

## Current Build

**Soulmates 0.15.1 - Careful Company** is an AETHER-EGG Alpha maintenance patch. It hardens input ownership, gathering feedback, multiplayer recovery, Sigil persistence, and local Field Notes without adding a new feature tier. See the [0.15.1 release](https://github.com/reirao/Soulmates/releases/tag/v0.15.1), [patch notes](releases/0.15.1.md), and [revision report](tests/REVISION-2026-10-01.md). The [0.15.0 notes](releases/0.15.0.md) describe the existing relationships, emotes, and AETHER flock. Living tree-branch pruning remains deliberately disabled.

The [0.14.0 client report](tests/PLAYTEST-2026-10-01.md) records a bounded real-client control pass and a subsequently fixed click conflict. The creator has begun playing 0.15.0 and confirmed a positive first impression; this is not a complete graphical or progression test. The current gallery uses the creator's own screenshots and unchanged frames from their gameplay video. Older galleries remain removed. Broader progression, long sessions, and live multiplayer still need testing.

The maturity gates for Alpha, Beta, release candidate, and 1.0 are defined in [VERSIONING.md](VERSIONING.md). The former `9.9.x` values were experimental build counters and remain in the changelog only as project history.

Workshop update text comes from [changelog.txt](changelog.txt), which contains only the current update. [CHANGELOG.md](CHANGELOG.md) preserves the complete development history separately so it cannot be accidentally republished in every Workshop entry.

## Actual Gameplay

These are genuine captures supplied by the creator on October 1, 2026, not generated mockups. The selected video frames retain their original 1280 x 720 resolution; the creator's own screenshots retain their original framing and size.

![Soul Creator with the animated Soulkin preview](media/current/01-soul-creator.png)

![Soulkin companion in the world](media/current/02-soulkin-companion.png)

![Native Terraria emotes unfolded from the player wheel](media/current/03-native-emote-wheel.png)

See the [complete current gallery](media/current/README.md) for Talk Mode, the mailbox, world tools, mining initiative answers, and more gameplay moments. The [screenshot bundle](https://github.com/reirao/Soulmates/releases/download/v0.15.0/Soulmates-screens.zip) contains all 17 unchanged PNGs with source timestamps for the video frames.

## How Soulmates got here

Soulmates began as a reusable item for shaping a personal pet. Repeated in-game passes added persistent Sigils, personalities, combat support, work, cargo, learned behavior, forestry, initiative choices, native emotes, and the Soulwheel. Several systems were rebuilt when real play exposed awkward controls, disappearing cargo, duplicate drops, or stale abilities. The full sequence is preserved in [CHANGELOG.md](CHANGELOG.md); this page describes the implemented AETHER-EGG Alpha state without promising future features.

## Playtesting and feedback

Every report helps. The creator has limited time to play and Terraria progression takes time, so some combinations and long-session behavior are necessarily discovered slowly. If something behaves strangely, feels unclear, or breaks later in a playthrough, please describe what happened and whether it was single-player or multiplayer. And if a companion simply made you smile, we are just as happy to hear that.

Please share findings through the [Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3807130821) or [GitHub issues](https://github.com/reirao/Soulmates/issues).

For local playtests, open the paper-plane icon on the Companion Soulwheel or enter `/soulfeedback` for the in-game **AETHER Mailbox**. It can enable the opt-in Field Notes, send a normal note, or preserve a bug report with recent context. Files are stored under tModLoader's save folder in `SoulmatesFeedback`: a bounded JSONL session journal, `latest-summary.json`, and `bug-inbox.jsonl`. New automatic records exclude account, player, character, and world names, chat text, and exact coordinates. Manually typed notes retain their text, up to 360 characters, so avoid including anything you do not want stored. Nothing is uploaded. Older logs are not rewritten. Use `/soulfeedback off` to stop future recording; existing notes remain yours until you remove them.

## Languages

- English
- German / Deutsch

English is used as the fallback when Terraria is set to another language.

## Install for development

1. Install Terraria and tModLoader through Steam.
2. Copy or clone this folder into `Documents/My Games/Terraria/tModLoader/ModSources/Soulmates`.
3. Open tModLoader and choose **Workshop > Develop Mods > Build + Reload**.

The project targets the current tModLoader 1.4.4 stable release. In the normal ModSources folder the project automatically uses `tModLoader.targets`. For development elsewhere, set `TML_PATH` to a tModLoader installation containing `tMLMod.targets`.

## Recipes

- **Soulcore:** 8 Fallen Stars, 5 Amethyst, and 10 Stone Blocks at a Work Bench.
- **Blank Sigil:** 3 Fallen Stars, 1 Amethyst, and 5 Silk at a Work Bench.
- **Starfinder Bell:** 5 Fallen Stars, 3 Iron or Lead Bars, and 1 Lens at an Anvil.
- **Delver Charm:** 5 Iron or Lead Bars, 2 Amethyst, and 20 Stone Blocks at an Anvil. Expands mining radius and speed.
- **Hearth Ribbon:** 5 Silk, 2 Daybloom, and 2 Fallen Stars at a Work Bench.

## License

MIT
