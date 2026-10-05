# Soulmates 0.19.8: Repair Goal and Repeated Critical Checks

## Goal and Status

Repair the five reproduced correctness findings and the source-confirmed resident-identity issue in the [fresh 0.19.7 review](FULL-REVIEW-0.19.7-2026-10-04.md), preserve previous repairs/features, add valid-operation controls and critically recheck the changed paths.

**Local candidate only.** No normal-client installation, real character/world save, publication, Git commit or push occurred. Existing uncommitted work was preserved. The previously recorded installed/public versions were not rechecked. New requests for graphical or connected multiplayer proof are not implied by these isolated tests.

## Confirmed Repairs

| Review finding | Repair and control |
| --- | --- |
| Resident observations are overwritten by rejected inventory rollback | Queue one eligible resident emote per companion during the transfer. Do not mutate the frozen profile. Replay after either accepted or rejected settlement; revalidate owner, companion GUID, world, resident object/type/name and current eligibility. Tests cover rejection, acceptance, duplicate observations, replaced/renamed residents, inactive companion and changed world. |
| Mining eligibility disagrees with native progression | Replace the hardcoded pick-power table with cached calls to the installed native pickaxe damage rule. Observation and work eligibility use the same rule. Underground outer-world Dungeon brick rejects copper, sufficient progression allows it, the surface exemption remains, and Obsidian accepts gold. Existing protected-tile/decor checks remain. |
| Pack consumables bypass ModPlayer restrictions | Select candidates through `CombinedHooks.CanUseItem`, retaining both player and item gates. A forbidden strongest potion cannot prevent an allowed alternative. Cursed/incapacitated/dead owners and pending inventory transfers cannot use cargo consumables. |
| Healing ignores item/player potion delays | Use native potion delay before item-use/effect hooks, as native Quick Heal does. Preserve item-specific delays, player modifiers and delay vetoes; synchronize the resulting authoritative sickness duration. Counterchecks cover a 0.75 modifier, Mushroom, Restoration Potion and a ModPlayer delay veto. |
| Multiplayer rejects an unchanged empty planting target | Compare captured tile presence/type, allowing an unchanged empty Forest target while still rejecting empty Mine targets and changed terrain. Single-player, empty-space server planting, occupied-ground server planting and old replaced-ore controls pass. |
| Resident relationships use unstable modded NPC save IDs | Save/network stable content names; lookup by world/key/resident name. Resolve current runtime IDs on load, preserve missing keys without aliasing native/current content, migrate native legacy IDs, and retain unidentified legacy modded records as history-only. Registered test content, poisoned obsolete IDs, missing-content clone/transport and legacy controls pass. |

Source changes are limited to the native-rule adapter, resource use/eligibility, resident observation and relationship identity, the captured tile guard, version/build text and the removal-only trinket message. Previous inventory transport, pickup and question repairs remain enabled. Intentional public compatibility/test wrappers were not deleted simply because the production UI does not call them.

## Critical Recheck Rounds

1. Preserve the failing 0.19.7 artifact/results and add counterchecks to the independent test-only mod. Compile the repair package and both probes against the installed engine. All seven old failed expectations become passing.
2. Inspect native use and consumption ordering rather than checking only end quantities. This found an additional existing double callback: `ItemLoader.ConsumeItem` already invokes `OnConsumeItem`. Remove the extra callback and correctly permit item effects without decrementing a unit when consumption is vetoed. Test both consuming and non-consuming paths with callback counters.
3. Re-review the new deferral and selection code. Prevent a distant NPC emote from taking the bounded deferred local slot. Reject disallowed recovery candidates during selection so a permitted alternative can still be used. Add explicit controls; align potion-delay ordering with native Quick Heal.
4. Rebuild the final package, run the expanded probe, full general suite, build/static checks, and verify package/source identity. Repeat the final native runs without changing production code. Results are retained separately from the historical failing review.

Two initial diagnostics were test-fixture problems, not product failures: the legacy relationship tag accidentally boxed a native short constant instead of the old schema's Int32, and the general probe initially still expected version 0.19.7. Correct the fixture type and expected candidate version, then rerun. No gameplay assertion was removed to force a green result.

## Final Verification

| Check | Result | Scope |
| --- | --- | --- |
| Production MSBuild | Zero errors, zero warnings | No automatic mod installation. |
| Native production compiler/package | Zero errors, zero warnings | Canonical isolated Soulmates source, 0.19.8. |
| Static/localization/math | **47,921 assertions pass; 845 keys, 442 references, nine catalogs** | One reference fewer because the unreachable trinket-message branch was removed. |
| Expanded independent native probe | **136 expectations pass, zero defects, no harness errors** | Previous 96 checks, repaired 0.19.7 boundaries and new native/migration/event-order controls. |
| Full general native suite | **39,738 assertions, zero failures** | Same final exact package, including disconnected transport replay. |
| Creator/mailbox geometry | Ten expectations pass inside the focused probe | Native UI scale and measurement-only font, not rendered screenshots. |
| Embedded description | **7,747 UTF-8 bytes** | Below the 8,000-byte limit; title matches the local candidate. No Steam changes. |
| Final package identity | Audit, general suite and exported candidate match | SHA256 checked after final runs. |
| Git whitespace | Pass with CRLF accepted | Unrelated dirty work retained. |

Exact candidate: [Soulmates-0.19.8.tmod](../outputs/native-repairs-0.19.8/Soulmates-0.19.8.tmod), **1,513,010 bytes**, SHA256 `292314015FD5B12E82A6FD522AA28FFF6FC88F649FC10DAF24C6498B8F3263B7`.

Evidence: [expanded audit](../outputs/native-repairs-0.19.8/Soulmates-audit-checks.txt), [general engine suite](../outputs/native-repairs-0.19.8/Soulmates-engine-checks.txt), [build](../outputs/native-repairs-0.19.8/build-check.txt), [static checks](../outputs/native-repairs-0.19.8/static-checks.txt) and the saved round-two/round-three results in that output directory. Reproduction source: [SecondReviewCases.cs](SoulmatesAuditProbe/SecondReviewCases.cs) and [NativeRepairCases.cs](SoulmatesAuditProbe/NativeRepairCases.cs).

The production compiler and independent probe have zero warnings. The older general probe still emits 31 test-only nullable/Hjson reference warnings. Controlled malformed packets and the deliberately blocked feedback append emit expected diagnostics. Optional .tmod registry association is denied by the environment; it does not prevent build/load/tests. These are not unhandled normal-game crashes.

## Remaining Limits

No newly confirmed failing boundary remains from this six-finding review and its added counterchecks. This is a bounded repair verdict, not a claim that every feature/path is bug-free or the build is graphically certified for release.

- No new graphical playthrough, screenshots, complete progression, long-session balance or connected host/client latency/disconnect/reconnect test occurred. The two probes exit before world loading and never open a listener. Private transitions are sometimes reached by reflection.
- The adapter binds two private installed native methods once to cached delegates. No reflective lookup happens in the tile-search hot path. Frame-important targets are excluded because native damage checks can redirect their hit buffers. A future engine signature change requires compatibility verification; the adapter does not silently fall back to another approximate threshold table.
- The Dungeon control demonstrates corrected eligibility and native-rule parity, not a fresh world-generation/progression playthrough. The item gate/veto and missing-content tests are deliberately controlled native fixtures, not compatibility certification for every third-party mod.
- Network relationship representation changed; all multiplayer peers must use 0.19.8. Old saved vanilla relationships remain usable. Old modded records lacking stable names cannot safely identify a remapped NPC; their history remains readable and future meetings create correctly identified records.
- Recorded open features remain open: full Bestiary pet AI, companion-owned vanity-pet summoning, arbitrary learned task programs, standalone player wheel without a companion, wall deconstruction and trap warnings. No new feature roadmap or balance decision was introduced.
- Native item-use restrictions are honored, but this does not turn cargo into an arbitrary native weapon/accessory/item execution system. Translation quality still needs native speakers; some multiplayer replies remain server-language formatted.

No private notes, real player/world names, positions or saves are included in exported evidence. Historical failing and repair reports remain distinct; changelog.txt contains only this candidate's update text, not accumulated old logs.
