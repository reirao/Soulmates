# Soulmates 0.27.0 - Behind the Curtain

October 12, 2026. Local beta candidate only: not installed, committed, pushed,
uploaded or graphically playtested by this preparation. The previous public
0.26.0 and the player's installed package were left unchanged.

## Implemented Scope

- Arbitrary-precision XP and levels keep the original `12 * (level - 1)^2`
  curve. The exact integer square root avoids floating-point level estimates.
  Old int-based saves migrate; clone/save/wire paths preserve large XP.
  Decimal wire input is validated and bounded to 4,096 digits for safety.
- Level-20 cargo and ordinary retinue balance remains unchanged: 1,000
  resources/type and three distinct normal staff types. AETHER still supports
  every distinct owned staff type within 64 real item slots.
- EVIL has a direct native angry-face root on her wheel. Commands and Details
  remain available. EVIL alone remains optional theatrical presentation.
- With EVIL on, Games reveals a native suspicious-eye icon. First click warns;
  a second click starts a real owner-only duel with the same companion NPC.
  Three health phases, a two-second opening, HP display, warned dashes and
  3/5/7-shot nonhoming fans. Real character damage is possible.
- Her normal activities, familiar and retinue stop during the challenge.
  Cargo, wallet, memories, permissions and real pet/staff items survive.
  There is no boss loot, XP reward or permanent companion death.
- Victory, Abort/Pause, Games Stop, EVIL off, recall, owner loss/death, leash,
  five-minute timeout or a competing world boss ends the duel. Shots and
  combat debuffs clear. Work stays paused until explicit Resume.
- Owner-only hit predicates exclude other players, residents and critters.
  Companion threat selection and SoulBolt also exclude companion NPCs, so
  other companions do not join this private duel. Runtime boss state is not
  stored in Sigils. Matching 0.27.0 multiplayer peers are required.

## Results

| Check | Result |
| --- | --- |
| Production MSBuild | 0 warnings, 0 errors |
| Native package compilation | 0 warnings, 0 errors |
| Native regression, repeated final package | 116,213 assertions, 0 failures |
| Separate adversarial audit | 501 passes, 0 failures |
| Static/localization | 60,730 assertions; 1,036 keys in 9 cultures; 587 references |
| Package integrity | Native payload hash, 94 source files and 122 entries verified |
| Workshop UTF-8 bytes | EN 7,830; DE 7,893; both below 8,000 |

Native fixtures check every level boundary from 2 through 5,000, levels as
large as `10^100`, legacy level 20, negative gain/XP, addition beyond int
range and exact clone/save/network round trips. Boss cases call actual NPC
updates, native projectile spawning and a lethal native hit, then check
friendly restoration and conservation. Single-player uses local player 0;
the server fixture uses 255 with disconnected sockets, not connected clients.
Actual wheel handlers cover the direct EVIL root, secret reveal, first-click
warning, reopening, confirmed start and stop. The separate audit sends real
owner/foreign/stale server requests and probes competing work/emote entry
points, active world bosses, debuff cleanup and corrupt XP transport.

The regression probe still has 33 pre-existing nullable/Hjson compile warnings;
these are not production compiler warnings. Deliberately malformed fixtures
log rejection exceptions. The final suites report zero failed expectations.

## Initial Failures And Repairs

1. Older extra-AI fixtures constructed a bare ModNPC without an attached NPC.
   Applying the new flags exposed that invalid fixture. It now initializes
   through native NPC defaults rather than weakening production behavior.
2. Initial actual projectile cases produced 32 failures: native spawning
   reset AI binding fields when hostile shots used the target player's ID
   instead of native `Main.myPlayer` ownership. Spawning now follows engine
   ownership; target owner, companion index and challenge session remain in
   validated AI fields. Repeated native checks pass, including local player 0.
3. The critical recheck added gates against work/emote reward bypass, cleared
   post-duel burns and excluded this duel from other companions' targeting.
   Separate adversarial expectations and the full suite pass afterward.

## Exact Candidate

Local file: `outputs/behind-the-curtain-0.27.0/Soulmates.tmod`

- Internal name/version: `Soulmates 0.27.0`
- Native loader: `2026.8.3.0`
- Bytes: `1695161`
- SHA256: `7C65EAF9A968EA9E4289C6400CD57E084AC1BFC44B73698B4C764458A1E1C2CD`
- Regression and audit package hashes match this file exactly.
- Private work, tests, reports, outputs and probes are excluded from the mod.
- Verification receipt: `outputs/behind-the-curtain-0.27.0/package-verification.json`

## Open Acceptance

No rendered screenshots or physical input are certified here. Actual boss
visibility, pacing, difficulty, owner death/rejoin, prolonged sessions and
connected two-client multiplayer still need playtesting. Existing V-key
claims remain withdrawn/unverified. Nine catalogs contain the new text;
translations remain AI-authored and need native-speaker feedback.

Use the [candidate checklist](../releases/0.27.0.md#try-it). Start with a saved
character/world and remember that this optional duel can hurt your character.
Installation and publication require separate requests and receipts.
