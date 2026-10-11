# Soulmates 0.26.0 Publication Verification

October 11, 2026, Europe/Berlin. The creator reports a positive playtest,
clarifies that the pet icons are not a duplication bug, and requests GitHub,
Steam and forum publication. No gameplay code was changed during publication.

## GitHub And Package Identity

Release: https://github.com/reirao/Soulmates/releases/tag/v0.26.0

- Beta/prerelease published at 22:57:06 CEST.
- Source commit: `89dbe55456358f85f936887a63acb7e4f36a2d4c`.
- Annotated tag `v0.26.0`; remote main and codex/living-souls advanced together
  without force-push or branch deletion. The tag is not moved by later receipts.
- Initial metadata rebuild: 1,682,981 bytes, SHA256
  `ABB9E88DE4E20D7C9332F78C053680CAB2D2FBCB7E19F4A941130FD3E5AA3612`.
- The current sole mod asset is aligned to the already uploaded Steam package:
  **1,671,094 bytes**, SHA256
  `53049F4018FFAC1FD8B486A09E42C46B13B43EDBBC5D85B1E50636F19BD0CB5A`.
- GitHub's asset digest and separately downloaded file match that exact package.
- Native loader 2026.8.3.0, valid payload, 119 entries, all 91 production source
  files match checkout. No probes, private notes, saves or gallery files included.

The rebuilt artifact and the exact Steam-managed artifact each pass 100,745
native assertions and 490 independent audit expectations with zero failures.
The production rebuild has zero compilation warnings/errors. The regression
probe has 33 existing nullable/Hjson compilation warnings; these are not
production warnings. Static checks pass 60,002 assertions, 575 source references
and 1,023 complete keys in each of nine languages.

Of 119 package entries, 103 match the metadata rebuild. Nine catalogs are nested
Hjson rather than the source's partly flattened format. Structured Hjson
comparison confirms all 1,023 key/value pairs per language are identical.
Info, descriptions/changelog, DLL and PDB differ. Matching source is not binary
identity; the separately retested Steam artifact is the distributed download.

## Existing Steam Upload

Item: https://steamcommunity.com/sharedfiles/filedetails/?id=3807130821

While publication work was underway, the existing item acquired a 0.26.0 update
at **22:50 CEST**, changelog entry `1791751856`, content handle
`470054996806133028`. The normal loader log records submitting the update at
22:50:49, then loading 0.26.0 and entering a world. Its Steam-managed 2026.8
package was copied read-only and verified/retested as above.

No second upload, game takeover or client-package replacement was performed.
An earlier request to enter the main menu became unnecessary once this upload
was verified. The historical 2026.7 package is not current-feature evidence.
Steam-managed cache, loader log and visible item update agree; no independent
CDN archive download or universal subscriber delivery is claimed.

English description saved at **22:57 CEST**, German at **22:58 CEST**. Fresh
item views show 0.26.0, personal undertakings, retinue, actual pet ownership,
beta limitations and local-only feedback. Prepared source sizes are 7,587 and
7,560 bytes respectively, below Steam's 8,000-byte limit. English retains the
standard tModLoader developer footer. Both saves were accepted.

The current changelog was corrected **in place**, removing the stale local-only
preparation claim. Total entries remain **32**. Its body contains only 0.26.0
changes; old entries and gallery media were not copied into it or deleted.
The uploaded binary retains earlier prepared-description wording; correcting
web descriptions does not require another binary upload.

Steam still shows **moderation pending**. Approval and each subscriber's loaded
version are separate from successful upload and owner-visible text.

## Terraria Forum

Existing thread: https://forums.terraria.org/index.php?threads/152194/

The opening post `3484785` and title were edited, saved and verified. Title:
**Soulmates - Personal Companions & Soulwheels (0.26.0 Beta: Little Company)**.
The new text covers personal undertakings, real-item retinue, ordinary limits,
AETHER, home, optional EVIL, item ownership and matching peer versions. The
current GitHub link now points to v0.26.0.

The personal introduction, invitation to playtest, limited available playtime,
gradual media updates, AI/asset/privacy disclosure and development diary remain.
The diary preserves cargo/critter mistakes and the unverified physical V claim.
No new promotional reply or duplicate thread was added; other replies remain.

## Scope And Local Evidence

The installation receipt preserves the earlier installed candidate hash. This
publication does not relabel that candidate as byte-identical to the upload.
No new game session, physical key test or gameplay screenshots were performed.
Graphical families, progression, endurance and connected two-client multiplayer
remain open. Positive creator feedback and fixtures do not close these gates.

Local generated evidence under `outputs/publication-0.26.0` includes package
verification, entry/catalog comparisons, Steam engine/audit reports, initial
and aligned GitHub downloads, and GitHub/Steam/forum screenshots. These outputs
are excluded from Git and the mod package. No private play logs or saves were
published. The source's historical installation and test receipts are retained.
