# Many Voices - 0.18.0 Local Candidate

## Implemented

- Complete 729-key catalogs for English, German, Italian, French, Spanish, Russian, Brazilian Portuguese, Polish and Simplified Chinese: all nine cultures supported by the installed Terraria 1.4.4 / tModLoader 2026.8.3.0 runtime.
- The seven new catalogs cover menus, items, cargo, wallet, commands, initiative rules, learning, work, combat, memories, resident relationships, critters, personality/voice responses and rock, paper, scissors. The unpublished 0.17.2 minigame remains included.
- No separate language option: text resolves through Terraria's selected culture. Proper names, player-selected names, localization keys, command syntax and numbered format arguments are preserved.
- Creator and mailbox titles/buttons fit their measured font width; preview/context text wraps within a bounded height. Mailbox input wraps Unicode text and shows the trailing visible lines rather than drawing long messages outside its field. Stored input is not shortened by the viewport.
- New catalogs are generated from a structured translation matrix with exact key and column checks. Tests and authoring data are excluded from mod packaging.
- README, local mod/Workshop descriptions and current-only update notes describe 0.18.0. No online descriptions, release tags or Workshop binaries were published during this language pass.
- No profile/save-data format change, currency/cargo policy change, autonomy change or new work permission.

## Verification

- Production mod and engine probe compile with zero warnings and errors.
- Static suite: 41,451 assertions, 366 source references, 729 keys per culture, zero failures. Checks include catalog parity, nonempty translations, format placeholders, generated-file consistency, Unicode wrapping, numerical text fitting, UI transforms and initialization, and the 8,000-byte Workshop description limit.
- Native tModLoader engine probe: 34,200 assertions, zero failures with Soulmates 0.18.0. It switches the actual engine through all nine cultures and compares every registered string with its packaged catalog, including personality/emote responses, saved-key fallback behavior and minigame reply lengths. Existing cargo, combat, authority and input fixtures also run.
- The headless combat fixture initially crashed in native projectile collision before the language checks: recipes initialize before a world exists. The probe now allocates a bounded tile map before those fixtures and positions its duplicate companion away from the map edge. This is a test setup repair, not a production combat change. The repaired run completes.
- The probe exits before loading a player/world. Server fixtures use disconnected sockets, not a live mixed-client multiplayer session.
- Packaging uses a clean staging directory because the canonical checkout contains an inaccessible ignored temporary directory. No protected temporary files or filesystem ACLs were changed.
- The final local install has a backup and checksum receipt under `outputs/languages-0.18.0`. Player and world files are not modified.

## Remaining

- The seven new translations are AI-authored. Native-speaker review is still needed for grammar, Terraria terminology and character voice.
- Font-fit and wrapping tests do not establish that every menu is visually readable at every resolution. No new graphical screenshots or nine-language real-client playtest are claimed. Check the creator, Talk Mode, mailbox/IME, radial tooltips, speech and resource pages in the actual client.
- Some multiplayer packets (`TalkResponse`, `QuickActionResponse`, `ProfileUpdate`) still contain server-formatted text, so those replies can retain the server's language. Menus, local replies, key-based `CompanionSpeech` and minigame results resolve in the client's culture. Complete mixed-language multiplayer needs structured key-and-argument transport. Saved literal memories retain their original language.
- Japanese, Korean and Traditional Chinese are not supported by this installed 1.4.4 runtime and are not claimed here.
- Full progression, long sessions, graphical minigame behavior and live multiplayer remain outstanding before public release.
