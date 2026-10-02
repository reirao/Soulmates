# Soulmates Development History

## Soulmates 0.19.3 - Steady Hands

- Protect blocks next to plants, saplings, trees, furniture and decorations in every mining path. Recheck already planned targets and direct orders; use native multi-tile data in the conservative support check.
- Add Work > Mining approach > Automatic mining: paged native-icon ore and observed-block toggles and all-type switches. Ores start allowed; learned terrain needs explicit opt-in. Persist stable tile keys on the Sigil and validate owner/profile/type permissions on multiplayer authority. Direct jobs retain safeguards but do not inherit automatic filters.
- Settle work effort in batches: eight retrieved stacks, four mined blocks or two successful forestry actions per energy point. Reduce work-command entry fees and healer energy cost; UI costs use the same dialogue policy.
- Restore two energy per two seconds while following and one per three seconds during work. Preserve recovery progress between tasks. Rebuild a 30-energy reserve after exhaustion before restarting autonomous or retained work; synchronize the recovery status.
- Scan sooner and shorten gathering/mining/forestry revisit delays. Keep permissions, cargo transactions, catch rules, completed-tree tracking, combat, Stay and explicit work priority.
- Old Sigils load with compatible default rules; multiplayer peers need the same version. The creator uploaded 0.19.3 to the existing Workshop item on October 2 at 21:08 CEST. Moderator approval and subscriber delivery are separate from upload. Engine fixtures are not a graphical or live multiplayer playtest.

## Soulmates 0.19.2 - Small Encounters

- Put a real subgroup layer before native item emotes: Food, Recovery, Tools, Weapons, Materials, Valuables and Party. Preserve all 151 vanilla symbols and stepwise Back navigation without adding an extra ring.
- Run passive critter observations in every non-Off mode, including during work. Queue greeting speech instead of silently dropping it while another line is active; reject stale creatures and clear disabled observations.
- Give a bounded critter visit a turn before selecting another automatic task when no current work is active. Keep active jobs, ongoing automatic work, combat, Stay, prompts, energy and native catch protections intact; add a five-second visit interval.
- Translate the seven subgroup labels in all nine catalogs, bringing each to 817 keys. Extend native click-routing and critter-priority checks. Initially prepared and tested locally; the creator then uploaded it to the existing Workshop item on October 2 at 20:32 CEST. Included in GitHub release 0.19.3.

## Soulmates 0.19.1 - Clear Choices

- Recalculate native UI click bounds whenever conversation categories rearrange controls. Item-topic buttons and reply buttons no longer retain overlapping positions from the previous category.
- Put Items directly on the companion wheel. Selecting a food, ore, tool or other topic immediately gives its cargo response and displays the active topic name.
- Selecting Me or Soulmate now closes the wheel and arms the next world right-click. Keep right-click cycling within open wheels and X/Escape returning to Terraria. Preserve native interactions in Terraria mode.
- Add opt-in local journal entries for category/topic selections, mode changes and context actions. Record decisions, not raw chat or per-frame mouse tracking.
- Reproduce the old UI defect with native hit testing and event dispatch, then test the complete mode/drop/NPC flow through production input hooks and wheel clicks. This is not a claim of a completed graphical or live multiplayer playtest.
- Include the previously local 0.17.2-0.19.0 systems in this release. The creator uploaded 0.19.1 on October 2; Workshop moderation and subscriber delivery are separate from that upload. Player-wheel item subcategories and Company activity starvation are new open playtest reports, not fixes claimed for this release.

## Soulmates 0.19.0 - Living Chapters (Local Candidate)

- Add item conversations with native-icon categories for food, ores, tools, weapons, recovery, materials, critters and other cargo. Discuss actual held or carried items without consuming them or farming progression.
- Replace the generic personal memory response with "Do you remember?" and short, personality-aware chapters composed from real recorded events. Remember actual confirmed pickups, greetings and losses; describe visible surroundings when no relevant memory exists. Add occasional bounded environmental remarks that wait behind existing speech and prompts.
- Expand the critter target context to Look, Company and Collect. Direct invitations replace the current assignment rather than silently waiting behind it; automatic visits still yield to work and low energy. Selected catching uses that exact NPC, a real carried net, cargo capacity and native catch/drop rules.
- Add Pause, Resume and Abort under Commands. Pause retains work and recovers energy; Resume continues it; Abort clears it and holds automatic work until resumed or a new command. Defense remains available. Save the hold on the Sigil and synchronize targeted visits.
- Safely prune dry vanilla side branches using native frame and stem checks and a real axe. Direct forestry accepts them; the Forester perk/AETHER can choose them autonomously. Native drops enter normal collection; leafy branches, stems, ground, gem trees and unsupported tree families are not pruning targets.
- Give long dialogue more room and scrolling in Talk Mode. Longer world speech has bounded reading time and viewport-aware layout. Add all new text to all nine languages. Old Sigils load unpaused; multiplayer participants must use the same mod version.
- This remains an unpublished local candidate. Test outcomes and remaining graphical/live-multiplayer scope are recorded separately, not inferred from compilation.

## Soulmates 0.18.1 - Small Friends (Local Candidate)

- Fix company guidance being overpowered by native walking and flying directions. Keep real critter AI, collision, health, catches and despawning, with no copies or teleporting through terrain.
- Make Watch a passive nearby observation that can coexist with work. Greet critters with native symbols and personality-specific lines; busy speech no longer consumes an unheard observation cooldown.
- Open the clicked NPC, drop or natural-resource context directly if Terraria does not handle the right-click first. Keep empty air, furniture, inventory, typing, native dialogue and alternate item use out of the fallback path.
- Add NPC Look and eligible critter Company actions, preserving the clicked target and validating profile, owner, type, range and availability on authority. Explain blocked modes; bound visits to six seconds without overriding assigned work or defense.
- React to nearby visible critter deaths with sorrow or apologies, and disapproval when Terraria records player attack credit. Queue lines behind speech, rate-limit reactions, distinguish native catches from deaths and grant no progression or penalties. Prevent Soul Bolts from hitting harmless critters.
- Add 21 translated keys across all nine catalogs, bringing each to 750. Preserve existing Sigil serialization. This is a local candidate, not a public release; graphical, terrain traversal and live multiplayer checks remain outstanding.

## Soulmates 0.18.0 - Many Voices (Local Candidate)

- Complete all 729 localization keys in Italian, French, Spanish, Russian, Brazilian Portuguese, Polish and Simplified Chinese, alongside the existing English and German catalogs. These are the nine languages supported by the current Terraria 1.4.4 runtime.
- Localize menus, work, memories, money gifts, questions, resident relationships, emote reactions and games. Keep proper names intact; select the active catalog through Terraria's language setting.
- Fit Creator and mailbox buttons, titles and longer wrapped text into their existing controls. Keep native symbols, input routing, jobs and save formats unchanged.
- Add all-language coverage, format, Unicode wrapping and runtime-selection checks. Keep the translation authoring data and mechanical catalog generation reproducible under tests/localization.
- Include the unpublished 0.17.2 minigame. This candidate is not a public release; the new AI-authored translations still need native-speaker feedback and graphical review.

## Soulmates 0.17.2 - A Little Game (Local Candidate)

- Add real rock, paper, scissors rounds using Terraria's existing RPS emotes, with a three-symbol submenu under Companion Soulwheel > Bond. Native player RPS emotes also start rounds.
- Draw an independent random companion move on authority, show both choices and the correct outcome, and reply in English or German according to personality and voice. Direct stays factual; low mood or energy uses a quiet line.
- Rate-limit duplicate rounds, validate binding and proximity, preserve pending questions, and validate identity and choices on client result delivery. Games award no XP and do not alter work, cargo, mood, energy or permissions.
- Keep this local test candidate separate from the published 0.17.1 GitHub release and its prepared Workshop upload. No public update is claimed for 0.17.2.

## Soulmates 0.17.1 - Shared Discoveries

- Give the existing Look/pointing interaction a short personal response shaped by personality, saved voice, mood and energy. Preserve the concrete target/capacity/mining facts; Direct stays concise.
- Describe coins as uncapped wallet contents rather than finite item-cargo capacity. Add complete English and German responses.
- Inspection changes no assignments, terrain, cargo, permissions or progression. Existing native player-emote reactions remain separate and keep their existing reward cooldown.
- Include all deliberate mouse-mode work from the unpublished 0.17.0 candidate in this growing-alpha release. Keep existing Sigils compatible and update the current release text without repeating historical logs.
- Pass 10,787 native-engine assertions and 3,337 static/localization/layout assertions. The creator played the isolated 0.17.1 build and approved releasing it; full progression, long sessions and live multiplayer remain test work.

## Soulmates 0.17.0 - Deliberate Mouse Modes (Local Candidate)

- Make Terraria the default world right-click mode. Stop opening a mod wheel on every empty-air click.
- Add deliberate Me and Soulmate mouse modes with native-symbol selectors in the Soulwheel and a small cursor indicator while active. Right-click in an open wheel cycles modes; X or Escape returns to native use.
- Keep character-bound wheels separate from context-sensitive world targets. Me offers pointing and native player emotes; Soulmate offers only applicable Look, Gather, Mine and Forest actions, with the existing full tools still accessible.
- Snapshot the original target before moving onto menu buttons. Reject changed tiles, vanished drops and reused item/NPC slots; recheck range, mining protection and cargo eligibility before sending an order.
- Consolidate companion and player right-click routing around raw, view-transformed mouse coordinates. Preserve inventory, typing, NPC dialogue and cursor-held-item priority, plus the menu-closing-click release latch.
- Keep existing Sigils and companion preferences compatible. Mouse mode is local session state, not a saved terrain-edit permission. Installation, graphical playtesting and publication are separate steps.
- Pass 10,686 isolated native-engine assertions and 3,294 static/localization/layout assertions. Compile and package without errors or warnings; see [the input revision report](tests/INPUT-MODES-2026-10-02.md) for the remaining real-client and live multiplayer scope.

## Soulmates 0.16.1 - Here For You (Uploaded, Workshop Review Pending)

- Prioritize helpful work before critter visits; shorten idle decisions and add approved close-range pickup through the existing world-to-cargo conservation transaction.
- Prioritize nearby coins without capping the wallet. Preserve native reservations, no-grab delay, shared capacity, Stay, disabled autonomy and server authority.
- Add personal Company and Wallet questions with four native-symbol answers. Save voice and explicit gathering preferences; change mood and bond without granting unrelated mining or forestry permissions. Deferral is neutral.
- Add ten personality-specific wallet-offer lines. Accept, save together, respond playfully or defer; use native inventory insertion and debit only the actually delivered coins. Keep full-inventory or very large-wallet remainders safe.
- Validate responses by bound profile, owner and unique question token. Reject stale/replayed answers and clear questions when context changes. Preserve personality chatter alongside the chosen voice.
- Reject obstructed, sloped, half-block and actuated planting sites; reserve growing room and verify one native sapling consumes exactly one acorn.
- Record opt-in local question/answer/gift outcomes and preference context. Record explicit Pet Collect rejection when no net is carried.
- Prepare a separate 0.16.1 candidate for another real playtest. No running installation, Steam upload or GitHub publication is changed by this revision; see [the test scope](tests/REVISION-2026-10-02.md).
- Follow-up: the tested package was installed locally, and the creator uploaded 0.16.1 to Steam on October 2 at 11:29 CEST. The Workshop reports moderator approval pending. Current descriptions use Soulmates as the mod name; AETHER remains the companion name. This text update does not create a GitHub release or add a gameplay test claim.

## Soulmates 0.16.0 - Little Company (Prepared, Not Published)

- Add a native-symbol Critters branch to the Companion Soulwheel with Watch, Company, Pet Collect and Off. Save the choice on each Sigil; existing Sigils default to non-capturing Watch.
- Let every companion use a carried catching tool for nearby common natural critters through native CheckCatchNPC and the existing exact-drop cargo transaction. Preserve lava-net requirements, mod catch hooks and capacity limits; exclude gold, statue-spawned and player-released critters.
- Invite existing native bunnies, squirrels, birds, butterflies, fireflies and lightning bugs as temporary company. Ordinary companions invite one; AETHER invites up to three. No NPCs or items are created by friendship.
- Guide those real NPCs gently after their native AI, retaining health, collision, catchability, drops and animation. Do not teleport stragglers through terrain. Release company on Off, recall, lost binding, disabled autonomy or excessive distance; synchronize by companion identity rather than NPC slot alone.
- Add quiet personality-specific observations and native emotes. Work, combat, Stay, social visits and initiative questions take precedence. Explicit Pet Collect replaces implicit idle forestry catches; AETHER's item-backed cosmetic insect flock is unchanged.
- Vanilla vanity-pet items remain player-bound. This patch does not add companion pet-buff slots, permanent saved critter pets, a combat swarm or living tree pruning.
- Correct the latest 0.15.3 Steam changelog in place and preserve historical entries. New Critter work is a separate local candidate, not part of the uploaded hotfix.
- Pass 10,336 isolated engine assertions and 2,463 static/math assertions with zero compiler errors/warnings. New graphical and live multiplayer playtests remain outstanding; see [the candidate scope](releases/0.16.0.md).

## Soulmates 0.15.3 - Bound Delivery

- Fix successful Host & Play creation leaving the new Soulbound Sigil invisible in the owner's inventory. Native unsolicited SyncEquipment updates are ignored by ordinary owning clients.
- Deliver only transaction-changed inventory slots through an owner-only mod response using native ItemIO serialization; update the client inventory cache and acknowledge the affected slots normally.
- Reuse that path for cargo deposits, withdrawals, coin transfers, legacy cargo returns, and recall. Preserve unrelated slots and the cursor-held item; parse complete bounded updates before applying them.
- Record local multiplayer creation results and add an actual serialized server-to-client creation counterexample. This failure was missed by the previous single-player graphical pass.
- Preserve item favorites and verify owner delivery, resource/coin withdrawals, native item save/load and malformed-update atomicity. The 0.15.2 baseline fails five creation checks; 0.15.3 passes 10,118 engine assertions. See [the hotfix report](tests/HOTFIX-2026-10-02.md) for scope and remaining live-test gaps.

## Soulmates 0.15.2 - Clear Targets (Unreleased)

- Fix pointed Mine, Gather, Look, and Forest targets drifting from the cursor in scaled UI hooks. Convert raw input through the inverse world view, including zoom.
- Open cargo on its first populated compartment and show compartment counts. Keep storage-tab hover labels in the cargo header rather than over the first item slots.
- Preserve the existing pickup transaction, resource limits, and unlimited wallet; this patch does not replace the collection logic.
- Notice nearby residents' native emotes even outside an existing visit. Queue a short personality-aware reply, retain relationship memory, and respect social cooldowns, Stay, assigned work, defense, and server authority.
- Add targeted coordinate round-trip, initial cargo-tab, and resident-emote regressions. Real Classic-client evidence and remaining gaps are recorded in [the playtest report](tests/PLAYTEST-2026-10-02.md); no publication is implied.

## Soulmates 0.15.1 - Careful Company

- Keep wheel shortcuts, companion clicks, and automatic questions out of native chat/sign/chest text entry and existing Soulmates interfaces.
- Wait for the multiplayer acknowledgement before reopening an answered initiative wheel, with a bounded retry timeout.
- Distinguish cargo-blocked drops from an empty gathering area; reject independent client-side world-to-cargo mutations.
- Send actual healing deltas, mana recovery, and potion/food buffs to the owning client rather than relying on observer-only player-state packets.
- Ignore invulnerable enemies, retain repeated attacks, and verify facing, Stay anchoring, server projectile ownership, and companion-specific recall cleanup.
- Recall when the matching Sigil leaves the carried inventory; preserve single-player cursor-held binding during inventory rearrangement.
- Validate the carried binding at the multiplayer request boundary too, preventing queued actions after a Sigil move or owner death.
- Save perk-unlock XP in the same update and report failed summons when all NPC slots are occupied.
- Preserve the mailbox's full 360-character note allowance; omit exact coordinates from new automatic records and accurately distinguish typed notes in privacy text. Existing local logs are untouched.
- Cache the Field Notes opt-in marker and reuse cargo-observation buffers, avoiding repeated filesystem checks and per-frame container allocation.
- Avoid the installed packer's exhausted-stream conversion for `icon_small.png` by excluding that optional asset. Keep its source, the normal mod/Workshop icons, and all gameplay graphics; this removes the longstanding image-type warning without editing Terraria or replacing images.
- Expand focused regression coverage without replacing existing companions or changing resource tiers, the wallet, forestry permissions, or relationships. See [the revision report](tests/REVISION-2026-10-01.md) for verification and remaining test gaps.

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
