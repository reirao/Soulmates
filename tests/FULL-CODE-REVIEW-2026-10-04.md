# Soulmates 0.19.6: Full Source Review

**Historical baseline:** the defects and results below describe the unchanged 0.19.6 package. Their subsequent repairs and critical counterchecks are tracked separately in [the 0.19.7 repair report](COMPLETE-REPAIRS-2026-10-04.md). Line references below belong to the reviewed snapshot, not the repaired source.

## Verdict

**Not an all-clear.** Eight additional defects are reproduced through production code by nine failed safety expectations. One further modded-material persistence inconsistency is source-confirmed. The preceding 0.19.6 repairs still pass their 67 counterchecks; the expanded independent audit now has **80 expectations: 71 pass and 9 fail**, with no harness errors. The older passing suite did not cover these boundaries.

This is a code review, not a new release or gameplay patch. Only test-only audit code, an ignored inspection helper and review documentation were changed in this pass. Existing uncommitted runtime work was preserved. No normal installation, character/world save, Git commit/push, Workshop page or publication was changed.

## Findings

Priority means repair urgency, not proof that every ordinary play session triggers the defect. P1 denotes the inventory-loss risk; P2 denotes the other concrete conditional correctness defects. Native boundary reproductions are distinguished from a connected multiplayer playthrough throughout.

### 1. [P1] A late owner-inventory update overwrites an intervening slot change

Locations: [CompanionInventorySync.Apply](../Common/CompanionInventorySync.cs#L41), [inventory-update sender](../Soulmates.cs#L809).

Server transactions capture a before-image only to determine which slots changed. The transmitted update contains the slot and replacement item, but no before-image, inventory revision or conflict acknowledgement. `Apply` parses the packet and then unconditionally replaces both the local player's slot and `Main.clientPlayer`'s slot. It sends that overwritten state back via native equipment synchronization.

Native event-order fixture: prepare an update that puts nine Wood in slot 3; change that slot locally to a Gold Pickaxe before delivery; invoke the actual client `Apply`. Slot 3 becomes Wood and the pickaxe is no longer there. Parsing validates slot bounds and duplicates, but cannot detect this conflict.

This establishes destructive last-write behavior, not an observed connected-session loss. Actual frequency depends on packet timing and whether the player can change affected slots while an operation is in flight. Creation, cargo transfers and reconciliation use this mechanism, so it should be treated as a release-blocking data-integrity risk. A repair needs a transaction/revision and reconciliation policy that preserves items on both sides; simply dropping a stale update after the server has already committed is not sufficient.

### 2. [P2] A server trinket request equips an item the owner does not possess

Locations: [HandleTrinketRequest](../Soulmates.cs#L501), [EquipTrinket](../Content/NPCs/SoulboundCompanion.Interaction.cs#L297), [normal item use](../Content/Items/CompanionTrinkets.cs#L36).

The handler checks the companion GUID, sender and enum, but does not check ownership of a corresponding trinket. `EquipTrinket` directly assigns the enum and saves the effect. Normal left-use runs through the real item; the custom request is currently sent only for alternate-use removal, yet the server also accepts arbitrary non-None equipment values.

Native reproduction: verify that the owner has no `CompanionTrinketItem`, then deliver a profile-bound Hearth Ribbon request. The profile receives Hearth Ribbon. This is a forged-request equipment/progression bypass for the sender's own companion, not theft from another player.

Narrow the removal request to removal, or validate the authoritative owned/used item before allowing equip. These trinkets are reusable; consuming them is not the required repair.

### 3. [P2] An obsolete emote packet can authorize a replacement work question

Locations: [unused sender](../Soulmates.cs#L174), [still-active handler](../Soulmates.cs#L634), [current-token substitution](../Content/NPCs/SoulboundCompanion.Interaction.cs#L325).

The supported initiative-response protocol now correctly binds answers to a unique question GUID. However, the older custom `NativeEmoteRequest` remains dispatched. It contains only companion GUID and emote ID; `ReactToNativeEmote` interprets it against the currently pending question and supplies that question's current token.

Native reproduction: create an Ask gathering question for item 72 and capture a custom Wink packet; replace the question with an Ask for item 73; deliver the old packet. Item 73 starts and Gathering changes to Always. The dedicated token-bound answer counterchecks still pass.

The custom sender has no production callers: real Terraria emotes are observed through `SoulmateEmoteObserver`. This is a dormant network entry point, not a claim that the current ordinary client routinely sends this packet. Retire the unused channel without accidentally renumbering other protocol identifiers. Keep native live-emote interpretation separate from captured, token-bound UI answers.

### 4. [P2] Direct-order sender and reader disagree on prefix width

Locations: [SendDirectOrderRequest](../Soulmates.cs#L239), [ReadByte in HandleDirectOrderRequest](../Soulmates.cs#L776).

In the installed tModLoader API, `Item.prefix` is an `Int32`, including in native `ItemIO.Send`. Consequently `packet.Write(target?.prefix ?? (byte)0)` writes an Int32. The receiver reads one byte. Three bytes remain unread and prefix values above 255 are truncated, so a valid modded-prefix item cannot satisfy the identity check.

Native protocol-width fixture: a loose Copper Shortsword with synthetic prefix metadata 300 cannot start Gather through the production handler; prefix 0 in the same fixture does start it. The synthetic value isolates field width; this is not a registered third-party prefix gameplay test. The installed native API confirms why wider prefixes must be supported.

Use one explicit shared representation on both sides and test normal, wide and malformed values. Native stack serialization uses full-width integers as well; the separate suspicion of a native short-stack overflow was rejected.

### 5. [P2] A queued mining order can silently change material before server execution

Locations: [direct-order sender](../Soulmates.cs#L239), [handler](../Soulmates.cs#L769), [directed target planning](../Content/NPCs/SoulboundCompanion.Work.cs#L208).

A mining request carries coordinates but not the original terrain identity. Item-target type/prefix checks apply only to Gather/Look. On receipt, Mine resamples whichever tile currently occupies the coordinates and plans that material's connected targets. The local `SoulwheelTarget` check occurs before transmission; it cannot guard later world changes.

Native reproduction: capture a Mine request while the tile is Copper, replace it with eligible learned Stone, then deliver the packet. The companion starts Mine with `directedMiningTileType=Stone` rather than rejecting the outdated target.

This is an ordering boundary, not a connected latency playtest or a bypass of general mining safety. Capture and revalidate the original terrain identity for destructive orders before beginning replacement work.

### 6. [P2] Automatic gathering ignores the pickup grace period used by ordinary gathering

Locations: [FindAutonomousLooseItem](../Content/NPCs/SoulboundCompanion.Autonomy.cs#L488), [UpdateAutonomousFetch](../Content/NPCs/SoulboundCompanion.Autonomy.cs#L165), [shared eligibility](../Content/NPCs/SoulboundCompanion.Inventory.cs#L146). Compare [ordinary selection](../Content/NPCs/SoulboundCompanion.Work.cs#L771) and [nearby collection](../Content/NPCs/SoulboundCompanion.Work.cs#L883).

Ordinary gathering and nearby pickup reject `noGrabDelay > 0`. Automatic selection and fetch execution call capacity/eligibility checks without that restriction. A supposedly centralized transfer can therefore be reached under inconsistent eligibility rules.

Two native expectations fail: automatic search selects a Gel drop with `noGrabDelay=1000`, and actual fetch removes all three units into cargo while the delay remains active. Units are conserved in this fixture; the demonstrated failure is premature pickup, not duplication or loss.

Put the common pickup eligibility rule at the transfer boundary and use it consistently. If native net-catch drops require an immediate receipt, make that a narrowly validated exception rather than allowing every automatic fetch to ignore the delay.

### 7. [P2] Critter-care questions expire before the general conversation cooldown allows them

Locations: [30-second deferred care window](../Content/NPCs/SoulboundCompanion.Nature.cs#L449), [cooldown and expiry](../Content/NPCs/SoulboundCompanion.Conversation.cs#L43), [question opening](../Content/NPCs/SoulboundCompanion.Conversation.cs#L73).

After a visible loss reaction, `pendingCritterCareTicks` is set to 1,800. The general personal-question cooldown is 7,200 ticks for Chatty and 18,000 otherwise. Each tick decrements the pending window even while that cooldown forbids opening a question. A recent ordinary question therefore causes the queued care follow-up to disappear silently.

Native control opens the care question when immediately eligible. With an existing 7,200-tick general cooldown, the same queued follow-up expires after 1,801 updates: pending ticks 0, remaining general cooldown 5,399, no question. This does not show that the grief line itself is absent; the lost element is the answerable follow-up.

Either retain a deferred care event until it can be offered, or establish an explicit independent priority/cooldown rule. Do not advertise the queue as reliable while it normally expires behind a longer gate.

### 8. [P2] Failed feedback writes leave an unbounded retry buffer

Locations: [Record](../Common/Feedback/SoulmatesFeedbackSystem.cs#L139), [Flush](../Common/Feedback/SoulmatesFeedbackSystem.cs#L205).

At 64 pending lines, `Record` calls `Flush`. On IO failure the catch leaves `PendingLines` intact. Every later event appends another line and retries. The four-MiB limit protects successful disk output, not the in-memory queue during disk-full/access failures. Repeated size calculation and IO retries can also cause sustained work on the game thread.

Native reproduction deliberately uses a directory as the session file within the isolated test save folder. After 128 events, the queue has 128 lines and `LastError` is set. This forces a controlled append failure; it is not an observed normal-game crash or an attempt to access an external protected path.

Use a bounded buffer, retry backoff and visible failure accounting. Preserve manually typed bug notes deliberately rather than silently treating all records as unlimited recoverable backlog.

### 9. [P2, source-confirmed] Learned modded materials are saved as unstable numeric tile IDs

Locations: [saved learned materials](../Common/CompanionProfile.cs#L532), [load](../Common/CompanionProfile.cs#L588), [knowledge lookup](../Common/CompanionProfile.cs#L824), [stable rule keys](../Common/CompanionMiningRules.cs#L25).

Automatic allow/block preferences deliberately use stable `ModTile.FullName` keys because modded tile IDs can change with load order. The adjacent learned-material list is persisted as plain integers, and `KnowsMiningMaterial` checks numeric membership. Reloading after a mod-set/content change can therefore make the companion forget a learned modded material or associate the old ID with a different current material. Non-ore auto-selection additionally requires an allow key, so this is not a claim that every remap automatically destroys the new tile.

The inconsistency is visible in save/load, binary transport and knowledge checks. A real two-mod-set save/reload reproduction was not performed here. Keep native runtime IDs for same-session transport, but persist learned modded material identity with stable names and an explicit migration/missing-content policy. Vanilla tile IDs do not have this problem.

## Wiring and Unused Paths

The principal visible menu entries are not mere placeholders: creator binding, Details/Talk, item topics, inventory pages, question answers, rules, world-target orders, critter modes and Pause/Resume/Abort have production action paths. Their existence is not proof that all transitions or third-party targets work. The reproduced problems mostly sit at eligibility, expiry and authority boundaries rather than completely absent button callbacks.

Compiled-IL reference inspection was filtered manually. Compiler-generated record setters/operators, enum `value__` fields, hook overrides and test/public API surface were not classified as removable gameplay code simply because a direct call was absent.

| Candidate | Production reference status | Recommended treatment |
| --- | --- | --- |
| `SendNativeEmoteRequest` and its custom packet handler | Sender unused; handler still externally reachable | Retire obsolete protocol entry, finding 3; do not simply shift enum ordinals. |
| `SoulboundCompanion.ActivityFor` | Private helper has no callers | Remove after confirming no intended missing transition depends on it. |
| `ToggleCommand` | Public wrapper unused internally | Choose whether it is supported external API; otherwise remove. |
| `WithdrawPackSlot` | Old public wrapper unused internally; current UI uses storage-kind-aware withdrawal | Remove or explicitly retain compatibility API. |
| `CompanionProfile.LatestMemory` / `RecallMemory` | No production callers; current narrative path is `TellRememberedStory` | Remove/retire duplicate API or document its distinct purpose. |
| `CompanionProfile.CanStore` | No production callers; callers use amount-aware capacity | Remove or retain intentional public convenience API. |
| `CompanionWheelSystem.OpenContext(companion, worldMouse, uiMouse)` | Unused overload; current path supplies captured `SoulwheelTarget` | Prefer captured-target path; do not recreate context after input ownership changed. |
| `CommandName`, `CurrentJob`, `CurrentJobRadius` | Public getters unused by production; some used by tests | Not automatically dead. Keep purposeful diagnostics/API, or retire deliberately. |

No consequential write-only production state field was established by the IL scan. Repeated broad imports across NPC partials are cleanup noise, not the explanation for behavioral failures.

## Conversation Requirements and Documentation

The [historical full requirements matrix](FULL-AUDIT-2026-10-03.md#historical-requirements-matrix) remains a record of the supplied conversation, not a current all-clear. The four old defects and personal-question work gate were repaired in [0.19.6](REPAIRS-2026-10-04.md); findings above are additional. Current distinctions that matter:

| Requested behavior | Current implementation or gap |
| --- | --- |
| Reusable creator, individual bound companions, growth, mood, bond | Real production systems; creator names remain nine presets, not free typing. |
| Full Bestiary choice / native pet behavior | Five visual families, not full Bestiary or adoption of every vanilla pet AI. |
| Wallet plus level-scaled resources | Implemented; unlimited BigInteger wallet, 50 units per level per item type up to 1,000 at level 20, 60 resource stack slots. Inventory update ordering remains finding 1. |
| Useful autonomous work and passive energy recovery | Implemented with permissions, recovery, leashes and cooldowns; long-session pacing/energy balance is not certified. Questions no longer broadly block Always-approved work. |
| Mine all ore / selected learned blocks | Bounded planning, tool/progression/safety rules and preferences, not unrestricted terrain destruction. Direct/automatic mining bursts are capped at 24 targets. Material persistence has finding 9. |
| Trees, branches, seeds and planting | Native shaking, supported dry side branches/fallen logs and acorn planting; intentionally excludes live leafy/stem/support terrain and unsupported families. A Forester perk or AETHER governs automatic forestry. |
| Adapt to player behavior and surroundings | Bounded rule-based insight and a 48-material knowledge list; no arbitrary learned task programs, external AI or automatic training from mailbox notes. |
| Use the things she carries | Specific recovery/food/light actions and a weapon-derived bonus; not arbitrary item execution, full native weapon/ammo/accessory usage or pet summoning. |
| Her own additional vanilla pet item / summon | Still open. Cargo can hold such items; no companion-owned vanity-pet summon system exists. |
| Critter greetings, following, catching and losses | Real native hooks and bounded supported company/catch behavior. One ordinary or three AETHER temporary critter friends; up to six cosmetic carried-insect sprites, not a fighting or permanent saved pet army. Care follow-up has finding 7. |
| Separate player wheel and companion wheel | Implemented, but the player's wheel/key still requires an active companion. This is a functional boundary, not an independent always-available emote tool. |
| Native emotes, categories, answers and combined expressions | All 151 vanilla emotes and categories; combinations are timed sequences, not arbitrary simultaneous native bubbles. Dedicated question replies are token-bound; obsolete custom channel has finding 3. |
| Relationships, stories and remembered moments | Bounded resident affinity and template-based chapters from events, not a general dialogue model or exact context-linked memory for every pointing/emote combination. |
| Rock-paper-scissors | Real independent rounds, native symbols, personality text and local feedback event, no XP. The historical matrix's claim of a saved round memory was too broad: current Games code does not call `Profile.Remember`. |
| Longer, stable, companion-origin speech | Length-based lifetime, world anchor/tail and fade exist in code. This pass does not verify new rendering screenshots or visually certify jitter removal. |
| All languages | Nine 845-key catalogs checked for references/placeholders; not native-speaker quality certification. Some multiplayer replies are formatted in server language. |
| Local mailbox / readable behavior feedback | Opt-in local files; no auto-upload or automatic behavior training. Remote clients do not record every server-authoritative action, and failed writes have finding 8. |
| Wall deconstruction / trap warnings | Open, not silently added or promised. |

The public-facing README currently labels its release-specific 826-key statement as 0.19.3; the local candidate has 845 keys. That is historical release text, not proof that the candidate lacks catalog keys. Keep candidate, installed and published versions distinct instead of replacing every historical version number. This review did not recheck live GitHub or Steam status.

## Coverage and Evidence

All **66 production C# files** were read and their relevant entry points/calls traced: `Soulmates.cs` plus 65 files under `Common` and `Content`. Supporting project/build metadata, nine localization catalogs, test harnesses, existing review/repair reports, README and description were checked. The supplied conversation was used as the requirement baseline; this does not claim access to missing assistant responses or unrelated chats.

Coverage groups:

- Packet dispatch, profile identity, request/response serialization, authoritative client/server routing and inventory synchronization.
- Profile save/load/clone/binary transport, cargo capacities and stacking, wallet accounting, mining preferences, memory and resident relationships.
- Creation, reusable Soulcore/Sigils/trinkets, summoning, recall, active binding, replacement lifecycle and projectiles.
- UI ownership and coordinate transforms, creator, Talk, player/companion/context/mining wheels, inventory pages, prompts, mailbox and speech.
- NPC main loop, movement/facing, rendering, energy, guardian/healer support, explicit and automatic work, forestry and mining safety.
- Critter native catches/company/loss observers, insect asset preparation, social emote hooks, question gates, resource use, stories and games.
- Text/layout/localization, feedback privacy and IO, workbench behavior, tests and release description limits.

Tests ran against the **unchanged exact Soulmates 0.19.6 package**, on installed tModLoader **2026.8.3.0** and .NET **8.0.425**. No dependency downloads were required. The API inspection used the installed native binaries, including full-width item prefix/stack serialization.

| Verification in this review | Result | Scope |
| --- | --- | --- |
| Main MSBuild | 0 errors, 0 warnings | Compile, no automatic mod packaging/install. |
| Static/localization/math suite | 47,916 assertions pass; 845 keys, 437 references, nine catalogs | Structural and arithmetic checks, not rendered language QA. |
| Existing exact-package native regression suite | 39,737 assertions, 0 failures | Isolated engine fixtures, not connected multiplayer. |
| Expanded independent probe compiler | 0 errors, 0 warnings | Test-only package, never production content. |
| Expanded independent probe run | 80 expectations: 71 pass, 9 fail; no harness errors | Eight additional reproduced defects; previous 67 safety checks still pass. |
| Creator/mailbox geometry | Ten expectations pass within expanded audit | Measurement-only font, native UI scale clamps; no screenshots. |
| Exact artifact hashes | All three inspected copies match | Candidate, original regression engine and deep-review engine. |
| Git whitespace check | Passed | Existing dirty work preserved; CRLF accepted. |
| Embedded description | 7,487 UTF-8 bytes | Below Steam's 8,000-byte limit; no live description edits. |

The eight reproductions use native handlers/types with in-memory fixtures and disconnected sockets. Some private transitions are reached by reflection. They run before world loading and do not open a listener. The wide-prefix and IO-failure cases are explicitly synthetic. The general suite's deliberately malformed packets emit diagnostics; its final result is zero failures. Optional `.tmod` registry association is denied by the environment.

The expanded probe source requests exit 2 for safety failures, but the observed runner returned 1. Its completed result has nine `DEFECT` lines and no `HARNESS ERROR`; the discrepancy is not a product pass or an unhandled normal-game crash. Inspect the report rather than inferring its category from the numeric status alone.

Exact candidate: [Soulmates-0.19.6.tmod](../outputs/careful-repairs-0.19.6/Soulmates-0.19.6.tmod), **1,498,442 bytes**, SHA256 `A6F4FD32C2FC2EAA9FDA9872ED291BF2DCA75C8EA1D443FABCC8C7EE30AB8BE8`.

Saved results: [expanded audit](../outputs/deep-code-review-0.19.6/Soulmates-audit-checks.txt) and [existing native regression suite](../outputs/deep-code-review-0.19.6/Soulmates-engine-checks.txt). The raw expanded result also remains at `work/deep-review-engine/Soulmates-audit-checks.txt`. Test source and isolation instructions: [SoulmatesAuditProbe](SoulmatesAuditProbe/README.md), especially [ReviewCases.cs](SoulmatesAuditProbe/ReviewCases.cs). No private journal text, real player/world names, positions or saves were copied into these review artifacts.

## Limits and Repair Order

No graphical gameplay, new character/world, fresh screenshots, real host/client latency, full progression, long-session balance or third-party furniture/item compatibility playthrough was performed. All 66 files being reviewed does not mean every execution path has been proved correct. Issues involving energy feel, animation/facing and the entire input experience still require real client observation after repairs.

Repair inventory conflict handling first; then retire obsolete/widened server capabilities, align serialization and bind destructive orders to captured targets. Unify pickup eligibility, retain deferred critter questions, bound feedback retries and migrate learned modded material keys. Add failure-and-success controls around each repaired boundary, then run single-player and connected multiplayer playtests.

Only afterwards consolidate transition checks for jobs, automatic activity, pending questions, visits, Stay, pauses, recovery and defense. Rearranging partial files or disabling large feature groups without evidence would not repair these ownership/expiry defects. Public wrapper cleanup is secondary to item conservation and authority checks. This review makes no new feature roadmap or release promise.
