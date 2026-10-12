# Soulmates 0.27.0 Publication Verification

October 12, 2026, Europe/Berlin. The creator reports a delighted installed
playtest and authorizes GitHub, Steam and existing Terraria forum updates.
This publication changes documentation, not gameplay behavior.

## GitHub And Package Identity

Release: https://github.com/reirao/Soulmates/releases/tag/v0.27.0

- Beta/prerelease published at **11:47 CEST**.
- Source commit: `bb9185740ae5a7bb68564c238873c0747bbcb483`.
- Annotated tag `v0.27.0`; remote main and codex/living-souls advanced together
  in one atomic, non-forced push. Subsequent receipt commits do not move the tag.
- The sole mod asset is the verified Steam-managed 2026.8 package:
  **1,682,492 bytes**, SHA256
  `667B907B1A2AE28A9D43E2BACF3B2E2DB93BEDEF5406F9B113B8628A762EC1E3`.
- GitHub's displayed asset digest and a separately downloaded public file match
  that exact package. Native payload integrity is valid, loader 2026.8.3.0,
  122 entries, all 94 production sources match checkout. No probes, private
  notes, world/player saves or gallery files are included.

The exact Steam-managed file separately passes **116,213 native assertions**
and **501 independent audit expectations**, with zero failures. Publication
static/localization checks pass **60,730 assertions**, 587 source references
and 1,036 complete keys per language in nine catalogs. Production compilation
previously completed without warnings/errors; this regression probe compilation
retains 33 existing nullable/Hjson warnings, while the audit probe has zero.

The installed candidate remains 1,695,161 bytes, SHA256
`7C65EAF9A968EA9E4289C6400CD57E084AC1BFC44B73698B4C764458A1E1C2CD`.
Its installation receipt is not relabelled as byte-identical to the upload.
Of 122 entries, 111 match. Nine Hjson catalogs differ in formatting; structured
comparison confirms all 1,036 key/value pairs in each language are identical.
DLL/PDB also differ. Source equality is not binary identity; the uploaded file
was independently retested before being used for the GitHub asset.

## Steam Workshop

Item: https://steamcommunity.com/sharedfiles/filedetails/?id=3807130821

The existing item records the 0.27.0 upload at **11:26 CEST**, changelog entry
`1791797199`, content handle `2362956937181287632`. Its Steam-managed 2026.8
package was copied read-only into the workspace and checked as above. The
older 2026.7 package remains historical, not current-feature evidence.
No second upload or game takeover was performed. No independent CDN archive
download or universal subscriber delivery is claimed.

English and German descriptions were saved at **11:40 CEST**. Fresh item views
show the boss headline, six grouped feature sections, practical controls,
implementation details, beta limitations, privacy and AI/asset disclosure.
The English source is 7,605 UTF-8 bytes, 7,665 with the native developer footer;
German is 7,775 bytes. Both are below Steam's 8,000-byte limit and both saves
were accepted. The current changelog was corrected **in place**, contains only
0.27.0 changes and no longer calls the uploaded package uninstalled/unuploaded.
The count remains **33 entries**, including the existing new upload.

The binary retains earlier prepared-description wording. Correcting current
web descriptions does not require another binary upload. Steam still shows
**moderation pending**; approval and subscriber loaded versions are separate.

## Terraria Forum And Feature Guide

Thread: https://forums.terraria.org/index.php?threads/152194/

Opening post `3484785` and title were edited, saved and verified:
**Soulmates - Personal Companions & Soulwheels (0.27.0 Beta: Become the Boss)**.
The boss duel is prominently featured with its confirmation path, real-damage
warning, interruption controls and no-loot/XP rule. All features are grouped
into creation/growth, wheels/control, useful work, personality/home/memory,
critters/pets/retinue and cargo/character. The implementation section explains
local state selection, consent, persistent Sigils, real-item transactions,
native APIs, bounded custom helper behavior, exact XP and temporary boss state.

The personal introduction, feedback invitation, limited playtime, gradual
media updates and AI/privacy disclosure remain. The diary preserves cargo,
critter, release and unverified-V mistakes, and adds the native boss ownership
countercheck. No new thread or promotional reply was added; others' replies
and existing gallery media were not changed. README and release notes carry
the same current feature organization and beta boundaries.

## Scope

No new graphical playtest, physical key test, gameplay screenshot or video was
created. Browser screenshots document publication only. Full progression,
endurance, graphical families and connected two-client multiplayer remain open.
The creator's positive playtest is encouraging, not exhaustive acceptance.

The additional request for gems, pots, cobwebs and consent-first decoration/
dungeon/underwater salvage is **not implemented in 0.27.0** and was not added
to the public feature claims. Unknown old tile origin must not imply ownership
or permission to dismantle player construction.

Local generated verification, entry/catalog comparisons, native reports,
public GitHub download and browser screenshots live under
`outputs/publication-0.27.0`, excluded from Git and the mod package. No private
play notes or saves were published. Normal client/source files were not
replaced during publication; the existing installed candidate remains separate.
