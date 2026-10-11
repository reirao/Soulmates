# Soulmates 0.25.1 Publication Verification

October 11, 2026. After installation the creator reports "klappt jetzt" and
explicitly requests GitHub/Steam publication and text updates. This is a
positive current playtest report, not exhaustive beta acceptance.

## Initial GitHub Publication

Release: https://github.com/reirao/Soulmates/releases/tag/v0.25.1

- Published at 21:39:57 CEST (19:39:57 UTC), as a prerelease/beta.
- Release source commit: `a55b5941ad584d12c846cb8fcfb3bc21c51deb4d`.
- Annotated tag: `v0.25.1`; both main and codex/living-souls were fast-forwarded
  together to the source commit. Neither branch was force-pushed or deleted.
- Sole mod asset: `Soulmates.tmod`, 1,648,254 bytes.
- SHA256: `341F1C03E79FBC023DA6C77F3AC0DA2F8458BB1AF7448131934BB27CFED65817`.
- GitHub's asset digest and the separately downloaded file match the candidate.
- Valid native payload, 114 entries, all 86 production sources match checkout;
  no probes, private notes, player/world saves or gallery content are packaged.

The published metadata rebuild passes 97,809 native assertions and the
independent audit with zero failed safety expectations (490 expectations).
Native compilation has zero warnings/errors. Source and catalog changes were
already covered by the 58,547 static/localization checks in the wiring pass;
this publication does not claim a new static assertion total.

The installed candidate remains 1,648,214 bytes, SHA256
`0667B514335D06CB3A03AB3EA175D5C1845051E8A0346D0CA6E021DBD1EB4457`.
All production sources and catalog bytes match the published rebuild. Of 114
entries, 108 are identical; four description/changelog files, Info and the
recompiled DLL differ. Source equality is not binary equality. The normal
client package and saves were not replaced during publication.

README, beta scope, release notes, full history, current one-release changelog
and both prepared Workshop descriptions are updated. English is 7,012 UTF-8
bytes; German is 7,296, each below 8,000. The current changelog summarizes only
changes since public 0.24.0, including the previously local 0.25.0 work, rather
than appending old release logs. Historical installation/preparation receipts
retain their original scope and hashes.

## Steam Upload And Current GitHub Asset

The creator subsequently clarifies that they already uploaded the binary.
No second upload was performed. Steam's published-file API for existing item
3807130821 reports update time **October 11, 21:32:32 CEST**, visibility 0 and
content handle `2062317107204218111`. The normal loader log records building
0.25.1 and submitting the existing-item update at 21:32:25. The Steam-managed
2026.8 package was copied read-only into the workspace for verification.

- Current package: Soulmates 0.25.1, loader 2026.8.3.0, **1,636,248 bytes**.
- SHA256: `A88D9E5ADD177FBB4F2F3BB2FC20AFC2201DB0D50744A8251E414F22AD7C360A`.
- Native payload and all 86 production-source hashes pass; 114 entries, with
  no tests, private notes, saves or gallery content packaged.
- Against the initial GitHub artifact, 98 entries match. Nine catalogs are
  reserialized as nested Hjson; structured comparison confirms all 997 keys
  and values per language are identical. Metadata, descriptions, DLL and PDB
  differ. This is not a binary-identical build of the earlier artifact.
- This exact Steam-managed artifact passes **97,809 native assertions** and
  **490 independent expectations**, with zero failures.
- GitHub's sole mod asset was replaced with this artifact, then downloaded
  again. Its package verification and SHA256 match exactly. The earlier GitHub
  download remains locally preserved; source tag v0.25.1 was not moved.

This verifies Steam-managed cache plus creator upload, official update metadata
and normal-loader logs. The public API returns no direct download URL; no new
independent CDN archive download or universal subscriber delivery is claimed.
The legacy 2026.7 package remains historical, not current-feature evidence.

## Steam Text: Saved And Verified

The first Codex editor attempt remained behind Family View. The creator then
unlocked that specific tab themselves; the visible description editor was
confirmed before editing. No PIN was collected or entered, and no alternate
authenticated write route bypassed the access control. No loader takeover
was needed after the binary-upload clarification.

On October 11, Steam confirmed both English and German description saves at
21:53 CEST. Returned editor values match the prepared files after newline
normalization; fresh English and German item pages display the current
0.25.1 text. The title remains Soulmates. English is 7,012 UTF-8 bytes and
German 7,296, both accepted without a size warning.

The existing changelog entry `1791747152` was edited in place and verified
on a fresh changelog page. It now describes changes since public 0.24.0,
including the previously local 0.25.0 work, without the stale claim that it
has not been installed or uploaded. Total entries remain **31**, unchanged;
no duplicate entry was added and older entries were left intact.

The uploaded artifact retains earlier prepared-description wording; these
web-text corrections do not require another binary upload. The existing
gallery remains untouched and accurately labeled as older footage.
The fresh item/changelog pages still show **Steam moderation pending**.
Each subscriber's loaded version remains a separate check.

## Evidence And Open Gates

Local receipts: `outputs/publication-0.25.1/package-verification.json`,
`github-release.json`, `github-package-verification.json`,
`installed-candidate-comparison.json`, `engine-checks.txt`, `audit-checks.txt`
and `github.jpg`. Follow-up receipts include `steam-file-details.json`,
`steam-cache-package-verification.json`, `steam-package-comparison.json`,
`steam-catalog-comparison.json`, `steam-engine-checks.txt`,
`steam-audit-checks.txt`, `aligned-github-release.json` and
`aligned-github-package-verification.json`. Web-text evidence includes
`steam-text-verification.json`, EN/DE save screenshots and before/after text,
`steam-en.jpg`, `steam-de.jpg`, `steam-changelog.jpg` and
`steam-moderation.jpg`. These outputs are excluded from Git and the mod asset.

The CLI's initial sandboxed credential check could not access Windows keyring.
The same existing login works outside the sandbox; no new credential or access
grant was created. The connected GitHub account was independently identified
as the repository owner. No credential values were stored in project files.

Full graphical, physical V, two-client multiplayer, endurance and progression
acceptance remain open. Positive creator feedback and fixture passes do not
close them. Public upload, moderation and loaded subscriber version are
distinct checks. No new gameplay screenshots or forum edits were performed.
