# Soulmates 0.25.0: A Place to Belong

Installation follow-up, October 11, 2026: the normal client now has this exact
package. See [the separate installation receipt](CLIENT-INSTALL-0.25.0-2026-10-11.md).
The preparation record below describes the earlier build and isolated checks;
installation does not establish a graphical playtest or public publication.

Local beta candidate, October 11, 2026. Based on the existing 0.24.0 checkout.
This pass does not install the normal client, publish to GitHub or Steam, or
claim a new played world or screenshot set. The shared desktop and player
saves were left alone. All peers testing the new network profile need 0.25.0.

## Scope

The requested refinement is integrated into existing profiles, observations,
task priorities, conversations and wheels. There is no separate AI executor,
online model, new control window or autonomous construction system.

### Grounded Place Memory

- A read-only, bounded room query considers enclosure, manufactured walls,
  flooring and useful furniture. It does not call the mutating global native
  housing checker. Selected-bed evidence requires the actual usable bed;
  resident evidence uses an active town resident's assigned home anchor, not
  where the NPC happens to be standing or the companion's own housing fields.
- Queries inspect at most 640 tiles within a local 41-by-25 search envelope.
  This is conservative shelter evidence, not full Terraria housing validation
  or universal recognition of large, open, irregular or modded builds.
- Eight places per Sigil, scoped by world identity, retain at most 32 visits.
  The first entry counts once. Standing still does not add visits; leaving for
  approximately twelve seconds between sampled returns does. A familiar
  shelter requires three visits. Adjacent rooms do not inherit ownership.
- Familiarity is not ownership. The existing personal question wheel offers
  Our home, Your little home, Just a stop, and Later. Assigned resident housing
  is not offered for claiming. Quiet disables personal questions; normal
  Chatty/Calm spacing still applies, so this is not an immediate popup.
- Answers bind to the captured world, room and question token. A demolished
  room, changed world or replay cannot assign a home role. No home answer
  awards XP, bond, mood, money or cargo. Later leaves the role unchanged.
- The canonical room anchor is a deterministic traversable room cell, even
  when a pillar occupies the bounding-box center. Observations distinguish
  tentative shelter, familiar shelter, bed/residence and confirmed homes.
- Automatic treasure inspection excludes recognized home chests. Explicit
  treasure searches remain available. Distance is checked before room scans;
  a bounded cache evicts one entry and invalidates on world changes.
- Visible damaging threats take precedence over friendly creatures and home
  commentary. These inferences affect existing observations and treasure
  relevance; they do not grant ownership, permissions or building capabilities.

### Optional EVIL Persona

EVIL starts off and persists separately per Sigil. Commands and the fourth
Details task button, using a native laughing-face symbol, toggle it. Safe idle
lines become theatrical schemes; actual completed work can queue one small
celebration. Rock, paper, scissors quips follow the existing fair outcome.

Celebrations expire, have a two-minute cooldown and wait behind readable
speech, questions, consent, threats, work, resident visits and critter reactions.
Pause, Abort, Stay and disabling autonomy prevent stale celebrations. Toggling
during a question preserves its text, token and native expression in the
production wheel and quick-response handlers.

EVIL changes expression only. It does not change task selection, damage,
terrain, cargo, rewards, consent or empathy for harmed creatures. It does not
invent successful work or add an automatic ability.

### Existing Behavior Repairs

- Native input owns inventory, typing, NPC dialogue, cursor-held items and
  normal Terraria item actions. Rejected opens preserve the current interface.
  Captured Sigil and target identities are revalidated before dispatch.
- Wheel and answer nodes fade in their final positions, matching hit tests.
  Requested UI scale is distinct from Terraria's applied viewport-limited
  scale; tests now exercise the latter, including actual 2.0 at 2560x1440.
- Recovery includes mood hysteresis and recovers mood while following when
  needed. Work timeouts precede forestry/pause branches. Removed treasure
  targets cannot complete successfully; low-energy mining settles once.
  Real threats take priority over a retained training dummy.
- Critter targets, pending visits and greetings validate live native slots.
  SetDefaults invalidates references on observed same-object respawns.
  Revoked automatic permission and recovery cannot execute stale visits.
  Catching is not death; multiplayer clients do not finish authoritative visits.
- Familiar selection checks the selected real pet slot. Invalid, duplicate or
  old-Sigil familiars are removed, and unresolved binding/inventory receipts
  cannot draw a stale familiar. Existing custom movement and native art remain.

## Verification

Bundled .NET SDK 8.0.425; native tModLoader 2026.8.3.0/Terraria 1.4.4 libraries.
No normal-client save directory was used for the probes.

| Check | Result | Scope |
| --- | --- | --- |
| Production MSBuild | 0 warnings, 0 errors | Current production assembly and analyzers |
| Native package compiler | 0 warnings, 0 errors | Current production assembly |
| Native engine regression | 96,492 assertions, 0 failures | Existing coverage plus persona, homes, work, critters, input and production UI handlers |
| Independent audit | 490 expectations, 0 failures | Bounded cargo ledgers, huge exact wallet, authority, consent and stale/replayed requests |
| Static/localization | 58,211 assertions passed | 991 keys in 9 catalogs, 546 source references, layout/controller checks |
| Package identity | Passed: 86 matching sources, 114 entries | Native payload, source hashes and private-content exclusion |

Counts are assertions, not separate features or exhaustive correctness proof.
The regression probe retains 33 pre-existing nullable/Hjson reference warnings;
the production builds and independent audit compiler are clean. Malformed-wire,
blocked-write and restricted registry-association diagnostics in the isolated
probes are expected negative tests, not silently ignored client crashes.

### Failure And Recheck History

The first production compile exposed missing native-emote namespace imports
and nullable flow annotations. The first new probe compile also exposed private
native test APIs and inaccessible storage helpers. Both were repaired.

A first executed regression stopped at 60,386 assertions with 52 failures,
including incorrect requested-scale coordinates, a detached cloned Sigil in the
character fixture and an uncaught InvalidDataException in a negative wire test.
The corrected pass reached 96,466 assertions with six failures: an older radial
reply fixture was trying to answer while Main.gameMenu was still true. Its
gameplay state is now explicit and restored; protected-input guards were kept.
The complete rerun passed all 96,466 assertions.

A second source counter-review then found query-position-dependent protection
for a chest at the edge of a wide room. Room extents now bound the flood fill,
not distance from its starting tile. A 30-by-6 room and all three classified
home roles have additional counterchecks. The final complete regression passes
96,492 assertions; production builds, static checks and the independent audit
were repeated against the repaired source and package.

Independent source counter-review found three production defects: a blocked
room-center anchor, room scans before chest-distance filtering, and EVIL status
overwriting pinned question speech. All three were repaired and exercised by
native counterchecks. A dynamically referenced Home heading was missing despite
catalog parity; it was added in all nine languages, and every question's heading
and four choices now have native lookup checks. Production analyzer warnings
about native tile/wall constants were also repaired, not suppressed.

### Exact Local Package

Local output: `outputs/beta-depth-0.25.0/Soulmates.tmod`.
Native header: Soulmates 0.25.0, loader 2026.8.3.0; 1,643,544 bytes.
SHA-256:

```text
17C974CDF0FF84E172FF0F3B46E560008FE89C5F093C328D3BB5BEB3E4C867B9
```

The native SHA-1 payload, table offsets and file lengths validate. All 86
packaged production sources match the checkout's SHA-256 hashes. Tests, work
directories, generated outputs and private player/world data are excluded.
The final isolated native probes used the same package identity. The local
`package-verification.json` receipt retains every source hash.

UTF-8 description sizes: main 5,060 bytes, Workshop English 7,109 and German
7,310. Both prepared Workshop descriptions remain below the 8,000-byte limit;
`changelog.txt` contains only the current 0.25.0 notes, not repeated history.
The package is prepared only; these hashes do not establish client installation
or a public upload. Historical packages keep their own receipts.

## Open Acceptance Gates

- No rendered 0.25.0 GUI or new screenshots were produced. Native UI dispatch
  and measurement-only fonts are not proof of visual quality or physical input.
  The reported V key remains unverified; use Details/Pets on her wheel.
- Two real connected clients, host-and-play and dedicated-server reconnects
  remain untested. Packet fixtures and both authority modes are not that test.
- Actual progression, long sessions, save/reload cycles and perceived natural
  pacing need a focused player run. AI-authored translations need speakers.
- Identical NPC-slot/species reuse occurring entirely between a captured client
  action and server delivery has no generation token in the existing packet.
  Observed invalidation is covered; the invisible replacement race is not
  claimed eliminated. Arbitrary modded pet drawing is not emulated.
- No installation, publication, Steam approval or subscriber delivery is
  established by this preparation. See ../BETA.md for the wider acceptance gates.

## Short Player Check

1. Confirm 0.25.0 under Mods before testing; do not infer it from this document.
2. Use a furnished enclosed room and a valid selected bed. Check that home
   commentary is grounded, rather than calling the home's chest a new treasure.
3. Visit a non-resident shelter three times, spending at least twelve seconds
   outside between visits. Use Chatty for shorter question spacing and wait
   until she is idle. Choose Our home, later test Just a stop with another room.
4. If defense temporarily closes an unanswered question's wheel, open Commands
   and toggle EVIL. The original question text and answer should remain intact
   when defense ends. Right-click dismissal normally answers Later instead.
5. Put a pillar at a room's center; classify it. Demolish a captured room before
   answering and confirm it does not become owned. Test another world/Sigil.
6. Compare automatic treasure suggestions with an explicit Find treasure order;
   a recognized home chest is skipped automatically, not made inaccessible.
7. Test Company/Collect, critter loss, Pause/Resume/Abort, pet switching and mixed
   drops. Inspect Diagnostics if a task waits. Save/reload and check exact cargo.

Automatic Field Notes remain optional, off by default and local. They omit
account/character/world names, chat and coordinates. Place coordinates are
ordinary local Sigil save data; typed feedback keeps the text entered. No online
AI, automatic upload or developer mailbox delivery is introduced.
