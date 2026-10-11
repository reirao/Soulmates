# Soulmates 0.25.1: Home Within Reach

Installation follow-up: the exact tested candidate was installed in the normal
client on October 11, 2026, at 21:07:26 CEST. The installed package was verified;
saves remain unchanged. See the [installation receipt](CLIENT-INSTALL-0.25.1-2026-10-11.md).
This is not graphical playtest or public-delivery evidence. The preparation
record below retains its original scope.

Creator follow-up: after installation the player reports "klappt jetzt" and
requests GitHub/Steam publication. This is a positive current playtest report,
not evidence that every checklist item, physical key or multiplayer case passed.
Release descriptions/changelog are updated separately from the installed
candidate; its historical hash below is not the publication rebuild's hash.

Local correction prepared October 11, 2026, following the player's report that
Home was absent from the installed 0.25.0 wheel. This pass changes no normal
client files, player saves, Workshop content or GitHub publication.

## Confirmed Problems

1. Place sampling, captured home questions and persistence existed, but no Home
   root or manual quick action exposed them. Only an automatic familiar-shelter
   question could open the classification flow. Existing tests covered that
   question but did not establish a player-accessible Home control.
2. Multiplayer compass edits sent complete settings from the last acknowledged
   profile. Toggling Passage and then Right before acknowledgement sent Right
   with the old Short end, losing the first choice.

This was a focused wheel/dispatch review, not a new exhaustive audit of every
mod feature. EVIL, Pets, Config/Last and Pause/Resume/Abort have reachable
production routes; no further disconnected route was confirmed in this pass.

## Repairs

- Permanent Home root in her wheel, separate from player Emotes and world
  tools. It renders the native TextureAssets.SpawnPoint map-house symbol, not
  a generated asset or an invented emote constant.
- Explicit DiscussHome quick action uses the existing four-answer conversation
  and server request route. It can classify a valid nearby shelter immediately
  or reclassify a remembered home; manual requests do not invent visits.
- Quiet, autonomy off, idle Pause and Stay do not block this configuration
  conversation. Automatic home offers still require familiarity and obey
  cadence. Ongoing work, defense, resident housing and captured question
  identities retain their protections. Invalid shelter produces an explanation.
- Home answers grant no rewards or permissions and revalidate the captured
  world/room. Later changes no role. An unanswered question or permission
  request is not replaced by a Home click.
- Eleven companion root hit regions remain separated. Home is exercised through
  actual production mouse-edge handlers into the production answer wheel.
- A bounded, Sigil-bound mining UI draft merges rapid clicks without modifying
  the authoritative profile. Each edit is sent immediately. Intermediate
  acknowledgements preserve later edits; conflicts, identity changes and
  timeout clear the draft without replaying requests. Closing does not discard
  an unsent final click, because every deliberate edit has already been sent.
- All new labels and explanations are present in nine language catalogs.

## Verification

Native runtime: tModLoader 2026.8.3.0, Terraria 1.4.4, bundled .NET SDK 8.0.425.
All probes use isolated workspace save directories, not the normal client.

| Check | Result |
| --- | --- |
| Production MSBuild | Zero warnings, zero errors |
| Native package compiler | Zero warnings, zero errors |
| Native regression | 97,809 assertions, zero failures |
| Independent audit | 490 expectations, zero failures |
| Static/localization | 58,547 assertions; 997 keys in nine catalogs; 551 source references |
| Package integrity | 86 matching production sources; 114 entries; private/test content excluded |

Home behavior tests cover single-player and server authority, all combinations
of autonomy, Pause, Stay and question cadence, immediate reclassification,
resident housing, busy/outdoor rejection, server dispatch and client mutation
rejection. Existing demolition, world-change, replay and conservation checks
remain. Production root clicks are measured at four viewports and three
requested UI scales, including native viewport-limited scaling; all nine
localized labels resolve.

Compass counterchecks capture real native outgoing packets and apply normal
client profile updates. They cover rapid end/direction/approach edits,
intermediate and same-frame acknowledgements, return-to-baseline choices,
rejection/timeout, same-ID replacement Sigils, close-before-acknowledgement and
the 64-request bound. Single-player configuration is also checked.

An early compile rejected a nonexistent EmoteID.ItemHouse reference. The native
runtime metadata was inspected and the implementation uses the actual map
texture instead. Subsequent production and native compilers are clean. The
test-only regression probe retains 33 pre-existing nullable/Hjson warnings.
Malformed-wire, blocked-write and registry-association messages are expected
negative/sandbox diagnostics in these isolated runs, not graphical acceptance.

## Exact Package

`outputs/home-wiring-0.25.1/Soulmates.tmod`

- Internal name/version: Soulmates 0.25.1
- Loader: 2026.8.3.0
- Size: 1,648,214 bytes
- SHA256: `0667B514335D06CB3A03AB3EA175D5C1845051E8A0346D0CA6E021DBD1EB4457`

Native payload integrity, source hashes and packaging exclusions are verified
in outputs/home-wiring-0.25.1/package-verification.json. Probe logs retain their
own scope; matching hashes tie the final isolated checks to this package.

## Remaining Acceptance

No rendered client session, physical keypress, screenshot set, two connected
clients or endurance campaign was performed. V remains unverified. The
native-fixture click route is not proof of a physical mouse click or visual
quality. Home is place memory, not teleportation or autonomous construction.
The place sampler remains conservative for large/open/modded buildings.

The normal client still has the separately installed 0.25.0. This 0.25.1 package
is prepared only, with no public upload or subscriber-delivery claim. Use the
same version on all multiplayer peers.

## Short Player Check

1. After installation, confirm 0.25.1 under Mods.
2. Right-click her and select the house symbol, inside a furnished enclosed room.
3. Choose Our home, reopen Home and change it to Just a stop. Save/reload.
4. Try Home with Stay, autonomy off and Quiet. Check that these settings remain
   unchanged. Try outside, during work and in assigned NPC housing for feedback.
5. In multiplayer, quickly toggle Passage, click Right and close the wheel.
   Reopen after the server replies and check that both choices remain selected.
