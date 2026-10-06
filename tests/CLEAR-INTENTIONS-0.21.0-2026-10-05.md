# Soulmates 0.21.0: Clear Intentions

October 5, 2026. Local beta candidate, **not installed, committed or published**. No normal-client package, enabled-mod list, player/world save or Workshop entry was changed. The preceding local 0.20.1 semantic categories are included, not released separately.

## Scope

- Work opens Config/Last; Config unfolds the existing work actions. Accepted explicit intent is saved per Sigil. Rejected actions, Look and settings cannot replace it. Area mining restores its recorded approach/direction/end; pointed work asks for a fresh target, never saved world coordinates or item slots. Consent stays independent.
- Tunnel compass selects Up/Right/Down/Left/Auto and Short/Until opening. Cardinal routes use three-tile-high passages or two-tile-wide shafts, bounded to eight/24 steps and the work radius. They stop at liquids, protected/decorated cells or unstable materials. A passage requires two open horizontal sections or three open vertical sections. The full cross-section is rechecked while selecting and immediately before mining.
- Explicit native Company/Collect no longer depend on automatic behavior being enabled. Manual consent does not become Always. Existing invited critters retain their binding with autonomy off, stop guided movement while paused and may hop at small ground obstacles. Native physics/life/despawning remain; there is no teleporting or general pathfinding. Mode labels expose manual, permission, paused/busy and follower states. Pet Collect is renamed Catch Critters.
- Critters > Companion pet selects a real Zephyr Fish/Nectar item in Equipment. Exactly one harmless item-backed familiar uses Terraria's sprite frames and custom companion-following movement. The item stays in cargo; no player pet/buff slot is modified. Dismiss clears selection; last-item removal clears selection and stops the pet. Recall/death/switch cleanup keeps real cargo intact. Only Zephyr Fish/Baby Hornet are supported, not ground, light or modded pets.
- Appearance, real Company animals, item-backed familiar and AETHER's cosmetic carried-insect flock are separate. No mini-mods, combat swarms or new minigames.

## Persistence and authority

Old Sigil tags default to Auto/Until opening, no Last and no selected pet. Empty/incomplete Last records do not invent work. Work and pet fields append deliberately to the earlier binary profile prefix. Existing enum/message ordinals remain unchanged; **all peers must use 0.21.0**. This is not mixed-version network compatibility.

Mining configuration and pet selection use identity-bound authority packets. Pet requests carry the native item type and existing storage token; reordered/changed cargo cannot select another item. Invalid enums, missing bytes, stale identities, invalid slots and independent client mutations are rejected. Familiar extra-AI includes the companion GUID, not just a reusable NPC index. A joining client gets a bounded grace period for delayed NPC/profile synchronization.

## Verification

| Check | Result |
| --- | --- |
| Production MSBuild | Exit 0; zero warnings/errors |
| Native production compiler/package | Exit 0; zero warnings/errors |
| General native engine probe | 87,916 assertions; zero failures; exit 0 |
| Separate native safety probe | 166 passing expectations; zero defects/harness errors; exit 0 |
| Static/controller/localization | 53,451 assertions; 906 keys in each of nine catalogs; 463 source references; exit 0 |
| Embedded / Workshop EN / Workshop DE | 7,330 / 7,247 / 7,697 UTF-8 bytes, below 8,000 before an external uploader wrapper |
| Whitespace | `git diff --check` passes |

Both authority modes use real production NPCs, Terraria tiles/items, native catch hooks and native projectile creation in isolated fixtures. Tests round-trip all 240 work recipe combinations and verify traversal bounds, openings vs pockets, protected terrain/liquid changes, rejected repeat rollback and fresh target preservation. Native wheel mouse edges cover Config, Last, compass/end controls, filters and pet selection/dismissal in nine cultures at three requested scales. The existing all-emote/bond/consent/cargo tests also remain active.

Pet checks cover native frame progression, real item conservation, clone/save/network persistence, no duplicates across repeated updates, missing/unsupported item rejection, selection changes, item withdrawal, pause, owner death, NPC-slot identity replacement and recall. The separate probe adds stale reordered-pack requests, synchronization delay/expiry, pre-sync binding and the survival of a distinct player-native pet. These are not connected-client or rendered-animation checks.

## Critical recheck

The first new pet test exposed a **production SinglePlayer spawn defect**: the NPC-owned neutral projectile lost its AI binding and subsequent updates could create duplicates. The repair binds during OnSpawn, explicitly sets both AI fields and uses player ownership only for single-player native creation, server ownership for multiplayer. The expanded suite then passed. No production behavior was disabled to hide failures.

The subsequent source pass added bounded client profile grace, null-cargo-safe pet normalization, incomplete-work-record rejection, a read-only nonallocating supported-pet list and consistent submenu animation origins. The complete suites were rerun. Earlier compile errors affected new test code; an intermediate read-only-list type inference error in the new registry was also corrected before the final passing builds.

The general test-only probe retains 33 existing nullable/Hjson compatibility warnings. Production and the separate safety probe have zero warnings/errors. Malformed-packet, blocked-local-IO and unsupported-pet diagnostics are negative controls; optional `.tmod` registry association is denied by the sandbox without blocking successful isolated runs. Tests exit before loading a normal world or opening a network listener.

## Exact artifact and limits

Final artifact: `outputs/clear-intentions-0.21.0/Soulmates.tmod`, TMOD internal name **Soulmates**, version **0.21.0**, loader **2026.8.3.0**. Its byte size, SHA256 and hashes of all 107 selected production/source/asset inputs are in `outputs/clear-intentions-0.21.0/source-inputs.json`, alongside both raw probe reports. Canonical staging is `work/workflow-source/Soulmates`; test probes are excluded from production.

No rendered client play, screenshots, long session, full progression or connected multiplayer was performed. The reported missed live critter-death reaction, graphical wheel transitions and pet animation/facing/darkness still need the [focused checklist](PLAYTEST-CLEAR-INTENTIONS-0.21.0-DE.md). Translation completeness is not native-speaker certification. The wider [beta gates](../BETA.md) remain open.

Implementation reference: tModLoader's [ModProjectile API](https://docs.tmodloader.net/docs/stable/class_mod_projectile.html) and [Projectile API](https://docs.tmodloader.net/docs/stable/class_projectile.html). Native frames are verified against the actual loaded runtime; custom following is explicitly not advertised as vanilla player-pet AI.
