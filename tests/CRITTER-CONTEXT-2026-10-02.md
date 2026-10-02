# Critter and Context Revision: 0.18.1

Local growing-alpha candidate, October 2, 2026. Not a public-release or full graphical-playtest claim.

## Findings and Changes

- The creator's opt-in local journal recorded Watch and Company selections and one real company join. This established that joining could occur, not that following was reliable. Native AI could reset movement to the opposite direction every tick, overpowering the previous weak blend. Company now supplies bounded walking/flight guidance after native AI, preserving collision, health, catching and despawning.
- Watch no longer shares the exclusive movement visit gate. It passively observes nearby visible natural critters alongside work. Assigned tasks and defense still outrank active visits. A current visit gets at most six seconds before yielding; blocked saved modes explain their prerequisites.
- Native-symbol greetings and personality-specific lines greet critters. Visible nearby deaths produce sorrow or apologies; Terraria's recorded player attack credit selects disapproval. This is not exact forensic attribution of a fatal hit, and no particular player is accused. Unknown deaths use grief. Native catching does not trigger a death reaction.
- Reactions are authority-only, rate-limited, and queue behind speech and pending prompts. They produce no XP, bond rewards, mood penalties or cargo changes. Soul Bolts reject harmless critters and friendly NPCs without disabling ordinary hostile combat.
- NPC/drop/natural-resource right-click context can open in default Terraria mode after native interactions have run. Empty air and furniture remain outside that fallback. Native dialogue, inventory, text entry, alternate item use, mouse-held items and changed targets retain priority.
- NPC Look changes no assignment or mode. Eligible critter Company selects the clicked NPC without requiring a net, revalidating owner, bound profile, type, range and availability. Pet Collect still requires a real carried net and cargo room.
- Added 21 keys to all nine catalogs, bringing each to 750. Existing Sigil serialization stays unchanged.

## Verification

- Main mod and native probe compile with zero warnings and errors against the installed tModLoader references.
- Static localization, text-layout, source-reference and initialization checks pass **42,617 assertions**, covering 750 keys in nine languages and 377 source references.
- The isolated tModLoader 2026.8.3.0 probe loaded packaged Soulmates 0.18.1 and passed **34,642 assertions, zero failures**.
- Native checks exercise single-player and server authority, the full companion AI join path, walking against an opposite native velocity, flying guidance, company limits/identity/release, native net catching and drop conservation, and actual `StrikeNPC` death hooks for all five personalities with and without player attack credit.
- The worldless fixture initializes native lighting. Test strikes hide graphical damage numbers and preserve explicitly configured attack credit; they still run native damage, death and mod hooks. Early fixture failures were corrected before the successful run, not ignored.
- Checks also exercise queued speech, harmless projectile filtering, default target context, clicked-NPC identity, original-target retention while moving the cursor, native input priority, forged/stale/wrong-direction network requests, and valid authorized requests. All nine real cultures are selected and their registered values compared against packaged catalogs.
- Existing cargo, wallet, creation, binding, combat, healing, learning, forestry, input, relationship and minigame checks remain in the probe.

## Limits

This is an isolated headless engine run, not playing or visually inspecting the live client. Native critter traversal across uneven ground, repeated greetings in a crowded scene, floating target labels at different UI scales, ordinary NPC interaction under the cursor, long sessions and live multiplayer still need real playtesting. Ground critters are not given a new pathfinder or teleported through obstacles.

The seven additional language translations are AI-authored and need native-speaker review. Existing server-formatted response packets, including the new NPC-context response, can retain the server language in mixed-language multiplayer; keyed speech uses each client's language, but NPC-name arguments retain authority wording. See [localization maintenance](localization/README.md).

The native journal is opt-in local evidence. No chat, world, character or save data was uploaded or edited for this test. The regression probe uses a separate save directory and is not installed in the normal client.
