# Soulmates 0.24.0 Publication Verification

October 9, 2026. The creator reports a positive first playtest and requests
publication updates. This is release housekeeping and isolated verification,
not a new graphical playthrough or completed beta acceptance.

## Public Package

Steam's existing item 3807130821 records the update at **14:43 CEST**, with
moderator approval still pending at this check. The existing changelog entry
`1791549787` was corrected in place from Local Test Candidate to Beta Candidate;
no new entry or cumulative historical log was added.

Its direct download is `depot_1281930_6337659682115347606.zip`. The archive
contains a current `2026.8/Soulmates.tmod`, a legacy `2026.7/Soulmates.tmod` and
workshop.json. The legacy loader branch is not claimed to contain current
features; these checks concern the 2026.8 package.

- Internal name/version: Soulmates 0.24.0, loader 2026.8.3.0.
- Current package: **1,601,397 bytes**.
- SHA256: `C299868125083DE7E0924C4C26FD9F05A6CB8663DB9BD928DF3EA137B2413362`.
- Valid native payload hash, 111 entries, all 83 production source hashes match.
- No tests, probes, private notes, saves or gallery content in that package.
- Nine catalogs each contain 949 keys; every key/value matches the originally
  tested candidate semantically. The loader reserialized flat/nested Hjson.
- Actual DLL/PDB hashes differ, so source equality was not called binary equality.
- Direct Workshop package rerun: **94,235 assertions, zero failures**.
- Separate audit rerun: **198 expectations, zero failures**.

An initial attempt to run against Program Files could not create the native
log and exited before tests. The successful runs use the existing isolated
runtime and fresh workspace-only save roots, never the player's save directory.
Malformed-packet and blocked-write diagnostics in those runs are deliberate
negative fixtures. No normal client package or save was replaced in this pass.

The earlier candidate installed at 14:26:56 CEST remains accurately recorded:
1,613,027 bytes, SHA256
`43F5C21289E0008D5A672BFCBA127407D128D93BF4927BE675FA90DE8A639E58`.
Its [installation receipt](CLIENT-INSTALL-0.24.0-2026-10-09.md) is not rewritten
to describe the later rebuild. Both runs use identical production source.

## Sources And Public Text

Release code/tag: `028cca5`, `v0.24.0`. Both existing GitHub branches are updated
together; the release is a prerelease, not a claim of stable 1.0 acceptance.
GitHub's sole mod download is aligned to the verified Workshop package; its
release notes preserve the earlier installation hash and this later countercheck.

README, BETA, release notes, development history, one-update changelog, English
and German Workshop descriptions describe the current scope. The mod-source
directory is a junction to this checkout, not an independent stale source copy.
An attempted metadata self-copy was rejected; no second source tree was created.

Steam English and German descriptions include the learning feedback, consent
priority, local-only notes and honest pet/building limitations. The first near-
limit German form was rejected by Steam despite a raw UTF-8 count below 8,000;
it was shortened with extra margin. The final server-side save confirmation and
freshly loaded public German and English descriptions were verified. Local
prepared texts match the submitted descriptions after newline normalization.
The forum's existing opening post and title are updated to 0.24.0, preserving the
personal development diary, V-key correction and disclosure. No new advertising
reply is added, and older media is not relabeled as current gameplay.

Local receipts under `outputs/publication-0.24.0/` preserve package integrity,
entry comparison, semantic catalog comparison and browser evidence. Native
reruns are in `work/workshop-0.24.0-regression/` and
`work/workshop-0.24.0-audit/`; they are excluded from the release.

## Remaining Gates

Steam approval and universal subscriber delivery are not established. The
creator's encouraging playtest does not close physical V input, every rendered
control, progression, endurance or connected multiplayer. Existing beta gates
remain in [BETA.md](../BETA.md). No free-building, copied architecture or online
AI behavior is advertised.
