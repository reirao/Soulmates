# Normal-Client Installation: Soulmates 0.24.0

Installed October 9, 2026, at 14:26:56 CEST after the user confirmed they had
exited and explicitly authorized installation. Process checks before staging,
replacement and final verification found no game/dotnet processes. This is
verified file installation, not proof of a loaded-client version or playtest.

## Installed Package

- Destination: `C:\Users\AETHER LUX\Documents\My Games\Terraria\tModLoader\Mods\Soulmates.tmod`
- Source: `outputs/intent-learning-0.24.0/Soulmates.tmod`
- Internal name/version: `Soulmates`, `0.24.0`
- Loader: `2026.8.3.0`; size: 1,613,027 bytes
- SHA256: `43F5C21289E0008D5A672BFCBA127407D128D93BF4927BE675FA90DE8A639E58`

Installed package is byte-identical to the tested candidate. Native payload
integrity and all 83 production-source hashes pass, with 111 entries and no
test/private content. Only `Soulmates.tmod` and the unchanged `enabled.json`
remain as top-level Mods files. No extra package or staging file remains.

## Backup And Conservation

Backup: `work/client-backups/before-0.24.0-20261009-142652-377`.

All 180 recorded files, totaling 178,242,241 bytes, were copied and hash-verified:
the previous package, activation, local Players recursively, top-level Worlds
and save-root JSON files. All 179 non-package originals remained unchanged.
Historical World subdirectories, normal ModSources, Workshop-managed files and
Steam cloud were not modified. Replacement was staged, verified and atomic.

The actual replaced package was 0.23.2, 1,588,550 bytes, SHA256
`09E08961B082E1E03E3A1937D56D76EC0F077EE0E19046B2B8F746299FBA8B93`.
It is preserved both in the backup and as `atomic-previous.tmod`.
The prior file is not assumed identical to an earlier development build.

Activation still enables Soulmates. Its SHA256 is
`BCE083542F7274DEEE54547E76666CE3889050729A311C5B3654C57EBE9D6B40`.
Machine-readable receipts: `outputs/intent-learning-0.24.0/client-installation.json`
and `outputs/intent-learning-0.24.0/installed-package-verification.json`.

## Start And Acceptance

Start tModLoader normally through Steam and confirm 0.24.0 under Mods. Use the
existing companion wheel's Details/Diagnostics route; V remains unconfirmed.
The [learning checklist](INTENT-LEARNING-0.24.0-2026-10-09.md#kurzer-spieltest)
covers observation, completed help, the four feedback answers, permission gates,
Pause/Abort and separate persistent preferences per Sigil. Use 0.24.0 on all
multiplayer peers. Graphical, long-session and connected-MP acceptance remain
open. No game launch, GitHub push, Steam text edit or public upload was performed.
