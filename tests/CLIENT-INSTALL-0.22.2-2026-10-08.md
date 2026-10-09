# Normal-Client Installation: Soulmates 0.22.2

Verified October 8, 2026, at 22:05:16 CEST, after the user requested installation. No game/dotnet process was running at the installation checks. File installation only: no game launch, rendered playtest, Git push or Workshop upload.

## Installed Package

- Destination: `C:\Users\AETHER LUX\Documents\My Games\Terraria\tModLoader\Mods\Soulmates.tmod`
- Source: `outputs/critter-pet-repairs-0.22.2/Soulmates.tmod`
- Internal name/version: `Soulmates`, `0.22.2`
- Package loader: `2026.8.3.0`
- Size: **1,586,271 bytes**
- SHA256: `7A9312816042843D160E220C0CB3DAD12AF3D5FA534D5FFF5D3752E9A9520BB2`

Both source and installed package passed native payload integrity, file-table and all **79 production-source hash checks**, with **107 entries** and no private/test content. The installed file matches the [Living Critters repair evidence](LIVING-CRITTERS-0.22.2-2026-10-08.md). The Mods folder contains only `enabled.json` and `Soulmates.tmod` as top-level files; its existing ModPacks directory was not changed. No temporary package or test probe remains there.

## Backup And Conservation

Backup: `work/client-backups/before-0.22.2-20261008-220514-450`.

All **180 recorded files**, totaling **178,073,389 bytes**, were copied and hash-verified: the previous package, activation file, local Players files recursively, current top-level Worlds files and save-root JSON files. All **179 non-package originals** were checked unchanged after installation. Historical world archive subdirectories and Steam cloud files were not modified.

The actual previous package had header version **0.22.1**, size **1,576,010 bytes**, SHA256 `52AB7F80D84C75D18154E02084FC399AB49589A4DF1AA51BCC1F792176127BC1`. Its verified backup is retained, along with an additional hash-verified `atomic-previous.tmod` from the successful atomic replacement. Activation remains unchanged: `enabled.json` enables Soulmates and retains SHA256 `BCE083542F7274DEEE54547E76666CE3889050729A311C5B3654C57EBE9D6B40`.

The first atomic replacement attempt rejected an empty backup-path argument before changing the installed package. The successful retry used an explicit workspace backup path. The failed attempt's verified backup was retained and its exact temporary file was hash-checked and removed. This was an installation-tool error, not a game crash. Only the authorized mod package was replaced; Workshop-managed files and normal ModSources were not changed.

Private machine-readable installation and installed-package receipts are in the ignored `outputs/critter-pet-repairs-0.22.2` directory.

## Start And Acceptance

Start tModLoader normally through Steam and confirm **Soulmates 0.22.2** under Mods. The installed file is verified; the version actually loaded after startup still needs that client check.

Prioritize aged critter recognition, Company approach/follow, Pet Collect, witnessed critter loss, right-click context through grass, and the two supported owned pets. See the seven focused checks and remaining graphical/multiplayer limitations in the [repair report](LIVING-CRITTERS-0.22.2-2026-10-08.md). The shared desktop was not taken over during installation. This installation is not public release evidence.
