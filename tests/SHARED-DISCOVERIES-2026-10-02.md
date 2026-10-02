# Shared Discoveries Revision

Soulmates 0.17.1 is a local playtest candidate for tModLoader 2026.8.3.0 / Terraria 1.4.4.9. This revision includes the deliberate mouse modes documented in `INPUT-MODES-2026-10-02.md`. It has not been published to GitHub or Steam.

## Personal Observations

- Showing a nearby item, mining target or forestry spot retains the existing concrete inspection facts, followed by a short observation matching the companion's saved personality and voice.
- Soft and Playful each have five personality-specific responses. Direct remains exactly factual. Low energy or low mood produces a quiet response without refusing the inspection.
- Coin inspection names the uncapped wallet, rather than treating money as resource cargo.
- Inspection itself changes no assignment, energy, XP, items, terrain or autonomy permission. Existing native player-emote reactions and their cooldown-limited rewards remain separate and unchanged.
- English and German include the new responses. No saved-profile migration, external AI service, automatic permission or additional network message is introduced.

## Verification

- Main mod and native probe compile with zero errors and warnings.
- Static checks: 705 bilingual keys, 358 source references and 3,337 assertions, all passed.
- Isolated native engine checks: 10,787 assertions, zero failures. The final package loads as Soulmates 0.17.1.
- Fixtures cover all five personalities and three voices, distinct Soft responses, factual Direct responses, tired/quiet companions, coin inspection and unchanged gameplay state after inspection.
- Earlier input routing, creation, cargo conservation, wallet, combat, healing, forestry, critter, social, lifecycle and multiplayer packet fixtures remain enabled.
- Engine fixtures are not a graphical or live multiplayer playtest. Fresh client input/visual checks and real Host & Play remain necessary; no complete progression playtest or new gameplay screenshot is claimed here.

Evidence and deliverable: `outputs/revision-0.17.1`. Package SHA-256: `96A5D42CE581193C15E924371251DFC753DFDD2AA75285257DD1DD3CD02667DC`; size 1,365,271 bytes. The regression probe is not included in the playable installation.

## Test Installation

The candidate is installed in the existing isolated save/client `work/client-revision-0.15.1`; that folder's legacy name is not the installed mod version. Only `Mods/Soulmates.tmod` was replaced. Enabled mods remain Soulmates only. Thirty character/world files were checked before and after installation and are unchanged. The previous package was backed up before replacement, and the installed package hash matches the tested deliverable.

Receipt and backup: `outputs/install-test-0.17.1/20261002-105554`. The normal Steam installation and public Workshop descriptions remain on 0.16.1. No public content or normal player/world save has been changed.

`work/Start-Soulmates-Test.ps1` starts this isolated client with the existing test characters and worlds. The graphical client was started outside the restricted engine-test process and observed in its real main menu. Its native log confirms `Configuring Content` and `Finalizing Content: Soulmates (Soulmates) v0.17.1`, followed by successful mod-load completion on 2026-10-02 at 13:01 CEST. Only Soulmates is enabled; the regression probe is absent. No world was entered during this startup check. The initial Workshop-change notice lists 0.16.1 from subscription discovery; it is not the candidate actually loaded.

The client remains open at the main menu for the user's playtest. This is verified startup, not a completed graphical gameplay test. `git diff --check` passed; line-ending notices are not whitespace errors.
