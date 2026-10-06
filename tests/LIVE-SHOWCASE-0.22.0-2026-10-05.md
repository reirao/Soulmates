# Soulmates 0.22.0: Live Showcase Session

Status: live feature pass and screenshot collection completed; final clean save pending. Coverage is partial, with concrete findings below. This is not a full graphical acceptance test or a release approval.

## Actual Session

- Used the normal Steam tModLoader client through its native Windows UI, without item-spawn cheats or QA probe mods.
- Visually verified Soulmates 0.22.0, tModLoader 2026.8.3.0 and Terraria 1.4.4.9.
- Created new Classic test characters `s22` and `q`. No pre-existing character was selected, replaced or deleted.
- Generated two new Small Classic Crimson worlds: `Canyon of Heart`, seed `2000863907`, and `The Reef of Guitars`, seed `475866392`.
- Created AETHER in the first world and reused the same test character and companion in the second world after a real Save and Exit.
- Tested in English. This session does not verify the other eight localizations.
- Saved 20 unaltered JPEG captures in `outputs/showcase-0.22.0-20261005`, with timestamps, dimensions and observations in `captures.json`. Initially misleading `.png` names were corrected to `.jpg` after signature validation; the original bytes were not re-encoded.
- Prepared a local English feature gallery in that directory's `index.html`. Nothing was uploaded or published.

## Observed Successes

| Area | Actual observation | Evidence or limitation |
| --- | --- | --- |
| Starter kit | One Soulcore and three Blank Sigils were present. | Soulcore was initially overlooked because of its narrow icon; native hover resolved its identity. Screens 01-02. |
| Creator | Inventory right-click opened the reusable creator. Name cycling Rune -> Mira -> AETHER did not randomize the other choices. | Screen 03. Not every appearance/talent combination was tested. |
| Creation and summon | Bind Soul delivered a Bound Sigil in slot 5, consumed one Blank Sigil, and summoned AETHER. Healing was observed. | Genuine inventory transaction and world companion, not a fixture. |
| Permission | Forestry, Gathering and Mining initiatives accepted Always do this. | Screen 04; Always policies remained present after save and world change. |
| Forestry | Native tree-shake leaves and real Wood drops appeared; successful ShakeTree and PruneBranch actions were recorded. | Screen 05 and local session events. All tree types were not tested. |
| Gathering | Small real resource stacks accumulated. Equipment view showed Gel 2, Wood 11 and Acorns 5. | Screen 11 and pickup events. This does not establish complete item conservation or exclude all duplication bugs. |
| Player wheel | Self right-click opened the player root. A further right-click switched to the companion root. | Screens 06 and 09. Hitting a moving NPC directly was less reliable. |
| Emotes | Feelings -> Joy & affection expanded; Heart produced a native player bubble and AETHER's heart response plus contextual text. | Screens 07-08. Other categories were inspected selectively, not exhaustively. |
| Character window | Care, Equipment and Diagnostics opened through Details and their view controls. | Screens 10-12. Capability slots are not proof of arbitrary equipment drag-and-drop. |
| Work control | Pause changed the live task lane to Paused; Resume restored AutomaticWork. | Actual UI state and response. Abort with a resumable explicit job was not completed. |
| Diagnostics | The export button wrote a real local diagnostic file. | File existence and relevant contents checked; raw private field notes are not copied into the showcase. |
| Wallet gift | AETHER independently offered 2 silver and 99 copper. Accepting the response completed the gift. | Screens 14-15; `wallet_gift` records 299 copper transferred and a zero remaining balance at that event. |
| Games | The dedicated Games branch opened; Rock versus Rock correctly resolved as Draw. | Screens 16-17 and `rps_round`. Win/loss cases were not exercised here. |
| Work UI | Config and Last were visible. Last reported no accepted explicit work order. Mining Mode opened the directional tunnel compass. | Screens 18-19. No completed tunnel is claimed. |
| Defense and healing | Soul Sparks attacked actual enemies; restored-life notifications appeared. | Screen 20 and attack events. The test character still died at night; no combat-balance approval is implied. |
| NPC awareness | AETHER noticed the Guide; native NPC/companion emotes appeared. | Screen 05 and later live observations. This is not exhaustive relationship/dialogue coverage. |
| Persistence | AETHER's identity, level 3 and Always policies survived a clean save, reload and world change. | Screens 09-12 from the second world. Full cargo/pet/Last equality was not measured. |

## Findings

1. **Resource withdrawal needs priority reproduction.** While work was paused, right-clicking Wood 11 in its visible resource slot and then left-clicking the same slot produced no visible transfer after fresh observations. Other character-window controls worked. Do not mark direct cargo withdrawal as passed. The root cause is not established by this session; inspect slot hit testing, pending-response gating and the actual transaction next.
2. **Question lifetime is inconsistent with its answer wheel.** Forestry and wallet response wheels remained open after their question text faded. Keep the prompt available until it is answered or deliberately dismissed. Screens 04 and 14-15 provide the relevant sequence.
3. **Soulcore icon is misleadingly thin.** It appears as a narrow vertical strip, although native hover identifies the item and right-click opens the creator. `Content/Items/Soulcore.cs` references the vanilla Fallen Star texture without an item-animation registration; this is a plausible cause, not a verified fix. Screen 02.
4. **Speech can overlap the minimap in the compact viewport.** The temporary 1152 x 864 client size exposed collisions. This needs viewport-aware placement rather than a claim of finished responsive layout.
5. **Critter behavior remains unverified.** Company mode selection succeeded and natural rabbits, squirrels and birds were present. No completed following, capture or death response was witnessed. Moving-target clicks often landed on the material behind an animal; target hit testing and visit gates require a dedicated follow-up. The inspected second-world session did not record completed critter-company or capture events.
6. **Critter rejection diagnostics need precision.** The exported report used a generic `not a natural catchable critter` reason. A useful follow-up should expose which native eligibility condition failed and which task/consent/range gate blocked a visit, without changing normal critter AI just to make a fixture pass.
7. **Diagnostic version label is wrong.** The export header says `loader 1.4.4.9`, which is the Terraria version. The observed tModLoader version was 2026.8.3.0.
8. **Exhausted trees are reconsidered.** Failed tree-shake attempts recurred after a roughly 60-second cooldown; the `forestry_shake_rest` event records a cooldown of 3600 ticks. No fabricated drops are established by this observation, but remembering exhausted targets could make activity less repetitive.

No gameplay fixes or source-version changes were made during this showcase run. These findings are recorded for a separate repair pass; they are not silently described as resolved.

Subsequent work is recorded separately in [the local 0.22.1 repair report](LIVE-SHOWCASE-REPAIRS-0.22.1-2026-10-05.md). Native counterchecks address cargo initialization, fading, icon registration, placement and diagnostics; they do not retrospectively turn this session's failures or unverified features into live passes.

## Input and Capture Limits

- Native menu clicks and individual character-name key presses worked. Automated short Escape and movement presses were unreliable in the world; this alone is not evidence of a Soulmates input defect.
- Inventory was temporarily rebound to Mouse3 through the native Controls UI, then restored to Escape and visually verified.
- Video was temporarily set to a compact viewport for readable feature captures. The original large maximized window was restored at the end.
- A picture-in-picture overlay was removed before the feature series. Saved images are full native captures, not cropped, retouched or generated replacements.
- One early menu-navigation mistake exited the client; its log showed a regular shutdown, not an exception. The client was relaunched normally through Steam. Fresh observation after a page change is essential because immediate captures can still show the prior menu.
- Clean saves were already performed during the run, including the reload/world-change check. The final second-world session is still running pending the user's manual inventory-open step, after which Save and Exit can be clicked.
- The in-app browser rejected the gallery's local `file:` URL. No alternate browser route or server workaround was used. Local references, decoded image data and gallery structure can be checked without claiming a rendered browser preview.

## Still Unverified

- Direct cargo withdrawal and complete deposit/withdraw/pickup quantity conservation.
- Completed explicit mining/tunnel jobs, ore/block filters, protected decorations and all biome tree types.
- Critter Company, actual built-in-net Collect, natural critter death responses, consent variants and Off/Pause cleanup.
- Own-pet summoning and lifecycle. The fresh Classic character did not possess a real Zephyr Fish or Nectar item; an empty pet slot is not a summoning test.
- Connected multiplayer, authority boundaries, reconnects and simultaneous transfers.
- Long-session progression, energy/combat balance, all nine languages and every wheel action.
- Full save/reload equality for cargo, Last job and equipped pet selection.

## Next Repair and Retest Order

1. Reproduce direct resource-slot withdrawal with transaction diagnostics and a known item count.
2. Keep pending-question text anchored and readable until resolution; check compact and wide viewports.
3. Correct the Soulcore animation/icon and diagnostic version label.
4. Run a dedicated natural-critter test with explicit eligibility and task-gate diagnostics, then test real pet items.
5. Complete the focused acceptance checklist and multiplayer conservation checks before declaring the build fully tested.

The focused checklist remains `tests/PLAYTEST-CRITTER-CHARACTER-0.22.0-DE.md`. Previously completed engine checks are separate evidence and do not complete this live session. The showcase is evidence of observed features, not proof that every requested feature is finished.
