# Normal-client installation: Soulmates 0.20.0

Verified on October 5, 2026, at 09:45 CEST. The user requested this local
installation and a playtest checklist. This did not publish a release.

## Installed Package

- Destination: `C:\Users\AETHER LUX\Documents\My Games\Terraria\tModLoader\Mods\Soulmates.tmod`
- Internal name/version: `Soulmates`, `0.20.0`
- Package loader: `2026.8.3.0`
- Size: `1561376` bytes
- SHA256: `8381D6D0E35D533367CD800007259044EFB82758732141CCAAC7846674225580`
- Source: `outputs/beta-candidate-0.20.0/Soulmates.tmod`

Installed header and SHA256 match the verified candidate. No tModLoader/dotnet
process was running at installation. The normal Mods directory contains one
`.tmod`, Soulmates. Existing `enabled.json` still enables Soulmates and its
SHA256 is unchanged:
`BCE083542F7274DEEE54547E76666CE3889050729A311C5B3654C57EBE9D6B40`.

## Backup

Workspace directory: `work/client-backups/before-0.20.0-20261005-094450`.

- Previous `0.19.8` package, independently hash-verified:
  `292314015FD5B12E82A6FD522AA28FFF6FC88F649FC10DAF24C6498B8F3263B7`.
- Unmodified `enabled.json`.
- `Saves/Players`: all 86 local player/map/backup files, each hash-verified.
- `Saves/Worlds`: 23 current `.wld`/`.twld` files, each hash-verified,
  totaling 51,295,624 bytes. Historical world backups were not recopied.

Original saves, Steam Workshop-managed packages and existing backups were not
changed. No probe/test mod was installed in the normal client.

## Remaining Acceptance

File installation is verified, not a fresh rendered playtest or proof of the
version loaded after startup. First confirm **0.20.0** in Mods. Earlier candidate
reports remain historical build evidence; playtest/publication gates stay open.

Use [the German checklist](PLAYTEST-CHECKLIST-0.20.0-DE.md), prioritizing creation,
native right-click, mixed pickup, permissions, task control and save/reload.
