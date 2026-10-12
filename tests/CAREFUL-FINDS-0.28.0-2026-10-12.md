# Careful Finds: 0.28.0 Verification

Date: 2026-10-12. Local beta candidate. The build/check session did not install it; the creator subsequently requested installation. At 13:53 Europe/Berlin the exact checked package replaced normal-client 0.27.0. No commit, push or public update was performed.

## Subsequent Installation

Only `Documents/My Games/Terraria/tModLoader/Mods/Soulmates.tmod` was replaced. The verified backup contains 161 files; all 160 non-package originals, including activation and backed-up saves/configuration, were verified unchanged. Staging was removed. The game was not launched. The package description's pre-install candidate status is a build-time statement, not a different installed version.

Installation receipt: `outputs/careful-finds-0.28.0/client-installation.json`. Backup: `work/client-backups/before-0.28.0-20261012-135351-244`. Historical world subdirectories and Steam cloud were not modified.

## Exact Artifact

- Package: `work/pet-inventory-0.28.0-build/Mods/Soulmates.tmod`.
- Native loader identifies Soulmates version 0.28.0.
- Size: 1,725,370 bytes.
- SHA-256: `BB63B2F460A88DD4B56625DE04D088375609C5D85D681C112AE33144DDF56D33`.
- Regression and audit package copies have this same hash.
- Production compilation: zero errors, zero warnings. Regression harness retains 33 existing nullable/Hjson compatibility warnings; audit compilation has zero warnings.
- Pack description: 7,687 UTF-8 bytes, below the 8,000-byte limit. This entry's changelog contains only 0.28.0 changes. Public descriptions remain at their preceding published state.

## Results

| Suite | Result |
| --- | --- |
| Static/catalog checks | 62,632 assertions passed; 1,070 keys in nine languages; 618 source references |
| Native engine regression | 117,074 assertions; zero failures |
| Separate adversarial audit | 516 passing expectations; zero defects |

Commands, from the repository root:

```powershell
./work/Build-PetInventory.ps1 -Mode Build -Version 0.28.0
./work/Build-PetInventory.ps1 -Mode Regression -Version 0.28.0
./work/Build-PetInventory.ps1 -Mode Audit -Version 0.28.0
./work/dotnet-sdk-8.0.425/dotnet.exe run --project tests/Soulmates.Regression.csproj -- .
```

The private workspace harness uses its isolated tModLoader runtime/save directories and does not operate on the player's normal saves. The native regression deliberately tests malformed data; expected rejected-packet exceptions in its log are not failing assertions. The audit deliberately exercises denied file writes. A denied registry association attempt from test packaging is expected in the restricted environment.

## New Coverage

- Actual context-wheel handlers retain the clicked salvage footprint when the mouse moves to an icon. Native chest/torch interactions retain priority. Meet reaches a specific native critter's question.
- The general search mode is labelled Critter Encounters; the direct capture command explicitly says Catch only this one. Catalogs explain the individual capture question rather than promising silent automatic collection.
- Salvage cannot start without one-object consent or the second bounded-area confirmation. Wrong world, changed frames, changed protection, old question tokens, replay, missing Sigil and client-side mutation are rejected.
- A single permit leaves neighbors alone; a captured area does not expand to later finds. Native cobweb drops are conserved and reach common cargo. Pause/Resume retains the approved work; Abort discards it.
- Native pots, coral, books, bottles, water candles, statues, a banner with wrapped style 100, and all five painting sizes can be recovered without fragments. Legacy bottom-right pot capture covers the full footprint. Native destruction is invoked once per object, not once per cell.
- Wires, actuators, paint/coatings, house walls, home records, bed spawns, lava and gem-supported falling terrain block removal. Ordinary water alone does not. Native runtime placement records conservative construction evidence; world generation does not. World save/load retains protection.
- Native NPC updates exercise temporary company and catching for both scorpion species in single-player and server-authoritative fixtures. Each native catch inserts exactly one actual item into ordinary Pack and its bound Sigil.
- Automatic Collect Always keeps the live animal until its individual answer. Four encounter choices, fresh target identity, permission changes and reused same-species NPC slots are checked. No broad future-catching consent is inferred.
- Two successive native scorpion catches during speech retain both receipts and their names in order. The client also queues an arriving receipt when its local speech clock is busy.
- Real packet handlers reject foreign/stale/truncated salvage requests and answers. Client-spoofed server speech is rejected; two-argument salvage localization and original single-argument receipt localization reach the correct companion.

## What These Checks Do Not Prove

The native engine fixtures run without a rendered client. Salvage job fixtures position the companion at each captured target to test authorization and mutation, not full navigation around a real cave. Critter movement fixtures do execute native NPC updates, but still are not screenshots or a physical player session. Server-mode packet tests are not two connected clients.

Old worlds have no trustworthy universal ownership history. Unrecorded objects stay unknown and require explicit consent; observed construction and recognized homes are conservative evidence, not a proof that every old player decoration is identified. Direct tile-writing mods can bypass placement hooks. Only the documented whitelist is eligible.

Meeting reuses native temporary Company, brief greeting behavior and existing species memory. It does not add persistent named-critter relationships or tame every vanilla/modded species. Caught critters are real items, not following pets. No new individual caught-item-loss report has been reproduced in rendered gameplay; the confirmed missing-message path is repaired and native scorpion conservation is covered.

## Next In-Game Acceptance

1. With the candidate loaded, use Watch/Meet on a natural scorpion. Leave it alone, then meet a different animal; verify the live question refers to the selected animal and remains answerable.
2. Select catch this one. Open ordinary Pack and confirm one actual critter item and its receipt. Try making friends with another supported animal; check that native temporary Company, not capture, occurs.
3. Right-click a natural pot/web/painting away from housing. Approve only this one, then separately test nearby plus its second confirmation. Confirm newer objects are not included.
4. Place a decoration or wire near a find. Confirm salvage refuses; repeat after a save/reload. Check an existing old build conservatively and decline unknown objects you want to retain.
5. Test Pause/Resume/Abort and repeat on a real connected server with all peers on 0.28.0. Report version, chosen action, expected outcome and observed outcome.
