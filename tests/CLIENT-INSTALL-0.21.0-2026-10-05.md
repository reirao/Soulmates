# Normal-client installation: Soulmates 0.21.0

Verified on October 5, 2026, at 13:37 CEST. The user explicitly requested
installation into their normal client. No release was published in this step.

## Installed Package

- Destination: `C:\Users\AETHER LUX\Documents\My Games\Terraria\tModLoader\Mods\Soulmates.tmod`
- Source: `outputs/clear-intentions-0.21.0/Soulmates.tmod`
- Internal name/version: `Soulmates`, `0.21.0`
- Package loader: `2026.8.3.0`
- Size: `1595209` bytes
- SHA256: `ED9E08EFE267AC24EB89AA4E2F0356953A1CC6308851B62509DBB7E175BB811D`

Installed header and SHA256 match the verified candidate. No tModLoader,
dotnet or Terraria process was running immediately before replacement or at
verification. The normal Mods directory contains one `.tmod`, Soulmates.
Existing `enabled.json` still enables Soulmates and has unchanged SHA256:
`BCE083542F7274DEEE54547E76666CE3889050729A311C5B3654C57EBE9D6B40`.

## Backup

Workspace directory: `work/client-backups/before-0.21.0-20261005-133640`.

- Actual previous installed package: `0.20.0`, `1551362` bytes, SHA256
  `AD15C654F2793E43EE35258A433ACE691910F00D5E1FE08F37F05074BEA40A8B`.
  This differs from the package recorded during the earlier 0.20.0 installation;
  the current file was independently inspected and backed up, not assumed.
- Existing `enabled.json`.
- All 86 local `Players` files, including map and player backups.
- 23 current `.wld`/`.twld` files from `Worlds`; historical world backup archives
  were not recopied.
- All 111 backup files hash-verified, totaling `53910485` bytes. The generated
  `backup-manifest.json` records every original relative path, size and SHA256.
- All 110 original non-package files rechecked unchanged after installation.

Original saves, Steam Workshop-managed packages and existing backups were not
modified. No probe/test mod was installed in the normal client.

## Remaining Acceptance

This confirms file installation, not a newly rendered playtest or the version
loaded by the game after startup. Confirm **0.21.0** under Mods, then use
[the German checklist](PLAYTEST-CLEAR-INTENTIONS-0.21.0-DE.md).
All multiplayer peers need the same version. Publication and graphical playtest
gates remain separate from local installation.
