# Soulmates 0.19.4: Little Replies (Local Candidate)

## Scope

The player reported missed critter deaths, unsettled switching, unanswered questions, flickering text and easy-to-miss native emotes. This pass targets those existing systems. It does not introduce companion-owned vanity pets, an online AI service or unrelated world automation.

Private feedback and runtime logs were inspected locally. No raw notes, names, world files or journals are included here or uploaded.

## Changes

- Separate pending critter grief from the 15-second reaction cooldown. A genuine witnessed death can wait up to two minutes behind an existing question or speech. Full-hitbox visibility includes a close owner's witnessed loss. Catches are not deaths; existing statue and harmless-creature safeguards remain.
- Offer an occasional critter-care question after a real observed loss. Four native-symbol answers select gentle Watch, Company, practical Watch or Later. Accepted choices save critter mode and voice, with cooldown-limited mood/bond changes. They grant neither catching/mining permission nor XP.
- Accepted personal and initiative answers create native player reply bubbles without recursively applying general emote rewards. Matching native emotes can answer pending questions. Remove the client echo of received native player bubbles: Terraria already observes the original native message on server authority.
- Preserve personal questions during brief defense, hide their answer interface until quiet, reject combat-time answers and keep the original deadline. Explicit work, Stay, pause, disabled autonomy, changed binding and recall cancel pending choices. Ambient lines do not overwrite questions.
- Synchronize idle state, duration and facing. Clients no longer choose separate random destinations or replace synchronized facing with local velocity. Hold the action animation briefly across movement gaps and wait calmly near the owner for answers.
- Repeated active speech does not reset its timer, fade, side or anchor. Clamp speech above the companion rather than jumping below near the top edge. Preserve the existing trailing fade and companion attribution.
- Show bounded sequences of native emotes: task > question mark, coins > heart > question mark, or creature > sadness/anger. Each symbol lasts three seconds. Incidental symbols cannot interrupt a running sequence; answers, direct interaction, commands and recall can. Retire replaced own bubbles using Terraria's native removal message, without touching other speakers' bubbles.
- Translate nine new question keys in all nine catalogs, now 835 keys each. Keep translation authoring data synchronized. The packaged description names 0.19.4 and is 7,117 UTF-8 bytes. The upload changelog contains only this candidate, not the full history.

## Verification

On October 3, 2026:

- MSBuild main mod and probe: zero compiler errors or warnings.
- Native tModLoader compiler and package: zero compiler errors or warnings.
- Final packaged candidate: **38,296 native-engine assertions, zero failures**.
- Static catalogs, source references, wrapping and UI transforms: **47,350 assertions, passed**; 835 keys in nine languages and 428 source references.
- Whitespace/diff check: passed.

Most assertions are existing broad checks, not separate playthroughs. New fixtures use actual NPC death hooks, native player/NPC bubbles and production response methods in controlled single-player and server modes. They cover delayed grief, answer persistence, reaction cooldowns, no extra reward or permission, temporary combat, sequence order/duration/interruption, bubble retirement transport, speech stability/viewport bounds, valid/invalid movement serialization, client echo rejection and recall cleanup.

An early native run exposed a null-NPC assumption in the added facing deserializer; it was removed. A later fixture incorrectly kept the dedicated-server UI flag during its single-player prompt check; the fixture now supplies a valid client UI state only for that check. Final packaged tests pass after both corrections. Expected malformed-packet warnings and denied optional registry association are not failed assertions.

**No graphical client playthrough, screenshots, long session or connected multiplayer session was performed for this candidate.** Pixel-level readability, screen-edge placement, visible native emote sequencing and real mixed-mod behavior remain player checks. Native fixtures do not replace them.

## Artifact and Status

The tested binary is saved locally as `outputs/little-replies-0.19.4/Soulmates-0.19.4.tmod`:

- Size: **1,489,296 bytes**.
- SHA256: **0F7810D99295AC67B4180F2BAD68C96DC70A9849041A223DC5FB47252EBB3062**.

This candidate was initially prepared without changing the normal client, then **installed locally at the player's request on October 3 at 14:09 CEST** after tModLoader was closed. It is **not committed, pushed or published**. The public baseline remains 0.19.3. Existing saved Sigils retain their format; multiplayer peers require the same version because live ExtraAI data changed. The regression probe remains only in the isolated test runtime. No player/world saves were changed.

The normal `Mods/Soulmates.tmod` now has the exact tested hash and size above. Its prior file was backed up as `Soulmates-before-install-0.19.4-20261003-140920.tmod` under the output directory, with verified SHA256 `1042AA4D6C72A82399EABC286AC284632DD753FB4800262329FE852F7CA73509`. No version is inferred from that old hash. Enabled mods remain only Soulmates; no Workshop files, characters, worlds or other mods were changed. The local installation receipt is `outputs/little-replies-0.19.4/installation-receipt.json`. Installation verification is not a graphical gameplay test.

Pet items may be stored in normal cargo, but companion-owned vanity-pet summoning is not implemented. No compatibility promise is made for all vanilla or modded pet AIs.

The separately requested original English lyric is in `outputs/Soulmates-song-en.txt`, exactly 5,000 characters including title and LF newlines. This is song text, not generated audio.

## Player Checks

1. Confirm 0.19.4 before testing. Inspect speech near the top of the screen and as the companion crosses or leaves the player; repeated lines should not blink or jump below.
2. Watch permission and wallet offers. Native symbols should appear one at a time; answer with the response wheel or its matching emote. Direct work and recall must stop the old sequence.
3. Witness a nearby natural critter death while idle and while a personal question is pending. Delayed grief should remain available; a care question may follow in a quiet moment. A net catch must not trigger death grief.
4. Check brief combat, Stay, pause/resume, work, recall and swapping Sigils. No old answer wheel, expression, company assignment or event queue should follow the wrong companion.
5. Check connected multiplayer with both peers on 0.19.4. Report the action, observed result and solo/host/client context; the native fixtures do not verify real network latency.
