# Little Company: 0.26.0 verification

Date: October 11, 2026. Local beta candidate only. No installation, Workshop upload, GitHub publication or graphical playtest is claimed by this pass.

## Implemented boundaries

Personal undertakings reuse existing idle movement, classified world-scoped homes, actual equipped familiars and observed building supplies. They require a bounded observation and return before completion is remembered. Interruptions are neutral. Existing reflection answers adjust bounded per-kind preferences without granting work permissions; negative feedback delays and reduces preference rather than permanently disabling an activity. The last completed undertaking is available in existing story conversations. Profile clone, save, transport and NPC display/reflection transport are explicitly connected.

Retinue is a separate real-item inventory embedded in Pets and ordinary cargo. Favorite protection, native item metadata, stale-token rejection and acknowledged transfers remain in use. AETHER selects every distinct owned staff type; normal companions select 1/2/3 at levels 1/10/20. Duplicate staves do not multiply helpers. First explicit gift selects only into empty storage; later gifts respect dismissal. Legacy cargo migration does not enable summoning.

Helpers use native artwork with bounded companion-owned combat, not original player-minion AI. The authoritative engine process is their technical projectile owner; logical ownership is the NPC plus Sigil identity. Player pet/minion/sentry behavior and buffs are not replaced. No fake player, global player swap, online service or generated item is used. Attacks recheck the companion's nearby defense target and exclude critters/friendlies. Inventory transactions suspend combat. Dismissal, final withdrawal, recall, switching, owner loss and missing bindings retire effects.

## Automated evidence

- Production MSBuild: zero warnings and zero errors.
- Native tModLoader package compilation: zero warnings and zero errors.
- Full native regression: 100,745 assertions, zero failures on the final candidate package.
- Independent native safety audit: 490 passed expectations, zero failures.
- Static/localization/layout checks: 60,002 assertions; 1,023 keys in all nine catalogs; zero failures.
- Native staff discovery covers 38 vanilla minion/sentry items, including Abigail, Flinx, Terraprisma and sentries. Resources, pet items, magic staves and whips are excluded.
- Exact package verification: 119 entries and 91 production source files; all packaged sources match the workspace. No work, tests, private feedback or probe content is included.

Candidate: `outputs/little-company-0.26.0/Soulmates.tmod`, 1,683,207 bytes.

SHA-256: `9902FFBA47C4B44F0E6BA361FA7EB1BE77E6097CC4FA94EE37C7EDD296123057`.

Local machine-readable evidence is in the same output directory. Workshop draft descriptions remain below Steam's 8,000-byte limit. The regression probe retains pre-existing nullable/Hjson compatibility compiler warnings; production compilation has none. Expected malformed-data exceptions and sandbox-denied file-association registry writes are not assertion failures.

## Repairs discovered during verification

- Native single-player projectile spawning requires the local authority identity; using the server sentinel there discarded the helpers. Both helpers and ranged attacks now use the engine authority identity while retaining separate logical binding.
- Native spawning can send SyncProjectile before NewProjectile returns. Bolts now bind their Sigil identity in OnSpawn, before that first packet. The regression captures and decodes the initial native packet into fresh client projectiles without a corrective update, proving the first GUID/NPC/staff/target binding is already present.
- Inventory header initialization placed a pet-only tab over its page button before the first UI update. Initial positions now follow the actual tab count. Clicking the retinue header also retains Summons storage instead of switching back to Pets.
- Old corruption fixtures assumed pet selection was the final profile field. The fixture now identifies the actual selection before the appended tail, and the independent historical wire-prefix check explicitly includes 0.26 extensions.
- Pending cargo transactions cancel personal movement without erasing an already captured reflection, its answer token or its feedback target.
- Headless real-projectile combat uses the existing saturated cosmetic-text fixture so missing graphical fonts do not prevent native damage testing. This is not evidence for rendered damage text.

## Playtest still needed

1. In Pets, use the staff symbol. Gift two different unfavorited summon staves to AETHER; confirm both helpers become visible without displacing your own pet/minions.
2. Dismiss company, give another staff, and confirm dismissal is preserved. Select an owned staff, return items, recall and switch Sigils. Save/reload and compare real item ownership.
3. Observe melee, ranged and sentry artwork near an enemy and harmless critters. Special vanilla minion AI, segmented art and arbitrary modded rendering are not copied or certified.
4. With safe idle time and autonomy on, observe a nearby classified home or her real familiar. Interrupt with work, danger, Stay or Pause; confirm incomplete undertakings are not described as successes.
5. Let one undertaking finish and return. Answer its existing evaluation, then use Do you remember? and Diagnostics to inspect memory/preference. Later must remain neutral.
6. Repeat with two connected clients, different Sigils, inventory conflicts and reconnects. Headless authority fixtures are not connected multiplayer acceptance. All peers require 0.26.0.

Physical V, rendered layouts, pacing, progression balance and endurance remain open. No new screenshots were fabricated or recorded.

## Subsequent Client Installation

Following a separate explicit user request, the exact checked package was installed into the normal client at 22:29:25 CEST. The [installation receipt](CLIENT-INSTALL-0.26.0-2026-10-11.md) records the package identity, verified backup and unchanged saves. This follow-up does not change the test scope above or claim a client launch, publication or graphical acceptance.
