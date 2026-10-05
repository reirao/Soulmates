# Soulmates 0.19.7: Full Review and Native Counterexamples

**Historical baseline:** the findings below belong to the unchanged 0.19.7 package. [The subsequent 0.19.8 repair goal and repeated recheck](NATIVE-REPAIRS-0.19.8-2026-10-04.md) records their repairs. Line references belong to the reviewed snapshot, not the later source.

## Verdict

**Not an all-clear.** This fresh review covers all **66 production C# files, 14,560 lines**, and the supplied conversation's requirements. It found **five reproduced correctness problems**, demonstrated by **seven failed expectations**, plus one source-confirmed modded-resident persistence risk. Existing repair counterchecks still pass, but do not cover these additional boundaries.

The exact unchanged 0.19.7 package was checked with the installed tModLoader 2026.8.3.0 and .NET 8.0.425. Only test-only fixtures and review documentation were edited. Production code, normal-client installation, player/world saves, Git history, GitHub and Steam were not changed. No new graphical playthrough, connected multiplayer session or screenshots are claimed.

## Findings

P2 denotes conditional correctness problems requiring repair; P3 denotes the lower-priority compatibility issue. Reproduction uses real native methods and production handlers with isolated in-memory state, not a connected gameplay session.

### 1. [P2] NPC-emote events mutate a frozen profile and rejection loses legitimate social history

Locations: [ObserveResidentEmote](../Content/NPCs/SoulboundCompanion.Social.cs#L337), [relationship mutation](../Content/NPCs/SoulboundCompanion.Social.cs#L355), [inventory rollback](../Common/CompanionInventorySync.cs#L173).

Authoritative AI waits while an owner inventory transaction is pending. Victory and critter-loss events are deferred too. The native resident-emote callback runs outside that AI gate, however, and immediately calls `MeetResident` and `SyncPackState`. It has no corresponding pending-transaction guard or deferred event.

Reproduction: start a ten-Wood withdrawal and leave its acknowledgement pending; a nearby Guide emits a native wink; deliver a rejected inventory receipt. The emote creates one relationship while the profile is supposed to be frozen. Rejection then replaces the entire profile with `BeforeProfile.Clone()`: the relationship disappears. Wood is conserved in this fixture; the demonstrated loss is legitimate relationship/history state, not items.

Defer the observation with validated resident identity until settlement, or reconcile only transaction-owned state instead of overwriting unrelated changes. Add both accepted- and rejected-transfer event-order controls. Silently ignoring the event would avoid mutation but would not preserve the interaction.

### 2. [P2] Companion mining disagrees with Terraria's native progression checks

Locations: [observed target eligibility](../Content/NPCs/SoulboundCompanion.Resourcefulness.cs#L54), [work eligibility](../Content/NPCs/SoulboundCompanion.Resourcefulness.cs#L93), [hardcoded thresholds](../Content/NPCs/SoulboundCompanion.Resourcefulness.cs#L120), [destructive execution](../Content/NPCs/SoulboundCompanion.Work.cs#L393).

`RequiredPickPower` is an incomplete approximation of native `Player.GetPickaxeDamage`. Calling `TileLoader.PickPowerCheck` separately does not reproduce the preceding vanilla coordinate/progression checks. Observation occurs before native `PickTile` completes, so an attempted but impossible mine can teach a material.

Two native comparisons fail:

- A copper pickaxe at an underground outer-world Dungeon brick has native pickaxe damage zero. The companion nevertheless learns the material and regards it as a valid mining target.
- A gold pickaxe has positive native damage against Obsidian at pick power 55. The companion rejects the observation because its table requires 65.

The Dungeon fixture proves incorrect learning and eligibility. Actual destruction of a Dungeon brick was not executed in this probe; progression bypass during work follows from the source path that calls `WorldGen.KillTile` after the same insufficient eligibility gate. This distinction matters.

Use one eligibility policy consistent with the installed native rules, including location-sensitive cases, for observation, target selection and execution. Keep mod hooks and decoration/protected-tile checks; do not substitute a mod-only hook for vanilla progression.

### 3. [P2] Automatic pack consumables bypass ModPlayer use restrictions

Location: [TryUsePackConsumable](../Content/NPCs/SoulboundCompanion.Resourcefulness.cs#L216).

The method calls `ItemLoader.CanUseItem`, which checks item hooks, but not the combined player-and-item gate. Native `CombinedHooks.CanUseItem` also checks `PlayerLoader.CanUseItem`.

Reproduction uses a test-only `ModPlayer` that rejects healing items. The native combined gate returns false. The same potion in the companion's pack is consumed anyway and heals the owner from 20 to 70 life. This demonstrates incompatibility with mods imposing player-specific use restrictions; it is not a claim that ordinary vanilla healing is always blocked.

Respect the combined use gate before effects or consumption. Separately review use-versus-consumption semantics: a false consume hook need not mean an item cannot be used. That additional case is source-level follow-up, not an additional reproduced defect in this report.

### 4. [P2] Pack healing ignores native item/accessory potion-delay behavior

Location: [fixed PotionSickness duration](../Content/NPCs/SoulboundCompanion.Resourcefulness.cs#L224).

Healing applies the global `Item.potionDelay` directly instead of the owner's native potion-delay path. This omits the owner's `PotionDelayModifier`, per-item special cases and delay hooks; it also does not reproduce native `owner.potionDelay` assignment.

Reproduction: set the native owner's delay modifier to 0.75 and use a Lesser Healing Potion. Native `ApplyPotionDelay` produces 2,700 ticks. Companion use produces 3,600 ticks for the same item and owner. Both controls actually use the installed native player/buff types.

Preserve native item-specific and player-specific delay behavior when administering a carried potion, and synchronize the resulting authoritative buff state. Avoid another independent hardcoded approximation.

### 5. [P2] Multiplayer rejects a valid planting order aimed at empty space

Locations: [server captured-tile guard](../Soulmates.cs#L758), [forest target resolution](../Content/NPCs/SoulboundCompanion.Work.cs#L50).

The new captured-terrain guard rejects every Mine/Forest request whose clicked tile has no tile. That is appropriate for mining but contradicts `TryResolveForestTarget`, which accepts a valid empty planting position above suitable ground. The sender uses `expectedTile=-1` for that unchanged empty position.

Reproduction: with two acorns and suitable grass ground, the single-player direct order accepts the empty planting position. The same production packet delivered in server mode returns no job. Clicking the occupied grass beneath it starts the multiplayer planting job, confirming that this is the empty-target guard rather than missing supplies or general forestry eligibility.

Validate both captured presence and type. Allow an unchanged empty target for planting while continuing to reject replaced mine/prune targets. Existing replaced-ore rejection counterchecks must remain passing.

### 6. [P3, source-confirmed] Modded resident relationships persist unstable numeric NPC IDs

Locations: [relationship save/load](../Common/CompanionRelationships.cs#L32), [lookup](../Common/CompanionRelationships.cs#L65).

Relationships save `NpcType` as a number and use `(world GUID, numeric type, resident name)` as identity. Modded NPC IDs can change when the enabled content set changes. The same resident can then fail lookup and acquire a separate new relationship; the numeric identity can also refer to unrelated content after remapping. Existing stable mining-material keys solve this problem for tiles, not residents.

This is established from persistence and lookup code, not a real two-mod-set world reload. Vanilla NPC IDs are not the issue. Persist a stable mod-content identity with a conservative migration/missing-content policy, retaining runtime IDs only for the current session.

## Menu Wiring, Dead Paths and Design Questions

Creator binding, Talk/Details, item categories, cargo/wallet pages, rule toggles, context orders, question answers, critter modes and Pause/Resume/Abort have production handlers. The reported failures primarily occur after those handlers, at authority, eligibility and event-order boundaries. No missing callback was established for these main visible controls.

Source references and compiled-IL references were checked together. Native hook overrides, generated record accessors, enum metadata, serializer-facing properties and public/test APIs were not automatically classified as dead code.

| Candidate | Current status | Treatment |
| --- | --- | --- |
| `HandleTrinketRequest` equipped-message branch, Soulmates.cs:510 | Unreachable: the preceding guard permits only `None` | Remove the redundant ternary arm in a later cleanup; not a gameplay failure. |
| `CommandName`, `CurrentJobRadius` | Public getters without production callers | Retain only if intended diagnostic/external APIs; otherwise retire deliberately. |
| `CurrentJob` | Used by native probes, not production UI | Purposeful test API, not automatically dead. |
| `ToggleCommand` | Unused public wrapper; supported routes use explicit commands | Decide compatibility policy before removal. |
| `WithdrawPackSlot` | Unused public legacy wrapper; current UI specifies storage kind | Retire or document compatibility. |
| `LatestMemory`, `RecallMemory` | No production callers; current story path is `TellRememberedStory` | Consolidation candidate, not proof that storytelling is unwired. |
| `CompanionProfile.CanStore` | Unused public convenience method; transfers use amount-aware capacity | Optional cleanup. |
| `OpenContext(companion, worldMouse, uiMouse)` | Unused overload; production passes a captured `SoulwheelTarget` | Prefer captured identity; retire the convenience overload if unsupported. |
| Reserved `NativeEmoteRequest` enum entry | Intentionally inert obsolete protocol ordinal | Do not renumber/remove it in a way that shifts supported packet IDs. |

No consequential unreferenced private production method or write-only state field was established after filtering the compiled reference scan. Broad imports across partial files are cleanup noise, not an explanation for the reproduced bugs.

**Balance question, not a certified defect:** ordinary Talk praise/trust and Follow apply bond rewards on each call without the native-emote reward cooldown. This lets repeated conversation clicks raise bond quickly, unlike the cooldown-limited gesture route. Decide whether that is intended before introducing a new rule. Long-session energy pacing, repeated questions and combat strength also require gameplay measurement, not arbitrary threshold changes during a review.

## Requirements Versus Current Implementation

The supplied conversation is the requirement baseline, including wishes that were never fully implemented. It does not provide missing assistant messages or establish that every requested future idea is a shipped promise.

| Request | Current state and boundary |
| --- | --- |
| Reusable Soulcore and individual equipable companions | Implemented creator/binding/summon/recall systems, with independent profiles. Creator offers nine preset names, not free-form naming. |
| Build appearance and choose creatures | Custom Soulkin plus Bunny, Blue Slime, Bird and Squirrel visual families. Not full Bestiary selection or adoption of every native pet AI. |
| Growth, XP, personality, energy and bond | Implemented systems. Whole progression and long-session balance are not certified. Talk reward consistency remains a design question above. |
| Unlimited money plus level-scaled resources | BigInteger wallet; resource limit is **per item type**, 50 units per level, 500 at level 10 and 1,000 at level 20. There are 60 resource stack slots, not a single total weight limit of 1,000. |
| Equipment inventory | Eight ordinary slots, rising to ten through bond ranks; Hearth Ribbon allows twelve. Separate from resource storage and wallet. |
| Autonomous helpful work, questions and explicit orders | Real rule-based work with Ask/Always/Never permissions, bounded plans, leashes and cooldowns. Four token-bound answer choices exist; personal questions do not generally block Always-approved work. |
| Mine selected ores and learned blocks | Rule menus and a 48-material knowledge limit exist; destructive bursts are bounded to 24 targets. Native progression consistency is finding 2. |
| Learn player behavior and surroundings | Bounded insights and observed mining materials, not arbitrary learned programs, a language model or automatic training from local field notes. |
| Trees, dead branches, supplies and planting | Native shaking and fallen logs are available without the Forester perk. Forester/AETHER/Gatherer or sufficient forestry insight unlocks automatic pruning/planting and expands sweeps. Only supported dry side-twig frames are pruned; living stems, leafy branches and unsupported families remain protected. Empty-target multiplayer planting is finding 5. |
| Use found/carried items | Specific healing, mana, food, torch light and a weapon-derived damage bonus. Not arbitrary item execution, native weapon/ammo/accessory simulation or vanity-pet summoning. Native item-use compatibility has findings 3 and 4. |
| Companion's own additional vanilla pet | Still open. Carrying a pet item is not the same as summoning its pet. |
| Critter greetings, company, catching and death reactions | Real native hooks and bounded supported behavior. One ordinary or three AETHER temporary friends; catching requires an available suitable net. Up to six carried-insect sprites are cosmetic, not a fighting army or permanent saved NPC entourage. Earlier deferred-care counterchecks still pass. |
| Separate self/companion/context wheels | Implemented, with native input ownership. The player wheel still requires an active companion; it is not an independent always-available emote replacement. |
| Native emote categories and combined expressions | All 151 vanilla emotes and item subcategories are present. Combinations are timed sequences, not arbitrary simultaneous bubbles. Automatically enumerating arbitrary modded emotes is not implemented. |
| Resident relationships and contextual stories | Up to 24 world-scoped relationships and eight bounded memories feed template-based stories. Not free-form stories for every item/context. Resident event-order and mod-content identity have findings 1 and 6. |
| Rock-paper-scissors | Independent rounds and native symbols/personality replies; no XP or persisted round memory. |
| Longer companion-origin floating speech | Length-based lifetime, world anchor/tail and fading exist. This review does not visually certify absence of jitter or overlaps. |
| Languages | Nine 845-key catalogs pass structural/reference/placeholder checks. This is not native-speaker quality certification; some multiplayer replies use server language. |
| Local mailbox and readable play feedback | Opt-in local files with no automatic upload or behavior training. Typed notes have a durable inbox; automatic write retries are bounded. Remote clients do not observe every server-authoritative action. |
| Wall deconstruction / trap warnings | Open; no new roadmap or completion claim is introduced here. |

The historical matrix overstated a general Forester gate and persisted rock-paper-scissors memories. The distinctions above follow the current code. Release-specific README statements are historical release descriptions, not evidence of the local candidate's installation or publication state.

## Verification

| Check in this review | Result | Meaning |
| --- | --- | --- |
| Production MSBuild | Zero errors and warnings | Compile check without automatic mod installation. |
| Static/localization/math suite | 47,922 assertions pass; 845 keys, 443 references, nine catalogs | Structural and arithmetic checks, not graphics or translation-quality certification. |
| Existing general native suite | 39,738 assertions, zero failures | Same exact unchanged production package; includes disconnected packet replay. |
| Expanded independent probe compilation | Zero errors and warnings | Test-only content, excluded from production. |
| Expanded independent probe | **109 expectations: 102 pass, 7 fail; no harness errors** | All previous 96 expectations still pass; 13 additional expectations expose five new problem fields. |
| Creator/mailbox geometry | Ten expectations pass within the independent probe | Native UI-scale clamps and measurement-only font; no rendered screenshots. |
| Candidate and both isolated test copies | SHA256 identical | No accidental test of another production version. |
| Description | 7,733 UTF-8 bytes | Below 8,000; no live Steam edit. |
| Git whitespace | Pass with CRLF allowed | Existing dirty work preserved. |

Exact candidate: [Soulmates-0.19.7.tmod](../outputs/complete-repairs-0.19.7/Soulmates-0.19.7.tmod), **1,517,108 bytes**, SHA256 `CE0AB6136B91B3DB1BA0243626F8661AD634D4E9CD5A85773F7D0000C9104A13`.

Saved results: [expanded independent audit](../outputs/full-review-0.19.7/Soulmates-audit-checks.txt), [general native suite](../outputs/full-review-0.19.7/Soulmates-engine-checks.txt), [build](../outputs/full-review-0.19.7/build-check.txt), [static checks](../outputs/full-review-0.19.7/static-checks.txt) and [native API inspection](../outputs/full-review-0.19.7/native-boundaries.txt). Reproduction source: [SecondReviewCases.cs](SoulmatesAuditProbe/SecondReviewCases.cs). See [probe isolation instructions](SoulmatesAuditProbe/README.md).

The expanded probe requests exit 2 for reproduced failures, but the runner returns 1. Its completed report contains seven `DEFECT` lines and no `HARNESS ERROR`. This is a failing product-boundary audit, not evidence of an unhandled normal-game crash. Earlier fixture initialization/compile issues were repaired before the final run; they are not product findings.

Both native probes terminate before world loading. They use synthetic player/NPC/item/tile fixtures, disconnected sockets and some private-method reflection. A test-only ModPlayer models a valid use restriction; a temporary world GUID models resident identity. No real player/world save, journal text or private gameplay record was exported. Deliberately malformed packet checks and controlled IO failure emit expected diagnostics. Optional .tmod registry association is denied by the environment and does not prevent the tests.

## Coverage and Limits

Every production source file was read and its relevant entry points/callers traced: `Soulmates.cs` plus the 65 `Common`/`Content` files. Supporting project/build metadata, catalogs, existing test/review documents and public-facing text were checked too. Source coverage is not execution coverage.

- Protocol dispatch, ownership/GUIDs, serialization, transfer preimages, acknowledgement, conflicts, retry and deferred responses.
- Profile persistence/clone/network transport, item conservation/capacities, wallet, learned material keys, resident identity and memory.
- Creator, reusable Soulcore/Sigils/trinkets, lifecycle/replacement, current binding and owned projectiles.
- UI input/coordinate ownership, creator, Talk, mailbox, questions, cargo, player/companion/context/mining wheels and speech.
- NPC state priority, Pause/Resume/Abort, Stay, movement/facing, rendering, energy, guardian/healer, manual and automatic jobs.
- Forestry/material protection, resource use, native critter catches/company/loss, insects, resident emotes, dialogue, stories and games.
- Text/layout/localization, local-feedback privacy and write failure behavior, workbench interactions and packaging exclusions.

No connected host/client latency/disconnect/reconnect test, new graphical client playthrough, full progression, long-session balance, arbitrary third-party item/furniture compatibility or real changed-mod-set world reload was performed. Main-menu geometry initialization is not a screenshot test. Rendering/facing glitches and the entire right-click feel remain client-observation work after repairs.

This review does not establish a new pickup-duplication defect, missing creator callback or unrepaired old inventory conflict. It establishes the new boundaries above. Retain the existing conservation, identity and old-repair controls rather than disabling whole feature groups to make tests pass.

Repair order: isolate pending-profile social events, align mining with native rules, respect native item-use/delay behavior, then correct empty-target multiplayer planting. Stable modded resident identity is next. Public API cleanup and balancing follow explicit decisions, not assumptions. Re-run both failure and valid-operation controls, then test actual single-player and connected multiplayer behavior before claiming a releasable build.
