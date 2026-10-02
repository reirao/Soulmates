# Soulmates 0.19.2: Item Emote Groups and Critter Attention

## Report and Diagnosis

The player's screenshot showed a flat item-symbol list under Player Emotes > Items. The categorized cargo conversation under Companion > Items is a separate interface and did not satisfy this request. The loaded 0.19.1 was not simply an outdated installation.

Company was enabled with autonomy, but busy work could repeatedly win attention. Passive observations only ran in Watch mode, and an already active speech line could discard a greeting. These are distinct from catching, which still requires a real carried bug net and valid cargo capacity.

## Changes

- Add seven item-emote groups before their individual symbols: Food, Recovery, Tools, Weapons, Materials, Valuables and Party. All 22 native item symbols and all 151 vanilla emotes remain reachable exactly once. Back returns through the group layer without adding another ring. Terraria's native item-emote catalog has no separate ore symbols; ore conversations remain under Companion > Items > Ores.
- Translate the seven labels in all nine languages; each catalog now has 817 keys.
- Observe nearby natural critters in every non-Off mode, including during work. Queue greeting speech behind current speech and discard stale, distant or disabled observations.
- Give a bounded critter visit a turn before choosing another automatic task when no current work is active, followed by a five-second visit interval. Explicit jobs, ongoing automatic work, combat, Stay, pending questions, energy and native catching protections retain priority.
- Always supply the localization argument for single-argument speech, including an empty argument. The original blank-name formatting failure occurred in a test fixture, not a confirmed player session.

## Verification

- Main mod and native regression probe compile with zero warnings and zero errors.
- The packaged 0.19.2 passes 37,296 native-engine assertions with zero failures. Most assertions are existing broad coverage, not independent graphical gameplay scenarios.
- New checks dispatch production wheel click events through all seven groups and Back in all nine cultures at UI scales 1, 1.5 and 2. They use a controlled measurement-font fixture, not rendered screenshots.
- Critter checks cover a busy greeting queue, Company observations during assigned mining, a visit competing with an available automatic ore task, and the post-visit interval. Existing combat, Stay, catch-rule, authority and cargo checks remain active.
- Static localization, formatting and layout checks pass 46,344 assertions.
- Final Workshop drafts are 7,796 UTF-8 bytes in English and 7,791 in German. With the standard author quote, they remain below Steam's 8,000-byte limit at 7,857 and 7,852 bytes respectively.

## Candidate and Player Check

This is a local 0.19.2 candidate, separate from the published 0.19.1. Root Workshop descriptions and changelog are prepared for a future upload; public pages remain 0.19.1. No graphical client playthrough or live multiplayer session was performed for this candidate.

1. Confirm 0.19.2 in the loader. Open your own wheel > Emotes > Items: groups should appear before individual symbols; Back should return one step.
2. Enable Company and autonomy near a natural bunny, bird or squirrel. Let existing work finish and watch for a bounded visit between automatic tasks. Watch observes without approaching; Off disables observations.
3. Keep another speech line active while approaching a critter: its greeting should wait, rather than vanish. A creature that leaves or changes type should not produce a stale greeting.
4. Check that Stay, combat, explicit work, furniture interactions and inventory actions still retain their normal priority.

## Installation

After the player confirmed tModLoader was closed, the final repackaged candidate passed the native-engine run again and was installed only into the normal client's `Mods/Soulmates.tmod`. It is **1,534,822 bytes**, SHA256 **5FEF119499BDDDFA9F62AC32D553EE9A34BDC181A7A67B99EA39A094A7661B3B**. The installed hash matches the exact tested candidate.

The actual previous package was backed up and its hash verified. The local installation receipt and package are under `outputs/item-wheel-0.19.2`; generated packages and receipts are not tracked in Git. Enabled mods remain only Soulmates, and the ModSources junction points to the working repository. No regression probe, world or character was installed or changed. At installation the candidate had not been pushed or published. Follow-up: the creator uploaded 0.19.2 to Steam on October 2 at 20:32 CEST; its changes are also included in the subsequent 0.19.3 release. These publication steps add no further gameplay test claim.
