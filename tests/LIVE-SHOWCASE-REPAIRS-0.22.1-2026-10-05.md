# Live Showcase Repairs: Soulmates 0.22.1

October 5, 2026. Local beta repair candidate, not installed or published.

Follow-up: the exact package was installed after explicit user authorization on October 6. See the separate [client installation record](CLIENT-INSTALL-0.22.1-2026-10-06.md). The following text preserves the original repair-pass scope; file installation does not add a rendered playtest or public release.

## Scope And Isolation

This pass follows the genuine [0.22.0 Classic showcase](LIVE-SHOWCASE-0.22.0-2026-10-05.md), not a fresh whole-code audit or a new graphical playtest. The computer is shared with another instance. No mouse/keyboard control, game activation/closure, normal-client replacement, screenshots or public uploads were attempted during these repairs.

The headless runtime is isolated in `work/showcase-repair-runtime`, with separate regression/audit save roots. It uses the installed tModLoader 2026.8.3.0 and Terraria 1.4.4.9 libraries. Production compilation uses a separate source stage. Neither test probe is installed in the normal Mods directory. No network listener or player/world gameplay save is created by the probes.

## Reproduction And Critical Recheck

Against the unchanged 0.22.0 package, the expanded suite produced **90,455 assertions and 10 failures**: six single/full withdrawal expectations at three requested UI scales, two unanswered-question lifetime failures, missing icon animation and incorrect loader labeling. That package is the exact installed baseline: SHA256 `1CC6C12688FCF1F945A1543A7532EFFBA2A27A3F2D8BDA5FCCC3CC1080DD899E`, 1,624,854 bytes. The baseline failure report and package remain in the local output directory.

The first event-coordinate repair did **not** fix cargo: six failures remained. Further inspection of native `UIElement.Append`, `Activate` and click dispatch identified the main defect. Equipment was hidden during the first state activation; appending it later did not initialize its cargo subtree. Items could render while cargo tabs and withdrawal event handlers did not exist. Selecting Equipment now activates the subtree; repeated selection initializes it only once. Using the actual event mouse position also removes dependence on raw mouse globals.

When handlers began working, the fixture reached native local-player recipe refresh before `PostAddRecipes` had finalized recipe lookups. The test now calls the native lookup initializer before exercising transfers; no production exception suppression or recipe bypass was added. A separate initial harness startup lacked ReLogic resolution because of its working directory; using the isolated runtime directory corrected it. An initial MSBuild invocation needed both TargetFramework and TargetFrameworks when BuildMod was disabled. These setup failures are not presented as gameplay failures.

## Repairs And Evidence

| Repair | Countercheck | Remaining limit |
| --- | --- | --- |
| Cargo initialization and event coordinates | Native production hit target; right-click 12 -> 11 with one Wood delivered, then left-click 11 -> 0 with 12 total delivered. Requested scales 1/1.5/2, deliberately unrelated raw mouse coordinates. Four cargo controls stay initialized once. | Not a newly rendered client test. Native UI scaling may clamp requested values. |
| Partial/full inventory safety | Only two free stack units deliver two of eight Wood; six remain. A repeated full-inventory click leaves quantities unchanged. | Connected multiplayer remains separate. |
| Pending speech lifetime | Personal and work questions survive 180 speech-state ticks beyond the former fade boundary; clearing the pending state permits normal expiration. | This verifies timer behavior, not every possible speech replacement or live answer timing. |
| Soulcore icon | Native item animation is registered; the installed Fallen Star initializer confirms eight vertical frames, five ticks and ping-pong. | Actual pixels need the next client check. |
| Corner minimap avoidance | Collision/viewport checks at 800x600, 1152x864 and 3440x1369, both speech sides. Try opposite side and free positions before normal clamping. | No-space fallback can still overlap; map skins, live movement and every UI/map scale are not graphically verified. |
| Diagnostic versions | The production text contains `BuildInfo.tMLVersion`, separate from Terraria's version. | Read-only diagnostic output, not a loader upgrade. |
| Critter reasons | Native natural-bunny positive control; inactive/dead/catch-item/friendly/town/boss/damage/statue/release rejection conditions, owner/search radius and replaced-object counterchecks. | These preserve eligibility. They do not establish live Company/capture/loss success. |

Critter diagnostics also include bounded native catch-item, friendly/life/damage/release/statue details and distances without names or exact world coordinates. Deferred automatic visits do not incorrectly label explicit visits. No permission, creature-protection, inventory-limit or save/network schema changes were introduced.

## Final Verification

- Production MSBuild: **0 errors, 0 warnings**.
- Native production compiler/package: **0 errors, 0 warnings**.
- Expanded native regression: **90,483 assertions, 0 failures**, repeated with the exact final package.
- Separate native safety audit: **174 expectations, 0 failures**, including older transaction/identity, permission, critter scheduler and owned-pet counterchecks. Earlier and final-package runs passed.
- Static/controller/localization checks: **54,125 assertions**, 918 keys in each of nine catalogs, 477 source references; passed.
- General test-only probe: 33 existing nullable/Hjson compatibility warnings. Audit probe: no compiler warnings. Expected malformed-packet rejection messages and a deliberately blocked-write control are negative tests; registry association permission warnings belong to the isolated environment.
- `git diff --check`: passed. Native package header, payload hash, entry bounds, test/private exclusions and all **79 packaged C# source hashes** match the repository. No probe or work/output directory is packaged.
- Prepared Workshop descriptions: English **7,790 UTF-8 bytes**, German **7,901**, both below 8,000. `changelog.txt` contains this patch only; historical logs stay in `CHANGELOG.md`.

Raw test summaries and generated `package-verification.json` are in `outputs/showcase-repairs-0.22.1`. Raw private gameplay Field Notes and saves are not included.

## Exact Candidate

- File: `outputs/showcase-repairs-0.22.1/Soulmates.tmod`
- Internal name/version: `Soulmates`, `0.22.1`
- Package loader: `2026.8.3.0`
- Size: **1,586,580 bytes**
- SHA256: `F904525719535DBFCA7530CF2A684E455A971858AAAFD5B1E2B3272C871B3290`

The normal-client package was re-read and still matches the 0.22.0 baseline hash. It was not replaced. Prepared description text is not a saved Steam description, Git push or public release. Do not enable test probes in a normal game.

## Still Open

1. Install only after the active game is saved/closed and shared UI control is available; verify the actual loaded 0.22.1 version.
2. Repeat genuine cargo clicks, icon display and unanswered questions at compact/wide viewports. Confirm no speech/map overlap or unwanted visual jumping.
3. Dedicated natural-critter run: explicit and automatic Company/Collect, Ask/Always/Never, death/greeting timing, pause/off/recall and real Zephyr Fish/Nectar summoning. Use the improved diagnosis to identify the actual blocked gate.
4. Exhausted tree reconsideration, full forestry types, completed tunnels/filters, pacing, progression and full save/reload equality remain outside this repair's acceptance.
5. Two connected clients, simultaneous transfers, reconnects and endurance sessions remain required.

The 20 genuine showcase images and gallery remain **0.22.0**, unaltered and locally prepared. No fresh 0.22.1 screenshots or completed visual acceptance are claimed.
