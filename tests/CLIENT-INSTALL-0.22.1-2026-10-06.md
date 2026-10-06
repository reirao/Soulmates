# Normal-Client Installation: Soulmates 0.22.1

Verified October 6, 2026, at 09:28:42 CEST, after the user requested the new version and confirmed tModLoader was closed. File installation only: no game launch, rendered playtest, Git push or Workshop upload.

## Installed Package

- Destination: `C:\Users\AETHER LUX\Documents\My Games\Terraria\tModLoader\Mods\Soulmates.tmod`
- Source: `outputs/showcase-repairs-0.22.1/Soulmates.tmod`
- Internal name/version: `Soulmates`, `0.22.1`
- Package loader: `2026.8.3.0`
- Size: **1,586,580 bytes**
- SHA256: `F904525719535DBFCA7530CF2A684E455A971858AAAFD5B1E2B3272C871B3290`

The installed header/hash match the exact package from the [repair counterchecks](LIVE-SHOWCASE-REPAIRS-0.22.1-2026-10-05.md). Before installation, package integrity and all 79 included production-source hashes were rechecked. No Terraria, tModLoader or dotnet process was running at the installation checks. The Mods folder contains only `enabled.json` and `Soulmates.tmod`; no test probe or second mod package was installed. The temporary `.tmp` file no longer exists.

## Backup And Conservation

Backup: `work/client-backups/before-0.22.1-20261006-092708`.

All **167 files**, totaling **168,674,090 bytes**, were copied and hash-verified: previous mod, activation file, local Players files recursively, current top-level Worlds files and save-root JSON files. Historical world archive subdirectories and Steam cloud saves were not copied or changed. The private backup manifest stays in the ignored workspace backup directory.

The actual old package was 0.22.0, 1,624,854 bytes, SHA256 `1CC6C12688FCF1F945A1543A7532EFFBA2A27A3F2D8BDA5FCCC3CC1080DD899E`. Its backup remains intact. All **166 non-package originals** were rechecked unchanged afterward. `enabled.json` still enables Soulmates and retains SHA256 `BCE083542F7274DEEE54547E76666CE3889050729A311C5B3654C57EBE9D6B40`.

Windows initially required parent-directory permission to create the staging file. A PowerShell null-string conversion then prevented the first atomic-replace call. Both failed before changing the original mod; the old hash was checked again before the successful replacement using an actual null string. These are installation-tool setup issues, not game crashes. Only the mod package was replaced. Workshop-managed files and saves were not modified.

## Start And Acceptance

Start tModLoader normally through Steam. Confirm **Soulmates 0.22.1** under Mods, then open your character/world. The file's installed version is verified; the version actually loaded after startup still needs that client check.

Prioritize single/full cargo withdrawal, partial/full player inventory, the Soulcore icon and questions that stay readable while awaiting an answer. The [focused playtest](PLAYTEST-CRITTER-CHARACTER-0.22.0-DE.md) still applies; live critter Company/capture/loss, owned pets, long sessions and connected multiplayer remain open. The shared mouse/keyboard was not taken over during installation.
