# Soulmates 0.25.1 Publication Verification

October 11, 2026. After installation the creator reports "klappt jetzt" and
explicitly requests GitHub/Steam publication and text updates. This is a
positive current playtest report, not exhaustive beta acceptance.

## GitHub: Published And Download Verified

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

## Steam: Awaiting User Actions

Existing Workshop item: 3807130821. Its visible public page before editing
still showed 0.24.0 and the October 9 update, with moderator approval pending.
The description editor and changelog then require the locked Family View PIN.
The user has been asked to unlock it themselves; no PIN is collected or entered.
No alternate route is used to bypass this access control.

Native activation of the running tModLoader window was rejected to protect the
user's live game. The user has been asked to save, return to the main menu and
explicitly permit window takeover. No retry, focus workaround, game closure or
native input followed that rejection. If the upload's final confirmation also
accepts terms, obtain action-time approval before that step.

No 0.25.1 Workshop binary upload, server-side description save, changelog edit,
moderation approval or subscriber delivery is established by this record.
Both descriptions and the single-release changelog are ready locally. The
existing gallery is left untouched and remains labeled as older footage.

## Evidence And Open Gates

Local receipts: `outputs/publication-0.25.1/package-verification.json`,
`github-release.json`, `github-package-verification.json`,
`installed-candidate-comparison.json`, `engine-checks.txt`, `audit-checks.txt`
and `github.jpg`. These output files are excluded from Git and the mod asset.

The CLI's initial sandboxed credential check could not access Windows keyring.
The same existing login works outside the sandbox; no new credential or access
grant was created. The connected GitHub account was independently identified
as the repository owner. No credential values were stored in project files.

Full graphical, physical V, two-client multiplayer, endurance and progression
acceptance remain open. Positive creator feedback and fixture passes do not
close them. Public upload, moderation and loaded subscriber version are
distinct checks. No new gameplay screenshots or forum edits were performed.
