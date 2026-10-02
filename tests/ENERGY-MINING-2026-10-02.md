# Soulmates 0.19.3: Steady Hands

## Reports and Diagnosis

The player reported hesitant activity, rapidly falling energy and mining beneath seedlings or decorations. They also requested persistent per-type permissions for automatic ore and block mining inside the companion orb menu.

Code inspection found that each mining or gathering action cost one energy, active work reset recovery progress, and retained work restarted with only 12 energy. This created repeated short work/rest cycles rather than sustainable activity.

Raw feedback logs, saves and identifying session data are not included in this report or uploaded.

## Changes

- Settle successful work effort in batches: eight retrieved stacks, four mined blocks or two forestry actions per energy point. Mixed actions share the same effort budget. The partial budget belongs to the live companion, not the saved Sigil.
- Reduce command entry fees: Follow is free, Gather costs one, Mine and Treasure two. Healing costs two instead of four. Existing healing amounts and cooldowns are unchanged.
- Following restores two energy every two seconds; active work restores one every three seconds. Waiting and manual pause retain faster recovery. Combat still interrupts recovery. Preserve progress between work phases rather than clearing it every frame.
- After exhaustion, autonomous work and energy-paused retained jobs rebuild a 30-energy reserve. Synchronize automatic recovery status. Shorten approved-work decision and revisit delays without changing Ask/Always/Never, combat, Stay, cargo transactions or catching protections.
- Add Work > Mining approach > Automatic mining. Choose Ores or Observed blocks, then individually allow/block types with native item icons, five entries per page, navigation and all-type switches. Ores default to allowed; observed non-ore terrain needs explicit opt-in. Vein remains ore-only; tool strength and other approach restrictions still apply.
- Save permissions per Sigil using stable vanilla IDs or mod content names, with bounded and validated rule lists. Old Sigils default safely without losing existing profile data. Multiplayer validates the owner, bound profile and available tile types on authority; peers need the same version.
- Apply a conservative adjacent-support check through the shared mining predicate. Plants, saplings, trees, furniture, cuttable decoration and native multi-tile object metadata protect neighboring support blocks. Direct orders bypass only the automatic permission filter, not this protection. Recheck after planning and before execution. This does not promise support detection for every unsupported mod tile or indirect world-physics effect.
- Include 0.19.2's item-emote groups and critter attention changes. Translate all nine new mining labels; each of nine catalogs has 826 keys.

## Verification

The main mod and native regression probe compile with zero warnings and zero errors. The final packaged candidate passes **37,865 native-engine assertions with zero failures**. Static localization, formatting and layout checks pass **46,851 assertions**. Most are existing broad coverage, not independent gameplay scenarios.

New native checks use actual companion AI ticks in controlled single-player and server-mode worlds:

- Thirty seconds of following recovers energy from 50 to 80. Seven light pickups cost no full point; the eighth settles one. Invalid effort and client-side spending are rejected.
- A 96-block ore assignment starting at 60 energy completes without an energy pause and retains at least 45 energy. Measurement ends with the assignment, so later idle recovery cannot inflate the result.
- Mining requests near saplings, plants, small piles, chests and torches preserve both the supporting dirt and decoration. A sapling added after a valid mining order is also protected at execution.
- Default ore access, material opt-in, individual/all ore toggles, invalid unobserved materials, Vein restrictions and cancellation of newly disallowed automatic targets are verified. Explicit orders remain distinct from automatic filters.
- Clone, native Tag save/load and ExtraAI round trips preserve permissions. Old Sigils lacking rule fields load the safe defaults without losing identity. Temporarily absent mod content retains its exclusion. Real serialized multiplayer requests reject a wrong profile and unobserved terrain while accepting a valid ore selection.
- Recovery begins below 16, remains active at 29 and clears at 30 with a new decision ready. Active recovery status survives an ExtraAI round trip.
- Production wheel click events exercise category selection, individual and all-type toggles, paging and layered Back in all nine languages at UI scales 1, 1.5 and 2. Labels resolve rather than displaying raw localization keys. These use a measurement-font fixture, not graphical screenshots.

Existing checks for cargo conservation, combat, Stay, critter rules, inventory delivery, input priority and malformed packets remain enabled. Expected negative-fixture packet warnings do not represent failed assertions.

Prepared Workshop descriptions remain below Steam's 8,000-byte limit, including the standard author quote. Historical update logs are kept in CHANGELOG.md, not appended to the upload changelog. Final publication checks are recorded separately from this candidate's test results.

## Candidate and Player Check

This report records preparation and local installation of the 0.19.3 alpha candidate. The creator subsequently uploaded it to the existing Workshop item on October 2 at 21:08 CEST, and requested GitHub publication and description updates. At that publication check, Steam still showed moderator approval pending. No new screenshots or graphical playtest are claimed. See [release notes](../releases/0.19.3.md).

No graphical client playthrough, long-session balancing or live multiplayer session was performed for this candidate. Engine fixtures do not verify visual framing or complete real-world mod interactions.

1. Confirm 0.19.3 in the loader. Open Companion > Work > Mining approach > Automatic mining, then select Ores or Observed blocks. Toggle a type, recall/resummon and check its saved state.
2. With mining initiative set to Always, check that disabled ore is left alone and enabled observed dirt can be mined under an appropriate mining approach. Unknown materials require observation first.
3. Place a seedling or decoration on soil. Check automatic and direct mining: protected support should remain. Furniture and inventory right-clicks must still retain priority.
4. Watch several mining/gathering/rest cycles. Work should last longer, energy should recover gradually, and exhausted work should restart with a usable reserve. Please report actions, energy and solo/MP context rather than only whether she seems busy.

The final local package and installation receipt are stored under `outputs/steady-hands-0.19.3`. They are not tracked in Git. Only Soulmates belongs in the normal player client; the regression probe belongs only to the isolated test runtime. Worlds and characters are not modified by packaging.

## Local Installation

On October 2 at 21:00 CEST, process checks found no running dotnet, tModLoader or Terraria process, and write permission was granted for the normal client's `Mods/Soulmates.tmod`. The final tested package was installed there: **1,547,980 bytes**, SHA256 **A14A100E39E74F1EDB21116D6248FFFD6C85BF50873693001D89C1A6D6FFE18A**. The installed hash matches the isolated engine-tested artifact.

The actual prior file was backed up as `Soulmates-before-0.19.3-20261002-210048.tmod`, with verified SHA256 **0A0FC9B5A749DAFE01A53AFF697465749A726ED984FF34BC8795662EBCD82C87**. No version is inferred from that hash. Enabled mods remain only Soulmates; its ModSources junction points to the repository. No probe, character, world, feedback journal or other mod was installed or changed. This local installation is not a public upload.
