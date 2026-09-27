# Soulmates

Soulmates is a tModLoader mod about creating companions that feel personal. The reusable **Soulcore** opens a small companion creator. Each finished companion is stored in its own **Soulbound Sigil** with a persistent name, appearance, personality, talent, bond, mood, and energy.

## Features

- Craft a reusable Soulcore and Blank Sigils.
- Receive one Soulcore and three Blank Sigils once per character as a prototype starter kit.
- Create multiple unique companions from English-language presets.
- Shape a companion through an animated live preview.
- Choose a name, form, essence color, aura, personality, and starting talent.
- Choose a visual Bestiary Muse: Soulkin, Bunny, Blue Slime, Bird, or Squirrel.
- Summon one active companion at a time.
- Right-click an active companion's Sigil to enter Talk Mode.
- Hold Up and right-click the Sigil to recall it.
- Companion identity and progression data are saved on the Sigil.
- Personality-driven idle, wander, inspect, follow, and catch-up behavior.
- Talk Mode with Care, Commands, Work, Bond, and Voice conversations.
- Mood-, energy-, bond-, and personality-aware replies, including occasional refusals.
- Real companion jobs: locate nearby chests, mine a small batch of stone or early ore, and retrieve loose items.
- Persistent job memories and completed-job history on each Soulbound Sigil.
- Craftable Starfinder Bell, Delver Charm, and Hearth Ribbon companion trinkets.
- Resting companions recover mood and energy over time.
- Every companion has a persistent 8-stack pack; the Hearth Ribbon expands it to 12.
- The Pack conversation can inspect cargo, store the selected hotbar item, or unload everything.
- Companions illuminate dark spaces and reveal the nearby world map as they explore.
- Work refusals are deterministic and explain whether mood or energy is too low.

The current release is designed and tested for single-player. Full server-authoritative multiplayer commands remain on the roadmap.

## Install for development

1. Install Terraria and tModLoader through Steam.
2. Copy or clone this folder into `Documents/My Games/Terraria/tModLoader/ModSources/Soulmates`.
3. Open tModLoader and choose **Workshop > Develop Mods > Build + Reload**.

The project targets the current tModLoader 1.4.4 stable release. In the normal ModSources folder the project automatically uses `tModLoader.targets`. For development elsewhere, set `TML_PATH` to a tModLoader installation containing `tMLMod.targets`.

## Prototype recipes

- **Soulcore:** 8 Fallen Stars, 5 Amethyst, and 10 Stone Blocks at a Work Bench.
- **Blank Sigil:** 3 Fallen Stars, 1 Amethyst, and 5 Silk at a Work Bench.
- **Starfinder Bell:** 5 Fallen Stars, 3 Iron or Lead Bars, and 1 Lens at an Anvil.
- **Delver Charm:** 5 Iron or Lead Bars, 2 Amethyst, and 20 Stone Blocks at an Anvil.
- **Hearth Ribbon:** 5 Silk, 2 Daybloom, and 2 Fallen Stars at a Work Bench.

## Roadmap

- Custom pixel art and layered companion bodies.
- Expanded equipment and companion inventory.
- Advanced jobs for later ores and specialized materials.
- Bond progression, needs, memories, dialogue, and evolving traits.
- Multiplayer synchronization and server-authoritative jobs.

## License

MIT
