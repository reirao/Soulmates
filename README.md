# Soulmates

Soulmates is a tModLoader mod about creating companions that feel personal. The reusable **Soulcore** opens a small companion creator. Each finished companion is stored in its own **Soulbound Sigil** with a persistent name, appearance, personality, talent, bond, mood, and energy.

![Soulmates feature overview](media/soulmates-overview.webp)

## In-game gallery

| Soul Creator | Living companion |
| --- | --- |
| ![Soul Creator with animated live preview](media/soul-creator.webp) | ![A Soulkin companion beside its player](media/living-companion.webp) |

| Talk Mode | Work orders |
| --- | --- |
| ![Talk Mode showing mood, energy and bond](media/talk-mode.webp) | ![Work orders for treasure, ore and gathering](media/work-orders.webp) |

| Companion pack | Emergent dialogue |
| --- | --- |
| ![Persistent companion pack](media/companion-pack.webp) | ![A companion speaking during normal play](media/emergent-dialogue.webp) |

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
- Companion identity and progression data are saved on the Sigil.
- Personality-driven idle, wander, inspect, follow, and catch-up behavior.
- Persistent per-companion autonomy, switchable from the action wheel.
- Talent-driven initiative: every companion retrieves nearby usable drops, Gatherers do so faster and farther, Miners help in short bursts and collect what they mine, Treasure Seekers investigate nearby chests, Guardians intercept danger, and Healers react to injuries.
- Persistent learning insights adapt to the owner's gathering, mining, forestry, combat, and exploration habits.
- All vanilla and modded ore families are recognized; companions remember safe natural materials they watch the owner mine and reuse discovered pickaxes for later mining assignments.
- The Work wheel supports both area assignments and precise cursor orders: point at one ore/material cluster or one loose world item and send the companion there directly.
- Opening the mining target reveals nearby ore families as their real Terraria item icons; choose one immediately or keep the pickaxe node for a manual world target.
- Choose a persistent mining approach per companion: Adaptive priorities, a narrow Tunnel to ore or the next opening, ore-only Vein work, or exposed-only Surface work. Unstable sand-like materials are skimmed only from open edges.
- Useful pack contents matter in the world: weapons strengthen companion attacks, torches add light, and food or recovery items can be used at an appropriate moment.
- Near a crafting opportunity, a companion may occasionally suggest a recipe inspired by materials in its pack.
- Contextual initiative prompts let you answer opportunities with Terraria emotes: Yes, No, Always, or Never, remembered separately for gathering, mining, forestry, and treasure hunting.
- The learned Forester perk lets a companion shake trees, clear natural fallen logs, collect seeds, and carefully replant carried acorns.
- A companion named **AETHER** is an Omni Soul with every starting talent instinct and learned perk available.
- Personality-driven autonomous moments add small surprises without overriding combat, Stay, explicit assignments, or low-energy recovery.
- Persistent companion experience with 20 levels earned from combat, healing, work, and shared moments.
- Use the compact Companion Soulwheel for commands, work, bonding, direct pack access, and details.
- Right-click your own character, or press the configurable **Player Emote Wheel** key (`G` by default), to open the separate wheel with all 151 vanilla Terraria emotes. Categories unfold into crescent sub-wheels instead of another window.
- Native Terraria emote bubbles for shared gestures, with occasional context-aware speech instead of constant text.
- Quiet social encounters with nearby town NPCs: companions approach, emote, and receive a native NPC response.
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
- Every companion has a persistent 8-slot pack with a practical carry reserve of 99 per item type and 12 acorns; the Hearth Ribbon expands it to 12 slots.
- The Pack conversation can inspect cargo, store the selected hotbar item, or unload everything.
- Pack slots have item tooltips and return a full stack on left-click or one item on right-click.
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

## Current release

**Soulmates 0.12.1 - Clear Voices** fixes missing English/German action texts and initialization-time localization glitches. Named speech bubbles now follow their companion rather than the player, keep a stable side while speaking, and retain a restrained fading trail. Talk Mode has readable numeric mood/energy values, a full-width level bar, and contained response/pack regions. Shared UI coordinates keep world targets and wheel hitboxes consistent across game zoom and UI scaling. The four mining approaches remain; changed terrain is rechecked before mining, blocked tunnels no longer fall back to unrelated material, and completion reports describe the finished plan rather than claiming an empty area.

The maturity gates for Alpha, Beta, release candidate, and 1.0 are defined in [VERSIONING.md](VERSIONING.md). The former `9.9.x` values were experimental build counters and remain in the changelog only as project history.

## How Soulmates got here

Soulmates began as a reusable item for shaping a personal pet. Repeated in-game passes turned that small idea into persistent Sigils, companion personalities, combat support, work assignments, a real pack, learned behavior, forestry, initiative choices, native emotes, and the compact Soulwheel. Several of those systems were rebuilt more than once when real play exposed awkward controls, disappearing cargo, duplicate drops, stale abilities, or behavior that looked clever but did not remain coherent over a longer session. The complete sequence is preserved in [changelog.txt](changelog.txt); this page describes the implemented AETHER-EGG Alpha state of 0.12.1.

## Playtesting and feedback

Every report helps. The creator has limited time to play and Terraria progression takes time, so some combinations and long-session behavior are necessarily discovered slowly. If something behaves strangely, feels unclear, or breaks later in a playthrough, please describe what happened and whether it was single-player or multiplayer. And if a companion simply made you smile, we are just as happy to hear that.

Please share findings through the [Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3807130821) or [GitHub issues](https://github.com/reirao/Soulmates/issues).

For local playtests, open the paper-plane icon on the Companion Soulwheel or enter `/soulfeedback` for the in-game **AETHER Mailbox**. It can enable the opt-in Field Notes, send a normal note, or preserve a bug report with recent context. Files are stored under tModLoader's save folder in `SoulmatesFeedback`: a bounded JSONL session journal, `latest-summary.json`, and `bug-inbox.jsonl`. The journal deliberately excludes account, player, character, world, chat, and exact position names, and never uploads anything. Use `/soulfeedback off` to stop future recording; existing notes remain yours until you remove them.

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
