# Soulmates 0.22.0: Critter scheduling and character revision

Date: October 5, 2026. Exact local package checked; [normal-client installation](CLIENT-INSTALL-0.22.0-2026-10-05.md) is a separate subsequent step. Not published.

## Findings and Reproduction

Read the creator's existing opt-in field notes locally. The 0.21.0 session recorded 74 automatic jobs (46 gathering, 14 mining, 12 forestry, two treasure), one critter observation and no Company/Collect job. This suggested starvation, but a journal alone does not prove the cause. No raw field notes or saves are included in the output bundle.

The added bounded native fixture, run against unchanged 0.21.0, reported 87,922 assertions and four failures: Company failed to enter shared opportunity scheduling, and an unrelated personal question blocked an Always-approved visit, in both single-player and server-authority modes. The historical report and old package are preserved in outputs/critter-character-0.22.0/baseline-0.21.0-failures.txt and baseline-0.21.0.tmod.

The subsequent Ask-policy negative fixture needed a real headless SpriteViewMatrix and UI eligibility flags; its intermediate missing-camera exception was a harness problem, not a reproduced graphical-client failure. The final test explicitly supplies that native camera fixture and restores globals. Existing permission tests now enter the shared scheduler before executing visits; safety assertions were retained, not removed.

## Repairs and Scope

- Company/Collect join the existing attention scheduler rather than running an independent automatic selector. Aging makes critter opportunities compete with gathering, mining, forestry and treasure.
- Approved Always visits can coexist with existing personal questions. Questions remain answerable. Ask does not displace an unanswered personal question or authorize a catch; Never still prevents automatic work.
- New resident visits and new personal questions do not interrupt active critter visits. Explicit work, pause, defense and energy-recovery guards retain priority.
- Active targets validate NPC object identity as well as slot/type; replacement cannot inherit the old visit.
- Details/V now separates Conversation, Equipment and Diagnostics, with a larger portrait/vitals area, independent reply strip, tool/trinket displays, pet choices, cargo and actual task controls. Narrow cargo actions wrap; scroll is separate from cargo paging.
- Tools are carried capability displays, not a generic armor/weapon equipment system. Existing right-click trinket removal and real item-backed pet selection use authoritative production operations.
- Read-only diagnostic state includes authority, task, stamina, policies, questions, nearby critter eligibility, target/timeouts, native catch refusals, witnessed death/cooldown/queued state and owned pet projectiles. Recent transitions are capped at 24. Only explicit export writes a fixed local file; no recording activation or upload. MP labels client-view scope.
- Item pets remain limited to Zephyr Fish/Nectar. Native art/frames use custom harmless following; no extra player buff slot or arbitrary vanilla pet AI. Company only supports existing whitelisted natural species and retains collisions, health/despawn. No combat swarm or universal pathfinding is claimed.

## Exact Final Evidence

| Check | Result |
| --- | --- |
| MSBuild production | Zero warnings/errors |
| Native tModLoader production compiler | Zero warnings/errors |
| General native regression | 90,441 assertions, zero failures, exit 0 |
| Separate native safety audit | 174 expectations, zero defects, exit 0 |
| Static/controller/localization | 54,125 assertions; 918 keys in nine languages; 477 source references; pass |
| Source/stage agreement | All 112 selected production inputs hash-match canonical staging |
| Description byte caps | Plain 7,561; English Workshop 7,769; German Workshop 7,902; each below 8,000 |
| Diff whitespace | git diff --check passes; CRLF conversion notices only |

Raw final reports and the package/source manifest are in `outputs/critter-character-0.22.0`. Package internal name is Soulmates, version 0.22.0, loader 2026.8.3.0; 1624854 bytes, SHA256 `1CC6C12688FCF1F945A1543A7532EFFBA2A27A3F2D8BDA5FCCC3CC1080DD899E`.

The general test-only probe has 33 known nullable/Hjson compatibility warnings; production and the separate audit have none. Registry association denial in the isolated restricted server is not a gameplay result. Malformed packet and blocked-write diagnostics are deliberate negative controls.

## What Ran

Native Terraria/tModLoader libraries and actual production hooks/operations in isolated save roots, exiting before a real world or network listener is opened. Coverage includes native catches/exact receipts, Company visits, 30-second bounded NPC-AI scheduling under competing loot, pause/resume/abort, death/greeting queues, owner/target lifecycle, gold/statue/released protection, cargo/wallet conservation, equipment-item-backed pet frames/following, save/clone/wire transport, duplicate cleanup, withdrawal/recall, player-pet coexistence, delayed client profiles and stale network requests.

Character UI coverage uses native layout and hit-testing/events, a measurement-only font, nine cultures, viewports 800x600/1280x720/1920x1080, and requested UI scales 1/1.5/2 subject to Terraria's native clamps. It checks tabs, reply/body separation, pet/cargo overlap, pet selection/switch/dismiss, task controls, local export and identity/type-bound acknowledgements. Those acknowledgements are controlled UI-state fixtures, not a live connected round-trip.

The independent audit confirms shared critter attention, Always/question coexistence, unchanged binary profile while reading diagnostics, replacement identity rejection, Never and explicit client-authority labeling, alongside all preceding safety boundaries.

## Open Acceptance

Windows desktop control failed initialization, including one reset/retry. No actual graphical client play, new screenshots, pixel/rendering checks or human-like full session is claimed. User saves were not loaded or changed by probes. Live perceived usefulness, visual greeting/death timing, equipment layout/art quality, full progression, long sessions, and two connected clients remain unverified.

Use [the focused checklist](PLAYTEST-CRITTER-CHARACTER-0.22.0-DE.md). A passing isolated fixture does not establish that every reported live critter problem is fixed. The debug view is intended to make remaining gates observable, including full cargo, blocked sight, unsupported species, pause/routines, recovery and consent.
