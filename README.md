# Soulmates

Soulmates is a tModLoader mod about creating companions that feel personal. The reusable **Soulcore** opens a small companion creator. Each finished companion is stored in its own **Soulbound Sigil** with a persistent name, appearance, personality, talent, bond, mood, and energy.

## AI-assisted development disclosure

Soulmates is an experimental project created with extensive AI assistance across programming, writing, interface work, and visual development. The Workshop icon, mod icon, and custom Soulkin sprite were AI-generated and then integrated into the mod. The current Bunny, Blue Slime, Bird, Squirrel, item, and inventory visuals reuse Terraria assets as placeholders. Features are reviewed and tested in-game by the creator before release.

## Features

- Craft a reusable Soulcore and Blank Sigils.
- Receive one Soulcore and three Blank Sigils once per character as a prototype starter kit.
- Create multiple unique companions from fully localized presets.
- Shape a companion through an animated live preview.
- Choose a name, form, essence color, aura, personality, and starting talent.
- Choose a visual Bestiary Muse: Soulkin, Bunny, Blue Slime, Bird, or Squirrel.
- Summon one active companion at a time.
- Right-click an active companion's Sigil to enter Talk Mode.
- Hold Up and right-click the Sigil to recall it.
- Companion identity and progression data are saved on the Sigil.
- Personality-driven idle, wander, inspect, follow, and catch-up behavior.
- Talk Mode with Care, Commands, Work, Bond, Voice, and Pack conversations.
- Mood-, energy-, bond-, and personality-aware replies, including clearly explained refusals.
- Five visible bond ranks with growing work radius, role strength, and pack capacity.
- Autonomous Guardian combat and energy-limited Healer support.
- Area assignments: locate nearby chests, clear every early-ore vein in range, and retrieve every loose item in range.
- A persistent memory chronicle covering creation, work, protection, healing, equipment, and bond milestones.
- Craftable Starfinder Bell, Delver Charm, and Hearth Ribbon companion trinkets.
- Resting companions recover mood and energy over time.
- Every companion has a persistent 8-stack pack; the Hearth Ribbon expands it to 12.
- The Pack conversation can inspect cargo, store the selected hotbar item, or unload everything.
- Pack slots have item tooltips and return a full stack on left-click or one item on right-click.
- Companions illuminate dark spaces and reveal the nearby world map as they explore.
- Work refusals are deterministic and explain whether mood or energy is too low.
- Active assignments survive recalls and summons, end when the area is clear, and report when the companion needs a new assignment.
- Press the configurable **Talk to Companion** hotkey (`V` by default) to open Talk Mode without selecting the Sigil.
- Right-click the summoned companion to open Talk Mode directly.
- Play in English or German; Soulmates follows Terraria's selected language automatically.

The current release supports single-player and server-authoritative multiplayer companions. Summoning, recalling, conversations, jobs, pack actions, and trinkets are validated by the server and synchronized back to the owning player.

## Languages

- English
- German / Deutsch

English is used as the fallback when Terraria is set to another language.

## Install for development

1. Install Terraria and tModLoader through Steam.
2. Copy or clone this folder into `Documents/My Games/Terraria/tModLoader/ModSources/Soulmates`.
3. Open tModLoader and choose **Workshop > Develop Mods > Build + Reload**.

The project targets the current tModLoader 1.4.4 stable release. In the normal ModSources folder the project automatically uses `tModLoader.targets`. For development elsewhere, set `TML_PATH` to a tModLoader installation containing `tMLMod.targets`.

## Prototype recipes

- **Soulcore:** 8 Fallen Stars, 5 Amethyst, and 10 Stone Blocks at a Work Bench.
- **Blank Sigil:** 3 Fallen Stars, 1 Amethyst, and 5 Silk at a Work Bench.
- **Starfinder Bell:** 5 Fallen Stars, 3 Iron or Lead Bars, and 1 Lens at an Anvil.
- **Delver Charm:** 5 Iron or Lead Bars, 2 Amethyst, and 20 Stone Blocks at an Anvil. Expands mining radius and speed.
- **Hearth Ribbon:** 5 Silk, 2 Daybloom, and 2 Fallen Stars at a Work Bench.

## Roadmap

- Custom pixel art and layered companion bodies.
- More trinkets and meaningful equipment tradeoffs.
- Advanced jobs for later ores and specialized materials.
- Evolving traits, contextual dialogue, gifts, preferences, and personal quests.

## License

MIT
