# Soulmates 0.20.1: semantic emote categories

October 5, 2026. Local beta usability candidate, **not installed or published**. Public/normal-client 0.20.0 remains separate. No player/world files, enabled-mod list or Workshop-managed package were changed.

## Implemented scope

All 151 vanilla player symbols are partitioned exactly once into seven semantic root categories. Large groups have small folders; item subcategories remain and Lucy is classified as a weapon. Hunger, signals, ailments and bosses move out of the old mixed feelings/dangers lists. Companion Bond has Feelings, Gestures and Care, with all six original actions. Its Games branch remains separate.

A common folder stack replaces the Items-only flag. Each Back removes one level; leaf page arrows cannot change folders. Root toggles, Point and reopening clear the old category/page state. The breadcrumb uses the same translated category labels. Category data is in the Emotes partial; native player symbols and companion gestures keep separate dispatch, icons and labels.

No initiative, cargo, combat, NPC interaction, persistent field, serialized enum or packet format was changed. Existing native-first input and captured world contexts remain covered by the broad probes.

## Exact package

- tModLoader: 2026.8.3.0, .NET 8.
- Internal name/version: Soulmates / 0.20.1, read from the TMOD header.
- Bytes: 1,567,497.
- SHA256: `1B6A976602207366C5D7F25E8CF9C721F6A7079428C0C696A0E1B7043507AA33`.
- Artifact: `outputs/emote-categories-0.20.1/Soulmates.tmod`, with raw probe reports and selected-source hash manifest.
- Canonical staging: `work/emote-categories-source/Soulmates`. All 101 selected production code/assets/root inputs match the checkout. Test probes are excluded from production.

## Verification

| Check | Result |
| --- | --- |
| Production MSBuild | Exit 0; 0 warnings/errors |
| Native production compiler/package | Exit 0; 0 warnings/errors |
| Native general engine probe | 85,796 assertions; 0 failures; exit 0 |
| Separate native safety probe | 156 passing expectations; 0 defects/harness errors; exit 0 |
| Static/controller/localization | 51,830 assertions; 877 keys in each of nine catalogs; 438 source references; exit 0 |
| Description bytes, embedded / Workshop EN / Workshop DE | 6,433 / 7,295 / 7,676 UTF-8 bytes, below 8,000 before any external uploader wrapper |
| Whitespace | `git diff --check` passes |

The wheel fixture drives production UpdateUI with mouse press/release edges, a measurement-only font and native UI transforms. It selects every native emote in all nine cultures at requested UI scales 1, 1.5 and 2, verifies the selected ID and native tooltip, and checks the actual emitted bubble is anchored to the player. It exercises folder/leaf input, next/previous pages, one-level Back, translated breadcrumbs, root toggling, switching to Point, independent player/companion navigation and all six actual companion gesture dispatches. Catalog checks reject omitted/duplicated symbols, empty or overcrowded folders and mixed folder/entry nodes.

The expanded broad fixtures continue to cover single-player/server authority, cargo conservation, consent, legacy serialization, task dispatch, native catches and context input. They are bounded engine tests, not connected multiplayer.

## Critical recheck and limits

The first run exposed an invalid dictionary-enumerator cast in the new test's bubble snapshot. That harness defect was corrected. The next complete run passed new symbol checks but retained one obsolete assertion expecting six flat Bond buttons; it now checks the real three-category Bond state, while separate controls still verify all six gestures. The expanded final suite passed, then ran again against the final re-packaged artifact after documentation edits. No production behavior was disabled to pass tests.

The native general test-only probe has 33 existing nullable/Hjson compatibility warnings; the production mod and separate safety probe have zero warnings/errors. Expected malformed-packet and blocked local-IO diagnostics come from negative controls. The sandbox denies the optional Windows .tmod association at native startup; the isolated runs nevertheless complete with exit 0.

No rendered tModLoader client, screenshots, prolonged gameplay, full progression or connected multiplayer session was performed. Fonts were measured, not rendered. Visual category transitions, icon legibility and language typography still require the [focused playtest](PLAYTEST-EMOTE-CATEGORIES-0.20.1-DE.md). The wider [beta gates](../BETA.md) remain open. Translation completeness is not native-speaker certification.
