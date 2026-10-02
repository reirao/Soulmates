# Deliberate Mouse Modes Revision

Soulmates 0.17.0, local candidate for tModLoader 2026.8.3.0. Not installed, pushed to GitHub or published to Steam by this revision. The installed/uploaded 0.16.1 and its public Workshop descriptions remain unchanged.

## Implemented Behavior

- Terraria is the default mouse mode. Empty-air and ordinary furniture/world right-clicks do not automatically open a Soulmates menu.
- Right-click the character or companion for their respective wheel. Native-symbol selectors choose Terraria, Me or Soulmate. Me points out a world target or opens native player emotes; Soulmate offers applicable inspection, gathering, mining and forestry tasks.
- World menus retain the target from the original click. Moving onto a button does not change it. Changed tiles, missing objects and reused item/NPC references are rejected; existing range, cargo and terrain-protection checks still apply.
- Right-click inside an open wheel cycles Me, Soulmate and back to Terraria. Opening/holding the mouse does not repeatedly switch. X or Escape restores normal use. Successful orders close their menu without forgetting a deliberately selected mode.
- A native cursor symbol indicates a non-native mode. The three selector hit targets sit outside the emote fan and above hover/status text. Player Emotes, full companion tools, details and pack access remain available.
- Inventory, text entry, ongoing native NPC dialogue and cursor-held items retain priority. Native alternate item use is allowed to take precedence over a deferred character-wheel opening in Terraria mode. Closing clicks remain captured until release.
- One player input router replaces separate player/companion opening paths. It uses raw input transformed through Terraria's view matrix, independent of UI-hook changes to Main.MouseWorld. Mouse mode is client-session state, not a saved autonomous-work permission; existing Sigils need no migration.

## Verification

- Native compilation: zero errors and warnings for the mod and its isolated probe.
- Native packaging and final engine run: **10,686 assertions, zero failures**. The final package loaded as Soulmates 0.17.0.
- Static suite: **692 bilingual keys, 354 source references and 3,294 assertions**, all passed.
- Native fixtures exercise contextual action availability, original-target gathering despite cursor movement, protected furniture, changed tiles, reused same-type item/NPC slots, native player pointing reaching the companion emote observer, native/default routing, companion identity changes, deferred alternate-use/NPC priority, selector hit tests, mode selection, right-click edge/hold handling, Escape, inventory priority and control restoration.
- The fixture supplies Terraria's real SpriteViewMatrix with a viewport override and 1.4x zoom because the headless server has no graphical camera. It invokes the production input/menu methods; it does not render a game window or emulate an entire client frame.
- Shared production layout math is checked at 800x600, 1280x720, 1920x1080 and 2560x1440 with UI scales 1, 1.25, 1.5 and 2, including edge clamping, non-overlapping mode hit targets and emote/label separation.
- Earlier creation, cargo conservation, wallet, social, combat, healing, forestry, critter, lifecycle and multiplayer packet fixtures remain enabled. Malformed-packet warnings in the console are deliberate negative tests. The denied optional Windows .tmod association registration does not prevent packaging or the test run.
- `git diff --check` passed; existing line-ending conversion notices are not whitespace errors.

Evidence and package: `outputs/revision-0.17.0`. Artifact SHA-256: `2A200F0244C711A4E94576E5D7E0ED6878E57B1DE3DF6D00126A7829D22C5A42`; size 1,363,271 bytes. The pack includes the existing Workshop icon and custom Soulkin assets; the optional mini-icon remains excluded. No test probe is included in the deliverable mod.

## Remaining Playtests

Fresh graphical single-player tests of ordinary chest/door/NPC/alternate-item interactions, camera/UI scaling, mode cursor readability, real button clicks, repeated target selection and companion switching. Live Host & Play tests of targeted orders and native emotes remain necessary. Server-side validation is reused; the local snapshot check is not a new cross-network identity protocol. No new gameplay screenshots, visual certification or complete progression playtest is claimed.

No player/world saves, running game installation or opt-in feedback files were changed. Public Workshop drafts still describe the uploaded 0.16.1; the embedded candidate description and upload changelog describe only 0.17.0. Historical logs were not pasted into a new public entry.
