# Pet Access And Character Shortcut: 0.23.1

Prepared and installed locally October 9, 2026. No public upload, game launch, graphical screenshots or connected multiplayer session in this pass.

**Later player countercheck:** V did not work in the real 0.23.1 client; an owned pet was not visible. The assertions below establish registered-trigger routing, not physical key mapping or rendered acceptance. The earlier unconditional V claim was incorrect. 0.23.2 corrects current instructions and pet controls; this report preserves the earlier test scope.

## Repairs

- Handle the registered character trigger before Soulwheel mouse capture. Injected-trigger tests open/close the panel; a real V press was subsequently reported not to work. Text input, native NPC dialogue, cursor items, creator, mailbox and initiative prompts retain priority. Customized keybinds are not overwritten.
- The wheel's direct Carrot/Pets entry and dedicated character tab open the same manager. Equipment's shortcut and Critters' owned-pet submenu remain.
- Show unfavorited reusable pet items from ordinary player inventory, six per page. Clicking gives the real item without changing the hotbar. Owned slots reuse existing storage: right-click summons, left-click withdraws; the star dismisses.
- Validate source data and active identity, respect full storage/pending transfers and reuse server inventory receipts/rollback. No new profile field or automatic permission.

## Verification

tModLoader 2026.8.3.0 / Terraria 1.4.4.9, .NET SDK 8.0.425. Isolated probes exit before world selection and do not modify normal saves.

| Check | Result |
| --- | --- |
| Production MSBuild/native compiler | 0 errors, 0 warnings |
| Native regression | 93,772 assertions, 0 failures |
| Independent native audit | 182 expectations, 0 failures |
| Static/localization | 54,688 assertions; 928 keys in nine languages; 489 references |
| Candidate and installed package | 80 source hashes match; 108 entries; valid native payload; no test/private content |
| Workshop drafts, offline only | EN 7,683 / DE 7,838 UTF-8 bytes, below 8,000 |

Native UI events cover both routes, four character views at three viewports/three scales in nine languages, source paging, real non-hotbar giving, favorites, replaced items, stale companion binding, summon/switch/dismiss and existing ore shortcuts. Key tests drive the registered trigger and production input hook, not operating-system keyboard injection.

Server fixtures reject wrong IDs, stale/truncated tokens, invalid slots, full storage, changed mod-item data and replayed requests. Rejected gift receipts restore cargo while preserving a newer player item; accepted gifts preserve custom data. Existing lifecycle, save/wire, player-pet coexistence and critter checks remain included.

The test-only regression probe retains 33 known nullable/Hjson warnings. Deliberate malformed-packet, blocked-write and registry-association diagnostics are not gameplay failures. Production and independent audit compile without warnings.

## Installation

Installed at **10:35:44 CEST** into `C:\Users\AETHER LUX\Documents\My Games\Terraria\tModLoader\Mods\Soulmates.tmod` after the user confirmed the game was closed.

- Version **0.23.1**, **1,596,298 bytes**, SHA256 `4160403D99D72FFB11661562D9D1937CDD13ADBF1ABA9E40CA60956B9AC1E9EC`.
- Backup: `work/client-backups/before-0.23.1-20261009-103542-253`.
- All 180 backup files verified, totaling 178,138,664 bytes. All 179 non-package originals remained unchanged. Activation still enables Soulmates; no probe or staging file remains in normal Mods.
- Actual replaced 0.23.0 file: 1,579,338 bytes, SHA256 `8741B592824118B6CD981A9E6A1735CE3C58420102552D76575A455DCC183283`. Retain the actual found file instead of assuming it equals the earlier candidate build.

The first atomic replacement was refused by a file lock despite an empty process query. It left the package unchanged; its verified staging file was removed. After the user's explicit close confirmation, replacement and full verification succeeded. The first backup remains under `before-0.23.1-20261009-103445-280`. Process queries alone are not proof that the shared desktop game is closed.

Raw package, reports and installation receipts remain under `outputs/pet-inventory-0.23.1`.

## Player Checklist

1. Start normally, confirm **0.23.1** under Mods, and summon with the Sigil in inventory.
2. Open her wheel and choose Details. The V route failed the later player countercheck; inspect the configured shortcut under Controls instead of assuming it is assigned.
3. Her wheel's Carrot/Pets icon and the character window's Carrot tab should open the same manager.
4. Put an unfavorited reusable pet item anywhere in ordinary inventory. Click its upper icon: it should move once into her lower slots.
5. Right-click her owned slot to summon; left-click to retrieve. Dismiss/recall must not consume the item or affect your pet.
6. Check full-inventory returns, favorites, save/reload and Work's direct ore commands. Report solo/MP, action and actual result.

Pet visuals still use harmless custom floating following, not original pet AI or special utilities. Rendered framing, long-session/progression and connected multiplayer acceptance remain open. All peers should run 0.23.1.
