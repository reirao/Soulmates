# Living Critters Repair: 0.22.2

Prepared locally on October 8, 2026. This record is not an installation or publication receipt.

Subsequent delivery: the exact package was [installed on October 8](CLIENT-INSTALL-0.22.2-2026-10-08.md). On October 9 the creator confirms a successful in-game playtest and Workshop upload; Steam records 08:05 CEST with review pending at the publication check. [Release notes](../releases/0.22.2.md) separate that player report from the bounded automated evidence below. The original preparation results are retained, not retroactively relabeled as graphical acceptance.

## Reproduced Cause

Native `NPC.SubAI_HandleTemporaryCatchableNPCPlayerInvulnerability` restores a critter's original `friendly` value when spawn immunity expires. Ordinary critters can then be `friendly == false` while still having the native critter flag, a real catch item, zero damage and normal health. Soulmates incorrectly excluded them from watching, company, catching and witnessed-death reactions. Earlier fixtures mostly tested freshly initialized, stationary critters and even asserted that the changing friendly flag meant hostility.

Normal NPC updates on a grass floor also exposed intermittent line-visibility rejection, an approach speed too slow for fleeing birds, and a completion threshold narrower than the existing invitation/catch reach. Cuttable foliage could block both target capture and the default right-click router. Missing pet selections silently played a click sound.

## Repair Boundaries

- Use permanent native critter classification and zero damage, not temporary friendliness. Preserve catch-item validation, boss/town, statue, released-animal and gold protections.
- Use native rectangle visibility as fallback to line visibility. A solid-wall countercheck remains negative; no manual raycast or terrain bypass was added.
- Match completion to the existing 96-pixel reach. Approach speed follows the moving animal's speed within a 4-7.5 cap. Real company uses 96/48 follow hysteresis, with bounded ground/flying speeds and unchanged native collision, health and despawning.
- Allow entities over cuttable foliage, but keep default furniture and placed-torch priority at both target eligibility and input routing.
- Give missing companion-pet selections the existing localized explanation. Own pets still require real carried Zephyr Fish or Nectar, preserve player buffs/pets, and disappear on dismissal, last-item withdrawal, identity change or recall.
- No save/wire schema change, new pet family, automatic permission grant, public upload, recording enablement or edited user save.

## Verification

Installed engine: tModLoader 2026.8.3.0 / Terraria 1.4.4.9, .NET 8.0.425. Separate workspace save directories; probes exit before world selection and never join a user's world.

| Check | Result | Scope |
| --- | --- | --- |
| Production MSBuild and native compiler | 0 warnings, 0 errors | Compilation/package, not rendered acceptance |
| Final native probe, two repeated runs | 90,691 assertions, 0 failures each | Same final package |
| Independent native audit | 176 expectations, 0 failures | Includes native immunity expiry and fake zero-damage enemy rejection |
| Static/localization checks | 54,126 assertions; 918 keys in nine cultures; 478 source references | No new untranslated keys |
| Package verification | Native payload hash valid; all 79 source hashes match; 107 entries | No test/private payload |
| Workshop text preparation | EN 7,746 / DE 7,781 UTF-8 bytes | Below 8,000; not publicly saved |

Expanded runtime cases call `NPC.UpdateNPC` and `Projectile.Update`, including native physics, rather than assigning movement manually. They cover aged Bunny/Squirrel/Bird visits and following, native Bunny/Butterfly/Firefly catches, real cargo/Sigil counts, consented automatic Company/Collect, queued aged-critter death while speaking, wall rejection, both supported pet lifetimes/animation/following and recall cleanup. Butterfly AI's actual post-spawn catch item is checked, not its initial color placeholder. Weather/daytime and terrain are controlled test conditions, not a played world or screenshot.

Raw reports and the unchanged 0.22.1 negative baseline are retained privately under `outputs/critter-pet-repairs-0.22.2`. The broad regression probe has 33 pre-existing test-only nullable/Hjson warnings; production and independent audit compile cleanly. Malformed-packet and blocked-write cases deliberately log errors. The initial narrow negative-baseline map allowed an unrecruited bird to leave the fixture boundary; the final fixture is wider. Do not count that harness diagnostic as a production defect.

Final package: 1,586,271 bytes, SHA256 `7A9312816042843D160E220C0CB3DAD12AF3D5FA534D5FFF5D3752E9A9520BB2`.

## Human Acceptance

1. Let a wild critter live nearby for at least ten seconds. Right-click it over grass: Look, Company and Catch should remain available when in range.
2. Invite a Bunny/Squirrel/Bird, walk away, then choose Off. Following should be visible; release leaves the real animal alive.
3. Catch an ordinary animal without buying a net. Inspect her cargo: exactly the real native catch item should be present. Lava animals still require a carried lava-proof upgrade.
4. With autonomy on, test Company/Collect under Ask, Always and Never. Direct invitations do not alter automatic consent. Pause/Resume/Abort retain their existing meanings.
5. Witness a nearby critter death after its spawn protection expires, including while she is speaking. Expect an expression/memory and a queued personality-aware reply, not kill XP.
6. Deposit a real Zephyr Fish or Nectar in her equipment cargo and select it. Test animation/following, dismissal and withdrawing the supporting item; your own native pet must remain unchanged. Unsupported pet families are not included.
7. Click a missing pet icon: expect a localized explanation. Right-click chests, furniture and placed torches in default Terraria mode: native priority must remain.

Rendered client interaction, real progression/endurance and connected multiplayer remain acceptance gaps. The shared desktop was not controlled and no new screenshots or visual playtest are claimed.
