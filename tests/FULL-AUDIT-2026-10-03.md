# Soulmates 0.19.5: Full Requirements and Code Audit

**Historical baseline:** this report preserves the October 3 findings against 0.19.5. The subsequent 0.19.6 repairs and counterchecks are recorded separately in [REPAIRS-2026-10-04.md](REPAIRS-2026-10-04.md). Statements below describe the audited baseline, not a verdict on that repaired candidate.

## Verdict

**Not an all-clear.** Four reproducible correctness defects remain, demonstrated by five failed safety expectations in an independent native-engine probe. A separate conversation gate can make an otherwise willing companion appear inactive. Existing regression checks pass because they did not cover these cases.

The useful systems are real: the latest local single-player journal contains successful pickup, mining, tree care and a completed critter-company encounter. This is not evidence of a total failure, but neither the journal nor the passing assertion count establishes release readiness.

This is a review, not a gameplay patch or publication. No runtime source, version, installed package, character or world save was changed during this audit. Test-only code and this documentation were added. Earlier uncommitted 0.19.4/0.19.5 changes remain intact.

## Scope and Baseline

- Reviewed the user conversation supplied to this task, current requirements, previous verification reports, current source and local feedback. This does not claim access to every unrelated chat or missing historical assistant response.
- Traced creation, saving, cargo transactions, summon/recall, companion switches, input ownership, context wheels, work, autonomous selection, questions, combat/healing, forestry, critters, social memory, localization and network authority.
- Audited local **0.19.5**, on **tModLoader 2026.8.3.0**. The recorded public baseline is 0.19.3. Live GitHub/Workshop content and moderation status were not rechecked or changed in this pass.
- Verified that the normal client's `Soulmates.tmod` matches the tested package, SHA256 `3C4840C3FB6492624D2D9613315B3BEB400100FB97778F1CD3CF84E62382B16B`, size 1,493,760 bytes. Its enabled list contains only Soulmates, not either test probe.
- No graphical playthrough, screenshot capture, new character/world, connected multiplayer, long-session or full-progression test was performed in this audit.
- Private raw journals, typed feedback, names, exact locations and saves were not copied into repository reports or uploaded.

## Findings

All four correctness findings are **P2**: concrete defects requiring repair, with bounded or conditional triggering circumstances. No P0/P1 is established by this audit. Suggested repairs below are not implemented or promised features.

### 1. Saved units can be erased when a stack maximum shrinks

Location: [CompanionProfile.cs:1014](../Common/CompanionProfile.cs#L1014), [load normalization](../Common/CompanionProfile.cs#L605), [ReconcilePackOnce](../Content/NPCs/SoulboundCompanion.Inventory.cs#L152).

`NormalizePack` clamps each saved stack to the current `item.maxStack` before excess reconciliation can return it. This loses units rather than preserving them in split stacks or recovery storage. Loading and several storage/reconciliation paths reach normalization; cloning itself preserves independent item copies.

Independent reproduction uses a test-only modded resource whose previous maximum was 500 and current maximum is 100. Save 240 units at companion level 10. Native `ItemIO.Load` preserves all 240 units while reporting the new maximum of 100; `CompanionProfile.Load` then leaves only 100. **140 units are lost.**

This is a migration/changed-item-limit defect, not evidence that normal current Gel pickup duplicates or deletes valid stacks. Repair should preserve quantity and item metadata before applying current limits, including full-inventory/full-cargo recovery. Existing reserve reconciliation happens too late to recover these erased units.

### 2. A superseded work answer can authorize a different target

Location: [initiative response sender](../Soulmates.cs#L214), [handler](../Soulmates.cs#L631), [prompt validation](../Common/UI/InitiativePromptSystem.cs#L150).

Work opportunity replies contain only an activity kind and response. They do not carry the companion identity or a unique question identity. UI/server validation accepts a replacement opportunity of the same kind.

Independent reproduction: create an Ask prompt for loose item 10 and capture Always; replace that prompt with a new Ask for item 11; deliver the old answer through the real server packet handler. The replacement is accepted, target 11 becomes active, and saved gathering permission changes from Ask to Always.

The demonstrated case is gathering, not a destructive terrain operation. Mining/forestry share the same reply design. Repair should bind work answers to the captured profile and a unique initiative token, as personal questions already do, then revalidate the original target and expiry.

### 3. Older mutating packet families can affect the wrong companion

Location: [quick-action sender](../Soulmates.cs#L121), [handler](../Soulmates.cs#L522), [pack withdrawal](../Soulmates.cs#L468).

Several older requests resolve whichever companion is active when received instead of carrying the identity originally clicked. This includes quick actions, pack withdrawals, talk, recall, trinkets and direct orders. Sender ownership is checked, but that is different from binding the operation to the original companion.

Independent reproduction captures Pause and a resource withdrawal while A is active with Wood in its first resource slot. Switch to B with Stone in that slot, then deliver the captured packets. **B becomes paused, and B's Stone decreases from 10 to 9.** An identity-bound mining-rule packet for A is rejected, providing a control case.

This is a stale-state/replay boundary test using real handlers, not a connected latency reproduction. It does not establish theft from another player's inventory or item duplication. Repair should bind every mutating request to the captured profile. Cargo actions also need captured item identity or a revision check when list indices compact.

### 4. Material learning depends on local mouse coordinates on the server

Location: [ObserveMiningEnvironment](../Content/NPCs/SoulboundCompanion.Resourcefulness.cs#L42), [learning authority](../Content/NPCs/SoulboundCompanion.Social.cs#L141), [observation packet limitation](../Soulmates.cs#L554).

Clients do not learn materials directly; the authoritative path reads static `Player.tileTargetX/Y`. Those coordinates belong to local input, not the remote owner whose held pickaxe is being observed. The current observation packet supports gathering only and does not supply a validated mining target.

Independent server fixture: the owner uses a Gold Pickaxe beside Dirt. With local target globals at `(0,0)`, Dirt is not learned. Setting those globals to the Dirt tile makes the same observation learn Dirt and pick power 55. General mining insight is a different counter; its existence does not repair per-material learning.

Repair should observe an authoritative mining event or an owner-bound, range/tool-validated target signal. A host's local mouse must not supply another player's target. Connected host/client tests remain necessary.

## Behavioral and Documentation Tensions

### Personal questions can stop new autonomous work

[UpdateHelpfulAutonomy](../Content/NPCs/SoulboundCompanion.Autonomy.cs#L63) immediately returns while a personal question is pending. The native fixture starts a valid Company question, enables Gathering=Always and clears the decision cooldown: no new helpful work starts. The answer window is 3,600 ticks, up to one minute at 60 ticks/second.

Combat, very-close approved pickup and explicit replacement orders are not all blocked. This is not a claim that every action stops. However, optional conversation should not silently prevent already permitted work if the intended behavior is a continuously helpful companion. The broad [README scheduler claim](../README.md#L37) needs clarification: work-kind cooldowns are separate, but a personal question is a global gate for new helpful activity. Decide this behavior before changing it.

### Structural caution

Production cargo insertion is centralized, and reuse of native catch/mining/emote hooks is a useful foundation. Behavior arbitration still spans `activeJob`, retained `Routine`, autonomous activity, pending initiative, personal question, critter visit, resident visit, guardian target and several recovery/cooldown fields. The defects arise at those transitions and ownership boundaries.

Do not begin a broad rewrite as a substitute for repairing the reproduced cases. Afterwards, consolidate transition validation and immutable captured interaction identities, with focused tests for interruption, replacement and resume. Purely moving partial classes or changing names would not solve these problems.

### Feedback is useful but incomplete in multiplayer

Field-note sessions are local and opt-in; dedicated servers do not start them. Several detailed action events are recorded only on authority. A remote client's journal therefore cannot be treated as a complete transcript of server-side pickup, forestry or social decisions. Any future diagnostics should retain explicit consent and local-only defaults.

## Historical Requirements Matrix

"Implemented" below means a production path exists and relevant code/engine coverage was reviewed. It does not mean every graphical/progression/network scenario was played. "Partial" distinguishes bounded behavior from the larger original wish. "Open" means not implemented, not a commitment to build it.

| Conversation requirement | Current state | Boundary or relevant evidence |
| --- | --- | --- |
| Reusable Soulcore creates individual companion items | Implemented | One transactional creation path; separate Blank Sigil input and bound output; full-inventory failures covered. |
| See and customize the companion before creating it | Implemented, preset-based | Animated preview, form/color/aura/personality/talent. Name selection is nine presets, not free-form typing. |
| Keep the custom Soulkin sprite | Implemented | Custom idle/action frames remain; this audit generated no artwork. |
| Select creatures from Terraria's Bestiary | Partial | Soulkin plus Bunny, Blue Slime, Bird and Squirrel visuals, not the full Bestiary. Native sprites animate; they do not give the companion each native creature's AI. |
| Exactly one companion; switching stops previous abilities | Implemented with network gap | Recall retires its projectiles, work and followers; old mutating requests remain unbound, finding 3. |
| Stop idle left/right jitter and excessive animation | Implemented controls | Facing hysteresis and slower idle frames; live obstacle/combat movement still needs visual checking. |
| Visible defense instead of one short fly-by | Implemented | Leashed Soul Bolt attacks and critter protection; current player journal records attacks. Not an unrestricted native weapon user. |
| AETHER can heal and fight | Implemented automatically | All talent instincts/perks; guardian and healer checks coexist. No dedicated manual heal-versus-fight stance selector. |
| Fair energy and passive recovery | Implemented, balance not certified | Lower work effort cost, follow/work recovery and exhaustion reserve; combat pauses recovery. Long-session balance is still a playtest question. |
| Level, bond, mood and deterministic refusal | Implemented | 20 levels, five bond ranks, stated energy/mood thresholds and cooldowns; no claimed general intelligence. |
| Resources scale to 500 at level 10 and 1,000 at 20 | Implemented | 50 per level per item type, 60 resource slots, native stack limits, paged UI. Saved-stack migration defect remains, finding 1. |
| Separate unlimited money bag | Implemented | Exact uncapped BigInteger copper balance; bounded native coin withdrawals; no cargo slots used. |
| Collect mined drops and nearby loose items | Implemented | Shared verified world-to-cargo transfer, ownership checks and limits; no normal pickup duplication reproduced here. |
| Use everything carried | Partial | Specific food/recovery/light use and weapon-derived attack bonus. Not arbitrary item execution, ammunition, accessories or native pet summons. |
| Crafting suggestions | Implemented, bounded | Nearby native recipe checks inspired by carried ingredients; suggestions are not autonomous unrestricted crafting. |
| Ore work finishes an area and reports completion | Implemented, bounded | Planned local area assignments and completion feedback. Direct/automatic bursts use 24-target caps; not unlimited vein clearing. |
| Dig to the next opening / mining modes | Implemented, bounded | Adaptive, Tunnel, Vein and Surface approaches; safety/tool checks remain. |
| Nearby ore choices unfold from mining | Implemented | Read-only survey and native item-icon family selection. |
| Configure automatic ore/block mining in an orb menu | Implemented | Persisted per-type rules, paging/all-type choices; observed terrain opt-in, direct orders bypass only automatic filters. |
| Preserve dirt supporting saplings and decorations | Implemented | Support-neighbour checks on selection and execution; this is deliberately conservative, not arbitrary demolition. |
| Learn local materials and reuse observed tools | Partial and defective in server context | Saved material list is bounded to 48; current remote material observation fails, finding 4. |
| Adapt to the owner's habits | Implemented, rule-based | Gathering/mining/forestry/combat/exploration insights and bounded imitation cues, not arbitrary learned procedures. |
| Shake trees, collect seeds, plant and prune dry branches | Implemented for supported families | Native shaking, fallen logs, bare vanilla side-branch pruning and acorn placement; native per-day limits and failure cooldowns remain. Leafy branches, stems and support terrain stay protected. |
| Forestry is a perk; AETHER has it | Implemented | Learned Forester perk rather than a sixth starting class; AETHER includes it. |
| Companion notices opportunities; emote Yes/No/Always/Never | Implemented with reply defect | Separate work permissions and prompt UI, but same-kind stale replies can authorize replacements, finding 2. |
| Stop, resume and abort current work | Implemented with network gap | Pause retains work/visit; Resume continues; Abort cancels. Retained generic routines differ from direct target snapshots. |
| True Stay position and attacks originating from her | Implemented | World anchor and bounded defense; production coordinate tests exist, visual checks still needed. |
| Terraria default input must keep items/furniture usable | Implemented routing | Captured UI/native alternate-use/cursor-held items take priority; placed torches/furniture priority and workbench inventory QoL covered. Not every modded furniture tested. |
| Mode wheel: Terraria / Me / Soulmate | Implemented | Captured context target, deliberate mode selection/cycling and exit; fallback only for supported nearby world targets. |
| Separate player and companion radial wheels | Implemented | Player Emotes/Point/Mailbox; companion work/cargo/settings/details. Player wheel currently requires an active companion. |
| Native symbols, correct Back icon, little custom UI | Implemented direction | Native item/emote/navigation assets used broadly; still contains custom Soulkin art and framed creator/details/mailbox. |
| All native emotes, with item subcategories | Implemented | 151 vanilla player emotes; item-symbol subgroups and separate cargo-topic classification. Every third-party emote is not promised. |
| NPC emotes trigger appropriate responses and relationships | Implemented, bounded | Native bubble observer, one bounded reply per visit, resident affinity and world-scoped memories. Not a general dialogue model. |
| Personal questions can be answered and change character | Implemented | Four native-symbol answers, saved voice/preferences, mood/bond and question GUID. Unanswered-question work gate remains. |
| Less text, combinations of meaningful emotes | Implemented as sequences | Up to three native symbols in three-second beats, not several simultaneous native bubbles; explicit interaction can replace a sequence. |
| Longer speech from the companion, fading trail | Implemented in code | Name/anchor/tail, length-based 9-25 second lifetime and ending fade. No new screenshot verification in this audit. |
| More stories and "Do you remember?" | Implemented, template-based | Item topics, environment observations and bounded remembered episodes. Not unrestricted invented narratives or verbatim NPC-chat memory. |
| Remember what the player pointed at or emoted about | Partial | Shared-moment and found-item memories exist; not reliable exact context-linked memory for every native emote and target combination. |
| React when critters die; apologize/disapprove | Implemented with witness/rate limits | Nearby line of sight, native player attack credit, queued grief and personality variants. No death in the latest reviewed journal proves or disproves actual reaction completeness. |
| Greet, approach and invite critters | Implemented, limited company set | Watch/Company/Collect/Off; nine common native types can join. Directed visits are time-bounded and fail on inaccessible targets. |
| Catch with a real net, respect Terraria logic | Implemented | Native catch checks, lava-tool restrictions, real cargo capacity; gold/statue/released protections. Latest local mode rejection without a carried net is expected. |
| Critters follow and AETHER has an insect entourage | Implemented, not a combat army | One ordinary/three AETHER temporary real critter friends; up to six cosmetic carried-insect sprites. No permanent new pets or fighting swarm. |
| Carry and summon her own vanilla pet alongside mine | Open | Pet items may be cargo; companion-owned vanity-pet summoning is not implemented. |
| Rock-paper-scissors | Implemented | Native emotes, real rounds, results and memory; bounded engine coverage. |
| Nine Terraria languages; no exposed localization keys | Catalogs implemented | 845 keys per catalog with placeholder/source checks. Seven newer translations still need native-speaker review; some multiplayer replies use server language. |
| Local gameplay mailbox and bug reports | Implemented | Opt-in local notes/events; text input, bounded session log, contextual bug reports; no automatic upload or automatic new behavior trained from notes. |
| Learn wall deconstruction and warn about traps | Open | Recorded wishes; neither implemented, and neither silently added by this review. |
| Current genuine screenshots/video, no fabricated captures | Existing material only | Current gallery uses player screenshots and unchanged October 1 video frames, not fresh 0.19.5 screens. |
| Accurate release/version/Steam descriptions | Locally prepared, not published here | Package/client both 0.19.5; recorded public baseline 0.19.3. Current upload changelog is separate from full history; no live page edits today. |
| Respect collaborators without redirecting the project | Documentation/social intent | Asset disclosure remains; a procedural sprite-parts system was a forum suggestion, not an implemented or approved roadmap. |

Forum replies, promotional posts and the requested song are creative/publication work, not gameplay code requirements. This review does not post, approve terms, upload files, remove media or change the agreed project direction.

## Verification Performed

| Check | Actual result | Limit |
| --- | --- | --- |
| Main MSBuild | 0 warnings/errors | Compilation only; no repackaging/install. |
| Existing regression probe MSBuild | 0 warnings/errors | Test assembly compilation. |
| Static/localization/math run | 47,915 assertions passed | Nine catalogs, 845 keys, 436 references; not rendered language QA. |
| Existing exact-package native-engine run | 39,737 assertions, 0 failures | Isolated bounded fixtures, not a connected multiplayer session. |
| Independent audit probe native build | 0 warnings/errors | Test-only content excluded from production. |
| Final independent audit probe | 21 safety expectations: 16 passed, 5 failed | Four confirmed defects; pending-personal-question gate logged separately. Returns nonzero for failed expectations. |
| Creator/mailbox geometry | 10 expectations passed | Native layout with measurement-only font, including action bounds. Physical sizes 800x600, 1280x720 and 1920x1080; effective native UI scales 0.85, 1, 1.2, 1.5 and 1.8. Not screenshots. |
| Normal-client package identity | Exact hash match; only Soulmates enabled | Read-only check; the client was not launched or updated. |

Native UI scaling is clamped by Terraria's real-screen width/height. Requested 150% at 1280x720 becomes 120%; requested 300% at 1920x1080 becomes 180%. Reporting only requested scales would overstate the tested geometry. An earlier geometry suspicion was rejected after correcting the fixture and checking effective scales.

The independent probe invokes real production packet handlers with disconnected sockets and native saved-item loading. It does not open a listener, load a world or touch normal saves. Some setup/selection calls use reflection to reach private transitions; that is controlled fixture injection, not a real user's mouse sequence.

Raw independent results stay locally in `work/full-audit-engine/Soulmates-audit-checks.txt`. Reproducible test source and isolation instructions are in [SoulmatesAuditProbe](SoulmatesAuditProbe/README.md). Existing regression assertions are unchanged; their project adds only an exclusion for the new native-only audit folder. Passing them does not override the independent failures.

## Next Decision

Repair saved-unit conservation and captured operation/question identities before another release; then fix authoritative material observation. Decide whether personal questions should be nonblocking for already approved work. Validate the repaired cases plus a real single-player session and a connected host/client session, particularly cargo compaction, switching, prompt replacement and native input.

Do not advertise open wishes as existing functionality or equate a large assertion count with complete gameplay verification. This audit adds no new feature roadmap and performs no publication.
