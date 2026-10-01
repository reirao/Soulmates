# Soulmates Development History

## Soulmates 0.15.0 - Familiar Faces

- Store up to 24 resident relationships per companion, keyed by world, NPC type, and name. Conversations affect sympathy; first meetings and friendships add memories.
- Add native-emote meaning and personality-aware replies, role-specific resident dialogue, and a Bond option to recall local acquaintances. Anger is no longer rewarded as cheering.
- Give social visits a greeting and response turn, limit each visit to one relationship update, reject replaced residents, and cancel visits for explicit assignments, Stay, combat, or disabled autonomy.
- Move initiative answer wheels to the player; show the task symbol over the companion, followed by a question mark. Preserve stationary answer targets and per-task permission rules.
- AETHER can use a carried bug net for nearby natural butterflies, fireflies, and lightning bugs during approved forestry. Forestry > Always also permits idle catches. Reject statue-spawned and player-released insects.
- Observe the exact native catch drop and use the existing world-to-cargo transaction, with capacity checks and authority-only catching. A cosmetic flock of at most six native animated insects is backed by real carried items and disappears when withdrawn.
- Retain safe tree shaking, fallen-log clearing, and carried-acorn planting. Living tree-branch pruning and a combat swarm are not implemented.
- Move the isolated engine probe to PostAddRecipes, after loader hooks and networking IDs exist. Use disconnected test sockets for server-mode packet construction, not a claimed live multiplayer session.
- Pass 9,950 engine assertions and 2,335 static/math assertions on tModLoader 2026.8.3.0. Both compilers report zero compiler warnings/errors; the existing packager image-type warning remains. The creator has begun playing 0.15.0; a complete graphical, progression, and live-multiplayer test is not claimed.
- Remove older public screenshot galleries and screenshot release attachments at the creator's request, preserving local originals and historical test reports.

## Soulmates 0.14.0 - Attentive Souls (Prepared)

- Separate Companion, Player, and World wheels. The Player wheel has its own Emotes button; G remains a direct emote shortcut.
- Right-click clear air to open Point tools; further right-clicks cycle Point and Area wheels. The native X exits, while vanilla tile, inventory, NPC, and alternate-use interactions retain priority.
- Keep Look, Gather, Mine, and Forest tools active after each target. Look is read-only; a directed forest order tends one valid location without starting a broad gathering routine.
- Replace potion-shaped navigation with Terraria's native back, forward, and close icons.
- Rank actionable opportunities by proximity, talent, recent imitation signals, and bounded waiting priority. Use independent task cooldowns so repeated loot does not monopolize initiative.
- Expose per-task Ask / Always / Never rules in a crescent submenu. Honor Never before scanning for work and rate-limit repeated automatic speech.
- Revalidate unanswered prompts, cargo capacity, and target identity. Advance prompt timeouts through combat; remove the old full-minute decision debt after answering.
- Validate pointed loot type/prefix on the server and reject recycled item slots during directed travel. All multiplayer participants need this version.
- A real-client pass exposed companion hover stealing an open wheel's right-click; guard captured input and cover it in regressions.
- Preserve 0.13.0 cargo, save migration, resource tiers, and the uncapped wallet. No new custom graphics or online AI service.
- Keep regular speech clear of an open wheel; use binoculars consistently for Look and a loot magnet for gathering. Both compilers report zero compiler warnings/errors; 6,629 engine and 2,178 static/math assertions pass. Final icon/speech polish, live multiplayer, and long sessions still need visual/play testing.

## Soulmates 0.13.0 - Growing Cargo

- Separate Equipment, Resources, and Wallet without changing companion identity or learned behavior.
- Resources have 60 stack slots and a per-type reserve of 50 units per level, reaching 500 at level 10 and 1,000 at level 20.
- Collected coins use an exact, uncapped balance, independently of cargo capacity; native coin icons support denomination conversion on withdrawal.
- Migrate legacy materials and coins, and return resource excess on summon without deleting it.
- Use resources for forestry, torch support, and recipe suggestions; update cargo feedback snapshots and Sigil tooltips.
- Add compartment-aware transfers and binary synchronization, responsive slots, resource paging, and focused cargo/wallet regressions.
- Verified with 4,051 engine/geometry and 2,065 static/math assertions; the creator confirmed that the new cargo works in play. Broader rendering, long-session, and live multiplayer testing remain open.

## Soulmates 0.12.2 - Patient Choices

- Fixed inventory right-click consuming the reusable Soulcore.
- Block held-item and tile actions while Soulmates menus or direct targeting own the input, including a menu-closing click until release.
- Autonomy prompts wait for existing Soulmates interfaces, inventory, and NPC conversations instead of replacing them.
- Keep response nodes stationary during a choice, even if the companion moves to intercept an enemy.
- Allow a full minute to answer an initiative before it expires.
- Keep companion speech clear of vanilla NPC conversations and the player's inventory.
- Added engine regressions for reusable Soulcores, menu input ownership, release handling, and deferred prompts.

Soulmates 0.12.1 - Clear Voices

- Fixed mining approach reply paths and missing quick-action translations in English and German; mode names now use one canonical enum translation.
- Initialize UI states after localization registration and refresh mailbox labels when opened; old saved memory keys resolve as text instead of leaking internal identifiers.
- Moved speech to a zoom-aware UI overlay anchored to the companion, with its name, a subtle speaker connection, stable placement, and a restrained fading trail.
- Cached speech wrapping, preserved line breaks, and split long words safely; ambient chatter no longer immediately replaces an important reply.
- Reworked Talk Mode padding, numeric mood/energy values, level bar, response bounds, and pack hitboxes; reset resize state and added recovery from unanswered multiplayer requests.
- Companion previews and their auras now fit their real panel bounds; wheels shrink within small viewports, and long world-target labels wrap instead of running offscreen.
- Unified raw-input-to-UI coordinate conversion for wheels, pack slots, resizing, initiative prompts, and world-target outlines.
- Close conflicting interfaces and clear bindings on world exit.
- Revalidate changed terrain before mining, enforce the mining timeout during travel, count real ore blocks in tunnel vein traversal, and remove unrelated fallback excavation from blocked tunnels.
- Mining reports describe the completed plan; blocked tunnels explain their limitation, and stale ore selections refresh instead of targeting changed material.
- Added reproducible localization, wrapping, UI-transform, and isolated tModLoader engine regression checks.
- Shortened the German Workshop copy to remain below Steam's 8000-byte limit.

Soulmates 0.12.0 - Mining Intent

- Added a contextual ore crescent to the Work Soulwheel: nearby ore families appear as their real Terraria drop icons and can be assigned with one click; the manual world-target pickaxe remains available.
- Added four persistent mining approaches per companion: Adaptive, Tunnel, Vein, and Surface.
- Tunnel mode cuts a narrow passage toward nearby ore, or downward until it reaches the next open space, while keeping the existing pick-power and protected-tile checks.
- Adaptive and Tunnel mining only skim gravity-affected materials such as sand, silt, and slush from an exposed edge instead of excavating unstable buried masses.
- Changing mining approach replans an active area assignment without discarding completed work or its recovery state.
- Talk Mode now has a real companion-level progress bar, visible mining approach, and a Terraria-ruler resize handle; its content reflows within safe screen bounds.
- The Soulwheel status line now keeps level and current mining approach visible beside energy, cargo, and assignment progress.
- Mining plans and ore selections are included in local Field Notes, and multiplayer selections remain server validated.
- Existing Soulbound Sigils load as Adaptive and remain compatible.

Soulmates 0.11.0 - Deliberate Souls

- Added precise world orders from the Work Soulwheel: point at a material cluster or loose item and send the companion directly, with server-side validation in multiplayer.
- Area mining now builds one bounded work plan instead of rescanning the full radius after every block; visible status and a small progress bar show the assignment moving forward.
- Low energy now pauses the current assignment without losing its target, route, count, or direct order, then resumes that same work automatically after recovery.
- Reduced repeated gathering, forestry, and map-reveal scans; forest searches deduplicate tree roots and inspect only useful trunk endpoints.
- Repeated owner habits now become weighted imitation signals. Strong observations take priority without unrelated gathering drops erasing a recent mining or forestry lesson.
- Forestry gives quiet trees a temporary rest after repeated unsuccessful shakes and limits fruitless trips instead of hopping indefinitely between targets.
- Calmed companion travel, work movement, and idle bobbing. Speech is larger, stays near the player, and retains its soft trailing echo.
- Restored reliable player self-right-click priority for the Player Emote Wheel while preserving vanilla tile, item, NPC, and alternate-use interactions.
- Expanded local Field Notes with mining plans, recovery pauses and resumes, reinforced imitation, forestry rests, and separate work-attempt counts.
- Existing Soulbound Sigils remain compatible.

Soulmates 0.10.0 - AETHER-EGG Alpha

- Added optional, local-only AETHER Field Notes with a bounded event journal and compact behavior summary; no data is uploaded and recording is off until the player opts in.
- Added an in-game AETHER Mailbox, opened from the Companion Soulwheel's paper-plane icon or `/soulfeedback`, with separate Feedback and Bug Report letters.
- Added `/soulfeedback bug <what happened>` to preserve a bug report together with recent event types and the latest anonymized companion context in `bug-inbox.jsonl`.
- Automatic pack collection now records requested, confirmed, remaining, and rejected transaction amounts so disappearing or multiplying cargo can be diagnosed from the local journal.
- Added `/soulfeedback note`, `status`, `flush`, and `off` controls so playtesters remain in control of their own notes.
- Rebased the public version line onto an honest pre-1.0 alpha: 0.10 is the grown AETHER-EGG foundation, not a claim of near-final stability.
- Moved companion initiative questions into their own compact Terraria-emote decision prompt instead of borrowing the Companion or Player Soulwheel.
- Preserved vanilla right-click priority for world tiles, hovered inventory items, smart-interact targets, and held items with alternate use.
- Companions now face their tracked enemy throughout strafing and attacks instead of occasionally turning with their flight velocity.
- Expanded adaptive learning into five named instincts: Forager, Ore Sense, Forester, Sentinel, and Trailblazer.
- Learned instincts now prefer opportunities that mirror the owner's recent gathering, mining, forestry, combat, and exploration behavior.
- Every learned instinct announces its unlock, persists through the existing insight values, and appears in Sigil and Talk Mode learning status.
- AETHER retains every talent instinct and learned perk from the start. Existing Soulbound Sigils remain compatible.

Soulmates 9.9.0.24 - Dual Instinct Hotfix

- Separated combat and healing cooldowns so AETHER can protect and restore the player during the same encounter.
- Acquiring a new threat now accelerates only the next attack and no longer delays healing.
- Healing no longer suppresses Soul Bolt attacks for several seconds; combat no longer postpones an available heal.
- Existing Soulbound Sigils remain compatible.

Soulmates 9.9.0.23 - Native Soulwheel

- Rebuilt Companion, Player Emote, and initiative wheels around Terraria's own inventory slots, item art, and animated emote graphics.
- Removed connector lines, doubled custom frames, letter buttons, and text arrows for a cleaner in-world radial interface.
- Reworked Talk Mode into a smaller detail view with six symbolic Terraria-emote tabs and a compact icon close control.
- Companion world speech now remains readable longer, follows movement with deliberate lag, and leaves a soft fading echo when the speaker flies away.
- Reviewed English and German interface text, controls, release notes, and public descriptions against the implemented build; removed roadmap language and stale prototype wording.
- Restored the packaged small mod icon so Soulmates loads without a missing-icon warning.
- Includes the coherent cargo, initiative consent, tree-shake terrain, player right-click, and Squirrel-facing fixes completed in 0.9.4.1.
- Existing Soulbound Sigils remain compatible.

Soulmates 0.9.4.1 - Coherent Cargo Hotfix

- Unified every pack insertion behind one capacity and item-limit policy in single-player and multiplayer.
- Fixed unlike item types merging into the first occupied pack stack during automatic pickup.
- Restored reliable player right-click emote wheels over ordinary terrain while preserving real tile and NPC interactions.
- Companions now announce gathering, mining, forestry, and treasure opportunities through a compact consent wheel: Yes, No, Always, or Never.
- Initiative preferences persist per activity, while ASK AGAIN in the command wheel restores all prompts; urgent defense remains immediate.
- Fixed tree shaking occasionally using the supporting ground as a drop source, which could create stray dirt or grass drops.
- Corrected the Squirrel muse's horizontal facing direction.
- Added a practical 99-unit reserve per item type so common drops such as Gel cannot grow without bound; acorns remain capped at 12.
- The 12-acorn forestry reserve now applies to world pickup and manual storage; existing excess is returned safely to the owner on summon.
- Tree shaking and deadwood clearing no longer perform a second broad pickup sweep; their drops enter the normal visible gathering flow.
- Replaced duplicate single-player and server creation code with one atomic inventory transaction and clear timeout/full-inventory feedback.
- Soulkin frames now preload without synchronous asset requests during normal UI and world rendering.

Soulmates 0.9.4 - Tidy Talk and Forestry Supplies

- Companion speech now appears beside the companion and avoids the centered Terraria emote bubble.
- Automatic pickup keeps a practical reserve of up to 12 acorns instead of filling the pack with every tree drop.
- Partial acorn pickup removes only the confirmed stored amount; excess acorns remain safely in the world.
- Foresters accept practical planting spaces more reliably, helping them spend their carried acorn reserve.

Soulmates 0.9.3 - Context Wheels and Forestry Fix

- Right-clicking the companion now shows only companion commands, work, bonding, pack access, and details.
- Right-clicking the player or pressing the emote hotkey now opens a separate player wheel containing only Terraria emote categories.
- Fixed tree shaking being rejected because Terraria's GetTreeBottom returns the supporting ground tile rather than the final trunk tile.
- Gather and autonomous forestry now validate the supporting tile with Terraria's native tree-type lookup before shaking.

Soulmates 0.9.2 - Reliable Gathering

- Collected world items now push an explicit authoritative pack update to their owner instead of relying only on NPC synchronization.
- The pack view reads the summoned companion's live profile and eagerly loads stored item textures, so new contents appear immediately.
- The Gather command now includes nearby tree shaking, fallen-log clearing, drop collection, and acorn planting instead of leaving forestry to occasional autonomy.
- Forestry searches farther, accepts natural fallen logs regardless of background walls, and shows a visible tree emote after successful work.

Soulmates 0.9.1 - Soulwheel Input Hotfix

- Fixed Soulwheel category and action clicks being ignored when tModLoader had already consumed Terraria's shared mouse-release state.
- Left-click selection and right-click navigation now use independent press-edge tracking across every Soulwheel layer.

Soulmates 0.9.0 - Soulwheel

- Replaced the separate hold-and-release action and relationship wheels with one click-driven radial Soulwheel.
- Right-clicking either the summoned companion or your own character now opens the same compact interface.
- Commands, work, bonding, Terraria emotes, the companion pack, and full details are visible as small icon categories.
- Categories expand into crescent sub-wheels; all 151 vanilla Terraria emotes are grouped into seven paged radial categories without opening Terraria's large emote window.
- Follow, Stay, Explore, Autonomy, Treasure, Mine, and Gather are now communicated through familiar emote symbols with localized hover labels.
- The pack icon opens the companion inventory directly, while Details retains the complete Talk Mode for stats, growth, memories, and conversation.
- The Soul Creator now shows all five starting talents as directly selectable visual class icons with a clear active state.
- Unified wheel ownership prevents the old action, relationship, and native emote interfaces from competing for the same right-click.
- Existing Soulbound Sigils and multiplayer-authoritative command, pack, and emote behavior remain compatible.

Soulmates 0.8.2 - Visible Forestry

- Every autonomous companion can now shake one nearby tree or clear one fallen log during a decision cycle.
- Gatherers, AETHER, and companions who learn Forester retain the advanced five-task forestry sweep and acorn planting.
- Forester progress is now shown explicitly in companion details and Soulbound Sigil tooltips.
- Existing companions with no forestry insight can immediately perform basic forestry without recreating their Sigil.

Soulmates 0.8.1 - Loot Flow

- Autonomous looting now continues as one coordinated sweep instead of stopping after every item stack.
- Companions immediately retarget when loot disappears, becomes unavailable, or cannot be reached in time.
- Direct gathering uses shorter pauses while pack capacity, ownership reservations, energy, and server authority remain enforced.
- Foresters can now chain nearby tree shakes, fallen-log clearing, drop collection, and acorn planting into bounded work rounds.
- Tree and deadwood drops are collected immediately, including wood, acorns, and anything shaken from the canopy.

Soulmates 0.8.0 - Soul Initiative

- Right-click a summoned companion to open a compact eight-action radial menu for follow, stay, explore, work, autonomy, and details.
- The full Talk Mode remains available through Details or the configurable V hotkey and now uses a smaller layout.
- Added persistent per-companion autonomy that can be toggled directly from the radial menu.
- Gatherers retrieve nearby loose items one stack at a time, Miners assist in short bursts while their owner mines, and Treasure Seekers investigate nearby chests.
- All autonomous companions now reliably collect nearby usable drops; Gatherers do so faster and across a wider area.
- Mining assignments and autonomous mining collect resulting drops directly into the companion pack.
- Gathering now locks one valid world item through approach, storage, and network confirmation instead of changing targets every tick.
- Full packs can still accept compatible partial stacks, and blocked gathering assignments now report capacity accurately without duplicating items.
- Soulmates observe gathering, mining, forestry, combat, and exploration habits; these persistent insights gradually shape their autonomous priorities.
- Repeatedly observing woodcutting and planting unlocks the Forester perk: companions can shake trees, clear natural fallen logs, collect seeds, and carefully replant acorns from their own pack.
- AETHER is now selectable in the Soul Creator and acts as an Omni Soul with every starting talent instinct and learned perk active.
- Right-clicking your own character opens Terraria's full native emote menu with original graphics; Soulmates see any vanilla or modded emote and answer with a native bubble. The G key keeps the compact relationship wheel.
- Guardians and Healers retain their autonomous combat and recovery roles, giving every starting talent a distinct form of initiative.
- Personality-driven autonomous moments add quiet, surprising behavior without interrupting combat, explicit assignments, Stay, or low-energy recovery.
- Work assignments now pause at low energy or mood, recover at a real Stay position, and resume automatically when the companion is ready.
- Added server-authoritative Soul creation, radial actions, rate-limited learning observations, and safer state synchronization for multiplayer.
- Bounded forestry memory, stale-response protection, autonomy leashing, object-safe self right-clicks, and dynamically fitted radial labels improve long-session stability and UI clarity.
- Existing Soulbound Sigils gain autonomy by default and remain fully compatible.
- Excluded repository gallery media and the unsupported legacy small icon from the packaged mod to reduce download size and remove packaging warnings.

Soulmates 0.7.4 - Careful Cargo

- Fixed Gather assignments occasionally multiplying loose item stacks while moving them into the companion pack.
- Pack transfers now stage a safe copy and remove exactly the accepted amount from the world item.
- Companion and pack state now synchronize immediately after every successful pickup.

Soulmates 0.7.3 - Kindred Company

- Shared gestures now use Terraria's native emote bubbles.
- Longer companion chatter appears only occasionally, with context-sensitive silent reactions in between.
- Companions can approach nearby town NPCs for short, wordless emote exchanges and native NPC responses.

Soulmates 0.7.2 - Kindred Gesture

- Hold right-click directly on your own character, point toward an emote, and release to share it.
- Right-clicking the companion remains dedicated to Talk Mode, and ordinary world right-clicks remain untouched.
- The configurable G key remains available as an alternative way to open the emote wheel.
- Fixed native Terraria muse sprites sometimes remaining invisible by explicitly loading their NPC texture sheets.
- Fixed radial emote geometry stretching across the screen when Terraria's magic-pixel texture is larger than one pixel.

Soulmates 0.7.1 - Kindred Motion

- Added an original eight-frame Soulkin animation sheet with distinct idle, flight, and attack motion.
- Bestiary muse companions now animate through Terraria's own NPC frames instead of appearing as static images.
- Right-click Talk Mode now opens only when the companion itself is clicked; ordinary world right-clicks no longer activate the Sigil.
- Direct interaction now fully suppresses Terraria's fallback NPC chat window.
- Slowed idle animation to a calm two-second loop while keeping flight and combat responsive.
- Migrated Soulkin animation to fixed frame assets so companions created by older Sigils always remain visible.
- Enforced one active companion per player and cleanly stops replaced companions, jobs, and soul bolts.
- Companions now keep a pet-like combat leash around their owner or Stay anchor and stop chaining into distant enemies.
- Nearby enemies actively attacking the owner remain the highest-priority defense targets.

Soulmates 0.7.0 - Living Bond

- Added persistent companion experience and 20 levels earned through combat, healing, completed work, and shared moments.
- Added a hold-and-release radial emote wheel with six personality-aware social interactions.
- Companions now speak on their own through floating world bubbles and react to weather, caves, injuries, rest, and quiet travel.
- Combat reactions now reflect personality, with distinct responses to ordinary victories, bosses, and creature deaths.
- Added animated emote gestures, new memories, victory and interaction counters, and visible level progress.
- Fixed Stay so companions defend a synchronized world anchor instead of drifting back to the player or firing from a stale position.

Soulmates 0.6.4 - True Guard

- Fixed Host & Play combat authority: the server now owns target selection and clients render only the synchronized target.
- Soul bolts now use server-owned friendly NPC projectile semantics instead of conflicting player ownership.
- Expanded hostile target validation and added explicit Target Dummy support for reliable combat testing.
- Preserved player interaction credit when a companion damages an enemy.
- Rebalanced soul-bolt damage and cadence so companions protect without replacing early-game combat.
- Companions now recover energy naturally at a useful pace while following and faster while waiting; manual rest is optional.

Soulmates 0.6.3 - Steadfast

- Basic defense no longer consumes work energy, so companions keep protecting their owner at zero energy.
- Companions retain targets longer, intercept more smoothly, and visibly strafe while fighting.
- Soul bolts are larger and brighter with a luminous trail, stronger homing, impact sound, and hit burst.
- Guardians attack faster and deal clearer early-game damage; every other talent remains a capable defender.
- Talk Mode is smaller and replaces the overflowing text meters with compact visual mood and energy bars.
- Combat and watch states are now short, readable, and visible without colliding with the close button.

Soulmates 0.6.2 - Faithful Guardian

- Fixed companions detecting nearby enemies but never attacking when terrain blocked Terraria's line-of-sight check.
- Expanded defense awareness and made enemies attacking the owner a priority.
- Added visible watching, guarding, and low-energy defense states to Talk Mode.
- Hardened soul bolt and Guardian memory synchronization for multiplayer.
- Pack slot withdrawals are now server-authoritative and synchronize the returned inventory item.
- Prevented clients from independently restarting persistent work assignments.

Soulmates 0.6.1 - Bound Together

- Fixed companions disappearing immediately in multiplayer sessions.
- Summoning, recalling, conversations, jobs, pack actions, and trinkets are now server-authoritative.
- Companion profiles and conversation replies now synchronize back to the owning player.
- Prevented duplicate client-side healing, projectiles, gathering, and world interaction.
- Added owner validation so invalid network state cancels safely instead of crashing Talk Mode.
- Every companion now defends against nearby threats; Guardians react faster, reach farther, and hit harder.
- Talk Mode now shows talent, mood and energy meters, exact action costs, and actionable refusal requirements.
- Resting immediately restores 30 energy, while waiting provides much faster passive recovery.

Soulmates 0.6.0 - Living Souls

- Guardian companions now intercept nearby threats and fire bond-scaled soul bolts.
- Healer companions now react to meaningful injuries and restore life with an energy cost and cooldown.
- Added five bond ranks: Newbound, Kindred, Trusted, Soulbound, and Eternal.
- Bond ranks improve work radius, role strength, and late-rank pack capacity.
- Added a persistent eight-entry memory chronicle for creation, jobs, protection, healing, trinkets, and bond milestones.
- Repeated memory conversations now cycle through recent shared experiences.
- Companion pack slots now show full item tooltips and support left-click stack withdrawal or right-click single-item withdrawal.
- Fixed early UI localization and arranged talk categories in a clearer two-row layout.
- Preserved compatibility with existing Soulbound Sigils and localized all new content in English and German.
