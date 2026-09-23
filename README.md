# Soulmates

Soulmates is a tModLoader mod about creating companions that feel personal. The reusable **Soulcore** opens a small companion creator. Each finished companion is stored in its own **Soulbound Sigil** with a persistent name, appearance, personality, talent, bond, mood, and energy.

## Prototype features

- Craft a reusable Soulcore and Blank Sigils.
- Create multiple unique companions from English-language presets.
- Choose a name, essence color, personality, and starting talent.
- Summon one active companion at a time.
- Right-click its Sigil to switch between Follow and Stay.
- Hold Up and right-click the Sigil to recall it.
- Companion identity and progression data are saved on the Sigil.

This first playable slice is intended for single-player testing. Full server-authoritative multiplayer commands are on the roadmap.

## Install for development

1. Install Terraria and tModLoader through Steam.
2. Copy or clone this folder into `Documents/My Games/Terraria/tModLoader/ModSources/Soulmates`.
3. Open tModLoader and choose **Workshop > Develop Mods > Build + Reload**.

The project targets the current tModLoader 1.4.4 stable release. In the normal ModSources folder the project automatically uses `tModLoader.targets`. For development elsewhere, set `TML_PATH` to a tModLoader installation containing `tMLMod.targets`.

## Prototype recipes

- **Soulcore:** 8 Fallen Stars, 5 Amethyst, and 10 Stone Blocks at a Work Bench.
- **Blank Sigil:** 3 Fallen Stars, 1 Amethyst, and 5 Silk at a Work Bench.

## Roadmap

- Custom pixel art and layered companion bodies.
- Equipment slots and companion inventory.
- Treasure sensing, gathering, and controlled mining jobs.
- Bond progression, needs, memories, dialogue, and evolving traits.
- Multiplayer synchronization and server-authoritative jobs.

## License

MIT
