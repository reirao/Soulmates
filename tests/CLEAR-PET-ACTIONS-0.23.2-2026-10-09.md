# Clear Pet Actions: 0.23.2

Prepared and installed locally October 9, 2026. No game launch, graphical screenshots, public upload or connected multiplayer session in this pass.

## Actual Client Evidence

- The normal client log confirms Soulmates 0.23.1 in the player's single-player session, followed by normal save/exit and shutdown. This was not a stale package: all 80 packaged production sources matched the repository before repairs.
- Read-only inspection with native TagIO/ItemIO found a stored pet item 4366 (Eucalyptus Sap), stack 1, and `petItemType = 0`. The item existed but no familiar was selected. Neither the save nor the selection was edited.
- The player reports V does not work. Previous tests injected the registered trigger and did not prove physical keyboard operation. Current documentation withdraws the unconditional V claim; it does not describe this patch as a proven physical-key fix.

## Repairs And Checks

- Owned pet slots now summon on left-click and return the real item on right-click, consistently in both cargo and pet management. The first gift to empty pet storage selects its familiar; subsequent gifts preserve an active selection or explicit dismissal.
- The manager distinguishes no summon, waiting and an active matching familiar. A selected item without a projectile does not display the active state. Status describes runtime entity state, not pixel verification.
- The Sigil displays the actual keyboard assignment or an unassigned hint. Diagnosis adds assignment and input gates. No raw V fallback, binding reset or profile write was introduced.
- Keyboard fixtures use native `KeyConfiguration.Processkey`, covering unbound V, customized B, menu routing and input guards. These remain mapping tests, not operating-system input.
- Normal NPC/projectile updates now cover Eucalyptus Sap alongside five existing families in single-player and server-authority fixtures. Source data, favorites, wrong identities, replay, full storage, receipts, rollback, dismissal, withdrawal and player-pet coexistence remain covered.
- The new UI return test initially encountered unfinished native recipe lookup tables at PostAddRecipes. The fixture now initializes the native lookup, as the existing cargo fixture already did; production code does not suppress the failure or bypass transfers.

| Check | Result |
| --- | --- |
| Native production compiler and MSBuild | 0 errors, 0 warnings |
| Native regression | 93,883 assertions, 0 failures |
| Independent native audit | 182 expectations, 0 failures |
| Static/localization | 54,971 assertions; 933 keys in nine languages; 497 references |
| Candidate and installed package | 81 source hashes match; 109 entries; native payload valid; no test/private content |
| Offline Workshop descriptions | EN 7,787 / DE 7,962 UTF-8 bytes, below 8,000 |

The general test-only probe has 33 existing nullable/Hjson compatibility warnings; production and the independent audit have none. Deliberate malformed-packet and blocked-write diagnostics are negative test cases.

## Installation

Installed at **10:52:49 CEST** in the normal client's Mods directory. Version **0.23.2**, **1,599,265 bytes**, SHA256 `117E204DDA0EBCB131D5D199E7C4D3E1194633886D725C6D951E1915EEF88769`.

The backup at `work/client-backups/before-0.23.2-20261009-105246-510` contains 180 verified files, totaling 178,143,432 bytes. All 179 non-package originals, including player/world saves, activation and input profiles, remained unchanged. Only `Mods/Soulmates.tmod` was replaced atomically; staging was removed. Workshop files were not changed. The actual replaced 0.23.1 package was retained, SHA256 `EB1D1902979AE25D47B8BF7B18B1F144F7E92BF4ECBAF6ABA0288C21548CB24A`.

Raw package, check results, redacted saved-pet inspection and installation receipts are in `outputs/pet-inventory-0.23.2`. Current Mod/project text and offline Steam drafts are corrected; no online description save is claimed.

## Player Countercheck

Subsequent evidence, separate from the preparation pass above: the creator reports a positive playtest and publishes 0.23.2 on October 9 at 11:15 CEST. Steam shows the matching update entry with moderation pending. The physical-V failure remains a documented limitation; this general playtest does not override that report.

1. Start normally and confirm 0.23.2 under Mods.
2. Right-click her > Pets. Left-click the existing Eucalyptus Sap in her owned slots. Check the status and whether the sugar glider appears near her.
3. Dismiss, select it again, switch pets and right-click to retrieve the item. Your own pet/buffs must remain unchanged.
4. Use her wheel > Details. Inspect the Sigil's actual shortcut hint; customize Talk to Companion under Controls rather than assuming V is assigned.
5. If status says Summoned but nothing renders, export Diagnostics and report that distinction. Physical-key, rendered visibility and connected multiplayer acceptance remain open.
