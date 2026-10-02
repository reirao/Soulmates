# Soulmates 0.19.1: UI and context hotfix

## User Report and Evidence

The player reported that item-chat categories and the context-sensitive wheel did not work. The normal client's log confirms that the reported session selected and loaded local **0.19.0**, not the older Workshop build. The installed package hash matched the previous installation receipt. An outdated installation was therefore not an adequate explanation.

Previous tests primarily called conversation/context helpers directly. They did not prove that category rearrangements updated native click geometry, that selecting a topic produced visible feedback, or that mode selection released the cursor for the next world target.

## Reproduced Defects

- `TalkModeState.ApplyLayout(force: true)` changed layout properties without recalculating native dimensions unless the panel size changed. The Items category's option buttons retained old bounds underneath the new topic row.
- Topic buttons only changed a filter. They did not issue a conversation request or replace the response. This looked like a no-op until the player separately selected a dialogue option.
- Items was reachable only through the old Talk Mode's small category icon, not directly from the companion wheel.
- Choosing a persistent mouse mode reopened a wheel immediately. That left no clear transition from selecting the mode to right-clicking a new world target.

The original packaged 0.19.0 was tested with native `UIElement.GetElementAt` and `LeftClick` event dispatch. It failed eight focused assertions: two repetitions of stale option bounds and absent immediate Food/Ores/Tools replies. An initial fixture attempt incorrectly omitted `PlayerInput.OriginalScreenSize`; that fixture was corrected before attributing any failures to the mod.

## Changes

- Recalculate the full UI after forced layout changes.
- Add Items directly to the companion wheel. Selecting a topic resets its cursor and immediately requests its actual cargo summary. Display the selected category/topic name. Ignore selection changes while waiting for a multiplayer response.
- Selecting Me/Soulmate closes the wheel and arms the next world right-click. Right-click cycling inside open wheels still reopens the corresponding wheel; Terraria/X/Escape restores native controls.
- Add opt-in local category, topic, mode and context-action journal events. Do not record raw chat, response text or per-frame cursor movement.
- Keep target identity validation, native Terraria interaction priority, inventory/text-entry guards, cargo and save formats unchanged in this hotfix.

## Verification

- Main mod and regression probe compile with zero warnings/errors.
- The packaged **0.19.1** passes the isolated native-engine run: 36,161 assertions, zero failures. Most assertions belong to broader preexisting coverage, not independent graphical gameplay scenarios.
- New tests route native UI hit testing and click events through the companion Items entry and all nine item topics in all nine languages, including repeated category changes.
- A complete click sequence selects the visible Soulmate-mode symbol, right-clicks a drop through production `ProcessTriggers`, `SetControls` and `PostUpdate`, then clicks Gather through `UpdateUI`. The original item slot reaches the assignment. The analogous selected-NPC Look sequence speaks about that NPC. Self and companion right-clicks reach their separate wheels. These fixtures use native hooks and controlled world state, not a full client/world simulation.
- Existing tests still cover stale/replaced targets, protected furniture, native dialogue and alternate-item priority, mode cycling, Escape, and cargo conservation.
- Static localization/text/layout checks pass 45,957 assertions. Workshop drafts remain below 8,000 UTF-8 bytes: English 7,636; German 7,808.

## Installation and Limits

Installed only the normal client mod package after the player closed tModLoader, preserving its previous package under `outputs/ui-context-0.19.1`. The installed file matches the tested package: **1,531,827 bytes**, SHA256 **C711C6BA9410FC9B3F1F23BC05405ED3B4BFCBA410DB0A9AFDBBA04B2C6E2DCB**. Enabled mods remain only Soulmates. No regression probe, world, character or public page was changed.

This run did not operate a graphical game client or take screenshots. Visual readability, live input timing and multiplayer usability still need a player test. Nothing was committed, pushed, or published to Steam/GitHub.

## Focused Player Check

1. Confirm 0.19.1 in the loader and summon an existing companion.
2. Right-click the companion, select Items, then Food/Ores/Tools. A category name and matching response should change immediately.
3. Choose the Soulmate symbol in the wheel's mode selector. The wheel should close. Right-click a loose drop or NPC and choose one of that target's actions. X/Escape returns to ordinary Terraria use.
