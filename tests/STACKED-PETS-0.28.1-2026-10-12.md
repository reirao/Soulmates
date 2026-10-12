# Soulmates 0.28.1 - A Little Flock: candidate checks

Date: October 12, 2026. Local beta candidate only, prepared after completing the 0.28.0 GitHub, Workshop text/changelog and forum publication. No 0.28.1 release or public description update was performed. After these checks, the exact candidate was [installed locally at 16:37 CEST](CLIENT-INSTALL-0.28.1-2026-10-12.md).

## Exact Package

- Native tModLoader: 2026.8.3.0; package name/version: Soulmates / 0.28.1.
- Candidate: `outputs/stacked-pets-0.28.1/Soulmates.tmod`.
- Size: **1,730,547 bytes**.
- SHA256: `3062669A70C2067D87E975B3A259DCE731E4F20B0E21D1C8127497D578E1020B`.
- Regression and audit copies have this exact hash. Production compilation: **0 errors, 0 warnings**.
- During initial verification the normal client remained on the 0.28.0 Workshop-managed package, SHA256 `658CBEAA06015743D5DFEE65991A01DC9470B432D0C8BACC5D1CB569BD7FB145`. The later guarded installation replaced it with the exact 0.28.1 candidate above and preserved the previous package in a verified backup.

## Results

- Native regression: **167,156 assertions, 0 failures**. The probe has its existing 33 nullable/Hjson reference warnings, distinct from the warning-free production build.
- Independent native audit: **519 PASS expectations, 0 failed safety expectations**, warning-free compilation.
- Static/catalog regression: **62,631 assertions**, **1,070 keys**, **nine languages**, **617 source references**. Localization placeholders, wrapping, UI transforms and initialization pass.
- `description.txt`: 7,798 UTF-8 bytes; unchanged published English/German Workshop files: 7,851 / 7,872 bytes. Each also fits the 8,000-byte limit with the existing 60-byte publisher footer.
- `git diff --check` passes.

Generated logs, native summaries and the runtime trace are retained beside the candidate in the ignored output directory. Intentional malformed-packet and denied-write fixtures produce expected rejection messages; those are not test failures.

## Covered Behavior

Repeated real Eucalyptus Sap gifts compact into one companion-only stack. Mixed pet types create one harmless familiar per owned unit, with unique companion/item/copy bindings. All accepted pet items share twelve stack slots and a 64-unit bag limit. Excess remains with its source. Modded items retain their native stack limit, hooks and custom metadata; unstackable marked test pets remain separate.

Save, clone, binary transport and repeated normalization retain counts and selection. Legacy duplicate slots compact without deleting cargo. Older over-cap cargo remains recoverable while activation is bounded. Gifts, source tokens, receipt replay and rejected server transfers preserve ownership. A rejected stacked gift rolls its extra familiar back with the profile on maintenance.

Single-item return removes one familiar and returns one item. Full owner inventory leaves the stack intact. Unload returns native-sized player stacks, including items with max stack one. Lead changes retain the group, withdrawal of the last leading type chooses another owned type, and explicit dismissal remains respected by later gifts. Recall, owner loss, identity reuse and boss cleanup remain covered by the combined suites. Player-native pets and unrelated projectiles survive.

Formation tests cover counts 2 through 64, finite positions within the companion leash, and more than 72 pixels between target centers. Native runtime fixtures in both single-player and server mode run **64 familiars through 600 real NPC/projectile update ticks** after owner movement, checking stable identities, lifetime, harmlessness, follow distance and separated settled frames. A full native projectile pool does not replace another projectile, and a waiting familiar returns when a real slot becomes available.

The existing pet-management controls, stack quantity displays, following/owned status and localized labels are exercised through production UI initialization and handlers. This does not certify rendered text appearance or physical mouse input.

## Player Checklist

1. After a guarded installation, confirm **0.28.1** under Mods. Keep the active Sigil in your inventory.
2. Open **her wheel > Pets** or **Details > Pets**. Give three real Eucalyptus Saps: one owned stack should show 3 and three Sugar Glider familiars should appear separately.
3. Add another pet type. Left-click either owned stack to call the group with that type first. Your own Terraria pet should remain independent.
4. Right-click the Sap stack once: receive one real Sap, retain two, and see one fewer familiar. Dismiss all, give another item, and confirm it stays dismissed until called again.
5. Save and reload, switch companions, recall and summon. Counts must persist; old familiars must not remain behind. Test returning items with a full player inventory.
6. Watch formation movement, sprite direction, crowded scenes and frame rate. Report version, pet types/counts, action and result. Connected multiplayer requires matching 0.28.1 peers and its own acceptance run.

## Open Acceptance

No fresh rendered play session, screenshot series, physical input test, two connected clients, long-session performance or universal mod compatibility is claimed. The 64-pet fixture proves bounded native update behavior, not that every machine or custom pet renderer performs acceptably. Native artwork uses companion movement rather than original player-pet AI. Retinue remains a separate distinct-staff-type system; this patch does not multiply summon-weapon helpers.
