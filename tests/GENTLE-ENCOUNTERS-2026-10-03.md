# Soulmates 0.19.5: Gentle Encounters (Local Candidate)

## Feedback and Scope

The latest locally recorded 0.19.4 single-player session contains four actionable notes: placed-torch clicks opening menus, frequent questions, learnable wall deconstruction, and trap warnings. The player also reports unsatisfactory critter interaction.

The journal records four critter observations and later activation of Company, but no completed visit or Company binding. The observed creatures preceded the change to Company. That alone does not prove a failed encounter: the journal cannot establish that an eligible creature was present afterwards. No critter death is recorded in this session.

No raw private notes, character names, world saves, exact positions or journal files are copied into this report or published.

## Implemented

- In default Terraria mode, placed torches and frame-important furniture take priority over overlapping player, companion and loose-drop hitboxes. Trees and fallen logs retain their natural-resource context. Explicit Me/Soulmate modes remain intentional context tools.
- Add Commands > Personal questions, using native Sleep / Heart / Laugh symbols for Quiet / Calm / Chatty. Save the setting in the Sigil, clone and binary profile. Older Sigils default to Calm. Quiet disables personal offers, not Ask permission requests or manual interaction. Calm spaces personal offers by at least five minutes; Chatty uses two. The initial idle grace remains two minutes. Per-topic cooldowns and other eligibility rules can postpone offers further.
- Fix the curiosity route bypassing question cooldowns. Wallet, Company and critter-care offers share the same interval. Change settings without granting work permissions; cancel an existing unwanted personal question.
- Explicit Company/Collect visits replace pending personal questions and existing assignments. Brief defense and Pause preserve the chosen visit and its remaining deadline. Direct orders still reject disabled autonomy, current combat, invalid identities and ineligible targets.
- A directed six-second timeout reports failure, briefly defers that target from automatic retry and leaves an explicit retry possible. Timeout feedback is a direct target-order line, not an ambient line that existing speech can suppress.
- Automatic critter selection runs on authority, requests native synchronization and arrives through ExtraAI. Clients do not choose competing targets. Sight checks use native hitboxes, including ground-level approaches.
- Real critter friends start catching up beyond 140 pixels and settle within 64. In between, they retain the previous catch-up state rather than reversing at a single threshold. Ground friends use horizontal separation; flying friends use full distance. A changed network binding resets the movement state. Native wandering, collisions, health, catch drops and despawning remain intact.
- Do not initiate phantom visits when Company is already full. Empty passive Watch scans wait two seconds before retrying instead of scanning every tick. Native catches, gold/statue/released protections and one ordinary / three AETHER friends remain unchanged.
- Add ten localized labels/replies in all nine catalogs and retain the prior Little Replies fixes.

## Recorded, Not Implemented

- Learnable wall deconstruction. Requires separate player permission, native hammer eligibility and protection against dismantling homes or player construction.
- Trap recognition and warnings. Requires native hazard classification, bounded visibility and a warning cadence that does not repeat the question-spam problem.
- Companion-owned vanity-pet summons remain unimplemented. Pet items can still be carried as cargo. Existing Company consists of real supported world critters, not player vanity pets.

## Verification

October 3, 2026, against local tModLoader 2026.8.3.0:

- Main MSBuild and regression probe: zero compiler errors or warnings.
- Native compiler/package: zero compiler errors or warnings.
- Final packaged native-engine run: **39,737 assertions, zero failures**.
- Static run: **47,915 assertions, passed**; 845 keys in nine languages, 436 source references.
- Diff whitespace check: passed.
- Packaged description: **7,369 UTF-8 bytes**, below Steam's 8,000-byte limit. Upload changelog contains only 0.19.5; history stays in `CHANGELOG.md`.

Focused fixtures cover saved/legacy/invalid preferences, common offer intervals, client mutation rejection, direct critter context replacing a personal question, combat and Pause retention, timeout feedback/retry exclusion, actual companion AI completing a free approach, native walking AI plus Company guidance, comfortable-distance hysteresis, full Company limits, empty Watch scan cadence, automatic target serialization and recall cleanup. The approach fixture integrates the flying companion's velocity against a stationary native critter; it is not a live pursuit or obstacle-navigation playtest.

Native UI tests dispatch the question symbols through production hit testing and actions in nine languages, at four resolutions (640x360 to 1920x1080) and three UI scales (0.85, 1, 1.5). They also check Back and switching between personal settings and work permissions. Torch tests overlap actual player, companion and loose-item bounds. These are geometry/input checks, not rendered screenshots.

An early run exposed suppressed timeout speech; the direct-order localization route fixes it. Other initial fixture failures came from old question timing assumptions and mouse rounding at exact tile boundaries. Updated fixtures retain those behavioral assertions. Final packaged checks pass. Expected malformed-packet warnings and denied optional registry association are not assertion failures.

**No graphical client playthrough, screenshots, long session or connected multiplayer test was performed in this pass.** Escaping critters, terrain obstacles, visible timing and real network latency remain player checks. Most assertions are existing broad regression coverage, not separate gameplay sessions.

## Artifact and Status

`outputs/gentle-encounters-0.19.5/Soulmates-0.19.5.tmod`

- Size: **1,493,760 bytes**.
- SHA256: **3C4840C3FB6492624D2D9613315B3BEB400100FB97778F1CD3CF84E62382B16B**.
- Engine result: `outputs/gentle-encounters-0.19.5/Soulmates-engine-checks.txt`.

Prepared and tested locally, then **installed into the normal client at the player's request on October 3 at 18:18 CEST** after checking that no game/runtime process was running. **Not committed, pushed or published.** Multiplayer peers require the same version; saved Sigils retain backward-compatible defaults. The regression probe stays isolated from the normal client. No Workshop package, character or world save was changed.

The normal `Mods/Soulmates.tmod` has the exact tested hash and size above. Its prior file was verified and backed up as `Soulmates-before-install-0.19.5-20261003T161702Z.tmod` in the output directory, SHA256 `4A37F1A865A6E1D92CA579CB1E8A6F18424AE82D267AAEF3E9CBB226676AF5A1`, size 1,479,828 bytes. No version is inferred from that old hash. Windows rejected the attempted atomic replacement without changing the target; ordinary copying succeeded, followed by package, backup and enabled-list verification. The temporary staging file was removed. Enabled mods remain only Soulmates. The installation receipt is `outputs/gentle-encounters-0.19.5/installation-receipt.json`. The client was not launched; installation verification is not a graphical playtest.

## Player Checks

1. Confirm 0.19.5, select Company, and invite a nearby natural bunny, squirrel or supported bird/insect with its context menu. A carried net is needed for Collect, not Company. A friend retains native terrain constraints; no teleporting is promised.
2. Check a selected visit during brief combat and Pause/Resume. An unreachable direct visit should report its timeout; Off and recall release real creatures safely.
3. Select Quiet / Calm / Chatty under Commands. Work Ask prompts should still appear on Quiet. Wallet and curiosity offers should share the selected spacing, and the choice should survive saving and resummoning.
4. Right-click placed torches/furniture near the player or companion in Terraria mode; loose drops and native NPC interactions must still work. Deliberate Me/Soulmate mode should retain context inspection.
5. Report concrete action, result, creature and solo/host/client context through the local mailbox. Check graphical readability and connected multiplayer separately.
