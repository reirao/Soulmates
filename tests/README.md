# Soulmates Regression Checks

These checks use the installed tModLoader libraries, without downloading test packages.

The current [0.20.0 beta candidate and critical recheck](BETA-STABILIZATION-0.20.0-2026-10-05.md) centralizes ability registration, consent eligibility and per-instance task dispatch. **43,741 native engine assertions, 156 separate safety expectations and 50,511 static/controller assertions pass.** Controller priority covers all 512 combinations; all 729 permission combinations round-trip and a hand-written 0.19.9 binary fixture matches exactly. That stabilization pass prepared the package without installation or publication; the subsequent local installation is recorded below. [Beta acceptance](../BETA.md) keeps graphical, progression, endurance and connected multiplayer work open.

The preceding [0.19.9 candidate and critical recheck](GAMES-AND-CRITTERS-0.19.9-2026-10-05.md) adds Games, a built-in basic net, independent critter consent and visible queued greetings, preserving preceding repairs. **145 independent native expectations, 42,922 general engine assertions and 48,366 static assertions pass.** New registration checks require every autonomous ability to have explicit policy mapping, persistence and rule buttons. This was prepared, not installed or published, and is not a rendered or connected multiplayer playtest.

The preceding repair goal and repeated critical recheck is [NATIVE-REPAIRS-0.19.8-2026-10-04.md](NATIVE-REPAIRS-0.19.8-2026-10-04.md), against local **0.19.8**. It repairs all six findings from the fresh 0.19.7 review and adds native use/consumption ordering, valid progression, stable resident identity and deferred event controls. **136 independent native expectations, 39,738 general engine assertions and 47,921 static assertions pass.** The report distinguishes the exact local candidate from installation/publication and graphical/connected multiplayer proof.

The preceding full review is [FULL-REVIEW-0.19.7-2026-10-04.md](FULL-REVIEW-0.19.7-2026-10-04.md), against unchanged local **0.19.7**. It covered all 66 production C# files and recorded five reproduced problem fields through seven failed expectations, plus a source-confirmed modded-resident persistence risk. That historical audit had **109 expectations: 102 pass, 7 fail**, with no harness errors. Its result is not replaced by the later repairs.

The preceding repair and critical recheck is [COMPLETE-REPAIRS-2026-10-04.md](COMPLETE-REPAIRS-2026-10-04.md), against local **0.19.7**. It addresses all nine findings from the preceding 0.19.6 review and adds inventory conflict, acknowledgement, duplicate-delivery and stable-material counterchecks. Its green results describe those bounded checks, not the newer failures above. Prepared is not installed or published.

The historical full source review is [FULL-CODE-REVIEW-2026-10-04.md](FULL-CODE-REVIEW-2026-10-04.md), against unchanged **0.19.6**. Eight additional defects were reproduced by nine failed expectations; a separate persistence inconsistency was source-confirmed. Its expanded audit had **80 expectations: 71 pass, 9 fail**, with no harness errors. The older native suite passed 39,737 assertions, and the static suite passed 47,916 assertions. Those passing older tests did not constitute an all-clear.

The preceding [repair pass](REPAIRS-2026-10-04.md) had 67 independent safety expectations passing. Those checks still pass in the expanded audit. Repairs cover cargo migration, captured companion/storage identity, unique work-question tokens, actual mining targets and nonblocking personal questions. The exact package remains prepared, not installed or published. These are isolated engine checks, not a graphical or connected multiplayer playtest.

**Historical audit:** [FULL-AUDIT-2026-10-03.md](FULL-AUDIT-2026-10-03.md) records four correctness defects, demonstrated by five failed expectations against 0.19.5, plus a personal-question gate on new automatic work. The 0.19.6 repair counterchecks now pass; the separate [SoulmatesAuditProbe](SoulmatesAuditProbe/README.md) must remain isolated. The historical report also distinguishes implemented, partial and open requirements. Passing counterchecks do not establish that every possible gameplay scenario is correct.

The last normal-client installation is **0.20.0**, with its package header and hash verified on October 5 at 09:45 CEST. The [installation record](CLIENT-INSTALL-0.20.0-2026-10-05.md) documents the preceding 0.19.8 package and verified local save backups. The [German playtest checklist](PLAYTEST-CHECKLIST-0.20.0-DE.md) keeps graphical acceptance separate from automated results. The candidate report above predates this installation. The creator subsequently reports a broadly working first playtest and Workshop publication; [0.20.0 release notes](../releases/0.20.0.md) describe current scope and remaining gates. The historical [0.19.5 installation](GENTLE-ENCOUNTERS-2026-10-03.md) was on October 3 at 18:18 CEST. Older native reports remain evidence for their original packages, not new playtest certifications.

## Static and Math Checks

From the repository root:

```powershell
dotnet run --project tests\Soulmates.Regression.csproj -- .
```

The harness parses all nine Hjson catalogs, checks source key references, enum names, placeholder parity, description byte limits, text wrapping, the production activity coordinator and UI coordinate conversions. Its small Terraria stub simulates the engine's world/UI screen-state switching; it does not simulate gameplay or rendering. Activity priority tests link the real production coordinator directly.

Malformed or unreadable catalogs now report their path and parser diagnostic, then exit with status 1 instead of throwing an unhandled exception. A deliberately malformed local fixture was checked separately from the passing production catalogs.

## Actual Engine Checks

The test-only `SoulmatesRegressionProbe` is excluded from the released mod. Build it against the current Soulmates assembly, place both `.tmod` files in an isolated save directory, and enable only those two mods there. Run tModLoader with `-server -nosteam -tmlsavedirectory <isolated-directory>`.

At `PostAddRecipes`, after item/NPC/emote hooks and mod networking IDs are initialized, it validates every packaged translation in all nine languages, saved-key migration, mixed-item cargo and reserve limits, independent clones, save/load, binary profile transport, connected ore traversal, and exposed-edge mining rules. It writes `Soulmates-engine-checks.txt` in that isolated directory and exits before loading a world. Never enable the probe in a normal play session. Earlier versions ran at `PostSetupContent`; they could not exercise finalized catch or emote-observer hooks.

For 0.12.2, the isolated engine run passed 2,669 assertions. The static/math run passed 2,013 assertions. Engine coverage now includes actual world-to-pack pickup transactions, exact conservation, full packs, ownership reservations, reusable Soulcores, UI input ownership, release handling, deferred initiative prompts, and speech visibility. The earlier 0.12.1 engine run passed 2,115 assertions.

For 0.13.0, 4,051 isolated engine/geometry assertions and 2,065 static/math assertions pass. New coverage includes all 20 resource tiers, independent equipment/resource slots, coin collection with full cargo, huge wallet balances, partial withdrawals into nearly full inventories, legacy migration on summon, clone independence, save/load, binary transport, resource page wrapping, and the production slot-bound calculation at six widths. Geometry checks exercise the actual arithmetic without rendering a client UI; they are not visual playtesting.

An actual single-player client pass and unaltered screenshots are documented in [PLAYTEST-2026-09-30.md](PLAYTEST-2026-09-30.md). Automated checks do not replace live multiplayer, progression, or long-session testing.

For the final 0.14.0 package on tModLoader 2026.8.3.0, 6,629 engine assertions and 2,178 static/math assertions pass. New coverage includes a 500-decision starvation stress check, independent cooldowns, saved individual permissions, changed item identity, timeout during combat, low mood/energy, read-only Look, directed forestry, a hovered companion not replacing captured UI, and speech visibility while a wheel is open. A bounded real-client control pass is recorded in [PLAYTEST-2026-10-01.md](PLAYTEST-2026-10-01.md).

Both MSBuild and the native release compiler without `-eac` report zero compiler warnings/errors for 0.14.0. Explicit nullable contexts removed the native compiler's annotation diagnostics. tModLoader's packager still emits `Image loading failed: unknown image type`; the package builds and loads successfully, but that diagnostic remains unresolved. The client also logged a small immediate asset-load warning for native wheel navigation. Neither diagnostic is treated as a passing visual test. Broader rendering, long sessions, and live multiplayer remain test gaps.

When compiling this checkout outside a folder named `Soulmates`, use `-p:BuildMod=false -p:TargetFramework=net8.0` for the MSBuild-only check, then package with tModLoader's `-build` pointing at the canonical `ModSources/Soulmates` directory. Do not install the incorrectly named `ich.tmod` produced by a default MSBuild packaging step in this checkout.

For 0.15.0, 9,950 engine assertions and 2,335 static/math assertions pass. Coverage adds resident affinity and memory transitions, bounded world-scoped history, cloned/save/network state, all native-emote response keys, actual NPC emote observation with one reply per visit, replaced residents and explicit-job cancellation, actual native insect catches in single-player and server authority modes, full cargo and missing nets, inactive/statue/released insects, client-side rejection, and flock removal after returning real items. A disconnected socket fixture permits server packet construction without opening a network listener. This is not a live multiplayer test. No 0.15.0 graphical client run is claimed; see [0.15.0 scope](../releases/0.15.0.md).
