# Pet Inventory And Ore Shortcuts: 0.23.0

Prepared locally October 9, 2026. This is not a client-installation or publication receipt. At preparation time the normal client and public release remained the preceding 0.22.2 baseline; the preparation pass did not control the shared desktop or change player/world saves. Subsequent [normal-client installation](CLIENT-INSTALL-0.23.0-2026-10-09.md) was verified the same day after the user's request; it does not change the testing scope below.

## Requested Repairs

- Ore selection was still implemented but buried under Work > Config. Work now exposes Find nearby ore and Mine area beside Config/Last, without replacing existing settings, consent or progression checks.
- Companion pets previously offered only Zephyr Fish and Nectar. Twelve separate Pets slots now hold real reusable summon items, discovered through loaded pet-buff/projectile metadata instead of an item whitelist.
- Hotbar storage, withdrawal and multiplayer acknowledgement reuse the existing cargo transaction. The wheel lists owned items only, four per page, with inventory access and dismissal. Right-clicking a Pets slot selects it; left-click returns the item.
- Legacy cargo routes recognized pet items into Pets without deletion. ItemIO preserves mod-item identity and custom data; the active saved selection resolves its item identity instead of trusting an old runtime ID. Profile transport includes Pets; all connected peers must use 0.23.0.

## Verification

Installed libraries: tModLoader 2026.8.3.0 / Terraria 1.4.4.9, .NET SDK 8.0.425. Probes use separate workspace save directories and exit before selecting a world. Server-mode fixtures use simulated clients; they are not a connected multiplayer session.

| Check | Result | Scope |
| --- | --- | --- |
| Production MSBuild and native compiler | 0 errors, 0 warnings | Compilation and package |
| Native regression probe | 92,296 assertions, 0 failures | Engine, UI events, normal NPC/projectile updates |
| Independent native audit | 178 expectations, 0 failures | Ownership, stale requests, custom item identity, inventory conflicts |
| Static/localization checks | 54,461 assertions; 924 keys in nine languages; 482 source references | Complete key coverage, layout/initialization/controller checks |
| Package integrity | 79 production source hashes match; 107 entries; valid native payload hash | No test/private payload; normal visual resources included |
| Workshop drafts | EN 7,664 / DE 7,765 UTF-8 bytes | Below 8,000; offline only |

Focused native cases cover:

- Ore-button hit testing in nine cultures at three requested scales; selection of a real ore family starts mining, disappeared ore refreshes safely, and manual pointing remains available.
- All owned pet entries and wraparound paging through production wheel input; Equipment inventory access and native UI-element right-click dispatch select the real pet slot.
- A full equipment pack does not consume pet capacity. Twelve distinct pet items fit; a thirteenth remains in the source inventory.
- Real deposit/withdraw transactions in single-player and isolated server mode. Full player inventories retain both the item and familiar; successful final withdrawal removes the familiar immediately.
- Clone, save and wire round-trips for all 78 discovered native items. A separately registered mod-item fixture works without production whitelist changes and retains custom data through save/transport. Tampering with its legacy raw ID does not override the ItemIO selection.
- Five pet families (Zephyr Fish, Baby Hornet, Bunny, Wisp and Companion Cube) run 600 normal projectile updates each in single-player and server fixtures. Checks include movement, lifetime, frame bounds, item ownership and recall cleanup.
- Repeated selection cannot duplicate followers or consume the summon item. Wrong identity, stale slot tokens, unsupported types, truncated packets, delayed profiles, owner death and coexistence with the player's native pet remain covered.

The broad test-only probe retains 33 known nullable/Hjson compatibility warnings; production and independent audit compile cleanly. Malformed-packet and blocked-write fixtures deliberately emit diagnostic errors. Denied `.tmod` registry association during headless startup is not a failed gameplay assertion.

A repeated initial run had two failing bird-company expectations. The trace showed a fleeing bird leaving the owner's existing 320-pixel leash before invitation; the success fixture inherited randomized companion position/momentum from its preceding species. The fixture now resets each species' starting motion, controls/restores its random source, and separately checks that an escaped target is not recruited at range. Production range, movement and animal AI were not relaxed. The failed report/trace are retained beside the final reports instead of being overwritten as an all-green history.

Final package: 1,589,998 bytes, SHA256 `72D61E7180F5C01333CF694BB6CFC73C3F76393238F34DD3D8747DD9B756250D`. Raw reports, native item catalog and package verification are retained locally under `outputs/pet-inventory-0.23.0`.

## Deliberate Limits

The follower is a harmless Soulmates projectile using the item's native projectile sprite frames and custom floating movement, not a second player or original pet AI. Ground pets float; complex multipart art, custom draw hooks, dyes, transformations and special utilities such as Chester storage are not reproduced. Light-pet projectiles provide basic illumination. Metadata discovery is not universal mod-pet compatibility. The player's own pet slot and buffs are untouched.

No rendered playtest, new screenshot, real progression/endurance run, connected multiplayer session or public upload is claimed. Installation is verified separately. Native UI hit tests do not prove final texture framing or visual polish.

## Player Checklist

1. Right-click her > Work > Find nearby ore (Spelunker Potion icon). Near an ore vein, choose its icon and watch a real mining visit. Test Mine area and ensure Config/Last still work.
2. Select a real reusable pet item in your hotbar. Details/V > Equipment > Store selected item: one real item should move into Pets, not vanish or occupy an equipment slot.
3. Open Critters > Companion pet. Only her owned pet items should appear. Select one; confirm one follower appears and your own native pet remains unchanged.
4. In the Pets inventory, right-click to switch and left-click to take back. Taking the final supporting item must immediately remove its follower. A full player inventory must leave both intact.
5. Dismiss, recall, summon again, switch Sigils, then save/reload. Cargo and selection must stay with the correct Sigil, without duplicate followers.
6. Check at least one flying, ground and light pet. Ground pets currently float; report missing/clipped art rather than expecting original walking or utility behavior. Try more than four owned items to check wheel paging.
7. In connected MP, use 0.23.0 on every peer and repeat transfer/switch/full-inventory cases. This acceptance gate remains open.
