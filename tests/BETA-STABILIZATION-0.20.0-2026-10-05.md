# Soulmates 0.20.0 beta stabilization

Date: October 5, 2026. Candidate: **0.20.0 - Shared Instincts**. Decision: local beta candidate, not completed beta acceptance. The feature scope is frozen; rendered, progression, endurance and connected multiplayer gates remain in [BETA.md](../BETA.md).

## Exact package

- tModLoader: 2026.8.3.0, native .NET 8-compatible compiler/runtime.
- Internal name/version: Soulmates / 0.20.0, verified from the TMOD header.
- Bytes: 1,561,376.
- SHA256: `8381D6D0E35D533367CD800007259044EFB82758732141CCAAC7846674225580`.
- Saved artifact: `outputs/beta-candidate-0.20.0/Soulmates.tmod`, with raw engine/audit reports and source-input hashes alongside it.
- Staging: canonical `work/native-repairs-source/Soulmates`. Production directory code/assets and selected root inputs are compared to the checkout. Test probes are excluded from the production package.

## Changes and critical recheck

1. Centralize six autonomous abilities in CompanionAbilityRegistry. Permission access, clone/save/load, existing tag keys, binary field positions, UI rule buttons, task/rule emotes and stamina now share the same definitions. Undefined kinds fail closed; invalid live permission values display/use Ask rather than leaking an undefined enum key.
2. Introduce CompanionActivityCoordinator and per-NPC activity adapters. It tries ordered lanes and stops after the first claiming module. Defense and Pause retain interrupted assignment state; native catch completion and automatic task termination own their final movement frame. Existing passive observations and permitted nearby pickup remain outside task dispatch.
3. Correct visible status for active critter visits and combat interrupting a job. Optional local snapshots include activity_lane without adding personal names, chat or exact positions.
4. Recheck duplicated initiative guards. The shared gate now prevents a late answer from bypassing a recovery hold, changed routine or Never rule. Offers, pending expiry and answers use it. Separate catch controls demonstrate rejection without creature mutation or saved Always consent, then a valid recovery/resume control.
5. Recheck quick-action API authority. Native input/network handlers already routed ordinary client actions to the server, but direct calls to some settings/toggle branches lacked the same guard. Every quick action now rejects independent client mutation; undefined and UI-only actions no longer fall through into care dialogue. A complete-profile before/after control covers all enum actions.
6. Preserve earlier repairs, Games, the basic net capability, independent Company/Collect permissions, queued greetings and tree context. No new behavioral feature group was disabled to obtain a green result.

## Verification

| Check | Final result |
| --- | --- |
| Production MSBuild, no packaging under the checkout name | Exit 0; 0 warnings, 0 errors |
| Native production compiler/package build | Exit 0; 0 warnings, 0 errors |
| Native general probe | 43,741 assertions; 0 failures; exit 0 |
| Separate native safety probe | 156 expectations; 0 defects, 0 harness errors; exit 0 |
| Static/localization/layout/controller harness | 50,511 assertions; 853 keys in 9 catalogs; 439 source references; exit 0 |
| Workshop description | 6,024 UTF-8 bytes; below the 8,000-byte limit |
| Whitespace validation | Pass |

The controller tests exercise all 512 availability combinations, ordered short-circuiting, exactly-once visits, retained work across defense/pause, reset, invalid registrations and exception diagnostics. Native fixtures exercise real NPC construction and AI dispatch in single-player and server-authority modes, proving per-instance controllers, assignment pause/resume/abort, defense, Stay, native catching, next-frame release and recall.

The hand-written, empty-cargo 0.19.9 wire fixture is independent of the new serializer and matches the complete field sequence byte-for-byte. All 729 combinations of six Ask/Always/Never policies round-trip together through clone, native TagCompound save/load and binary transport. Existing broader tests cover mixed/modded cargo, wallet, capacity, safe terrain, learning, conserved inventory acknowledgements, actual native deaths for 16 critter species, protected catches, UI bounds and all nine runtime language catalogs.

Checks were repeated after the shared gate and API guard changes. Earlier intermediate runs passed 43,006 and 43,735 engine assertions; the expanded final suite above describes this artifact. The separate safety suite expanded from the preceding 145 to 156 passing expectations. Historic reports remain unchanged.

The general test probe still has 34 test-only nullable/Hjson compatibility compiler warnings. The safety probe compiles with 0 warnings/errors. Production has 0 warnings/errors. Expected malformed-packet and deliberately failing local-IO diagnostics occur in negative controls; startup also reports denied sandbox access to the optional .tmod registry association. None is hidden as successful gameplay evidence.

## Module limits

The work removes duplicate dispatch/policy registration, not all shared NPC state. Activity-specific fields and implementations remain in SoulboundCompanion partial files. The coordinator serializes claiming task/movement branches, not every observation, passive pickup, healing or consumable side effect. New activities still require explicit validation/execution and tests; adding a binary field still requires a deliberate protocol/version decision.

## Not verified or changed

- No rendered client play session, screenshots, full progression or 60-minute endurance session was performed for this candidate. Native server fixtures and disconnected socket replay are not connected multiplayer tests.
- The user's missed live critter-death reaction is not reproduced by the passing native death fixtures; visual timing, visibility and actual circumstances remain an acceptance gate, not a closed defect.
- No normal player/world save, enabled-mod list or Workshop-managed package was changed. No Git commit/push, GitHub release or Steam publication was performed.
- The normal installed client was re-read on October 5: Soulmates 0.19.8, 1,513,010 bytes, SHA256 `292314015FD5B12E82A6FD522AA28FFF6FC88F649FC10DAF24C6498B8F3263B7`. The prior backup remains intact.

The candidate is ready for the next bounded client test. Do not describe it as a fully accepted beta until the outstanding gates pass against this exact package or a subsequently repaired candidate.
