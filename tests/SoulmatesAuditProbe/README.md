# Soulmates Audit Probe

This separate, test-only native mod counterchecks safety boundaries against the exact **Soulmates 0.22.2 beta candidate**. The latest [Living Critters repair and critical recheck](../LIVING-CRITTERS-0.22.2-2026-10-08.md) records **176 passing expectations, zero failed expectations and no harness errors**, including aged native critters and rejection of a fake zero-damage catchable enemy. Earlier permission, transaction and [stabilization checks](../BETA-STABILIZATION-0.20.0-2026-10-05.md) remain included. Original findings remain in historical audit reports. It is excluded from the production mod by project exclusions and `buildIgnore`; it is not a normal gameplay mod, independent human review or alternative release.

**Never enable it in a normal client or save directory.** It replaces in-memory player/NPC/item/tile fixtures, suppresses outgoing native packets, writes its result, and terminates the process before loading a world. It never opens a network listener. A small test-only item models a reduced stack limit; no such item is added to the real Soulmates package.

## Cases

- Capture Pause/withdrawal for A, switch to B, and deliver the old packets through the production server handlers.
- Use an identity-bound mining-rule request as a rejection control.
- Replace one gathering prompt with another and deliver the original Always answer.
- Observe the remote owner's explicit mining target with unrelated local mouse globals; reject wrong tools, idle owners and out-of-reach tiles. Exercise the real single-player PickTile hook.
- Compare native item loading with Soulmates profile loading after a saved item's stack maximum shrinks.
- Check that pending personal questions do not block Always-approved gathering and remain answerable; Ask work still needs consent.
- Check stale storage indices, changed quantities, modded item metadata, wallet balances, legal legacy withdrawals, full storage, save/load and binary transport.
- Check authoritative initiative tokens, duplicate answers, UI acknowledgement identity and delayed prompt notifications.
- Initialize real creator/mailbox UI states and inspect panel/action bounds at effective native UI scales. The font is for measurement only; this is not rendering or visual QA.
- Check that automatic selection and execution respect the native pickup delay, as ordinary gathering does.
- Deliver a terrain order after the original ore has been replaced by another eligible material.
- Check direct-order prefix width against native `Item.prefix`, with a zero-prefix control.
- Send an equipment request without any owned trinket.
- Deliver the obsolete custom native-emote request after replacing the question it could answer.
- Check that a deferred critter-care question survives the existing general question cooldown, with an immediately eligible question as control.
- Apply an inventory update after an intervening local slot change.
- Verify rejected withdrawals/deposits conserve owner and cargo items, valid acknowledgements commit once, malformed receipts retain the pending transaction, and duplicate proposals/completions never restore older state.
- Preserve stable learned mod-material names through save/load, clone and binary transport, including temporarily missing content and its share of the 48-material limit. Prefer new keys over legacy IDs; migrate unidentified legacy modded IDs conservatively.
- Force a local journal append failure and check the pending buffer bound. The failing path is a directory inside the isolated save folder, not a protected external path.
- Compare an unchanged empty-space planting target in single-player and server handlers, with occupied-ground planting as a valid control.
- Compare Dungeon/Obsidian eligibility with installed native pickaxe damage rules.
- Compare pack healing delay with native potion modifiers, and respect a test-only ModPlayer item-use veto.
- Deliver a native resident-emote observation during a pending withdrawal, then reject the receipt and verify legitimate social history survives.
- Cover sufficient Dungeon progression and the native surface exemption, consume-versus-use hook behavior with exactly-once callbacks, a potion-delay veto, a cursed owner and an allowed alternative to a forbidden potion.
- Validate accepted/deferred resident events, duplicate observations, distant events, replaced/renamed residents, inactive companions and changed worlds. Preserve stable resident keys through save/load, missing content, clone, transport and conservative legacy migration.
- Reject late consent after recovery holds, routine changes and Never; compare fresh offers with delayed answers and valid controls. Pause/resume an approved native catch without duplicate cargo.
- Reject every independent client quick-action mutation and undefined/UI-only action, comparing the complete profile before and after.

The October 3 audit reported **16 passed and 5 failed safety expectations**, grouped into four defects, plus the question gate. See [the historical audit report](../FULL-AUDIT-2026-10-03.md) and [the repair result](../REPAIRS-2026-10-04.md).

The historical October 4 review reported **80 expectations: 71 passed and 9 failed**, grouped into eight additional defects. All 67 preceding repair expectations remained passing. See [the full review baseline](../FULL-CODE-REVIEW-2026-10-04.md) and [the subsequent 0.19.7 repairs](../COMPLETE-REPAIRS-2026-10-04.md). These boundary fixtures invoke production code but are not connected multiplayer or graphical gameplay.

The preceding 0.19.7 repair countercheck had **96 passing expectations, zero defects and no harness errors**, with exit 0. Missing-content material capacity and rejected/duplicate inventory acknowledgements are covered explicitly. The artifact identity and broad-suite results are in the repair report.

The historical fresh 0.19.7 review had **109 expectations: 102 pass and 7 fail, no harness errors**. All previous 96 expectations still passed. Its raw failing output is preserved in the review artifacts; the runner returned 1 even though the fixture requested exit 2 for reproduced failures.

The final 0.19.8 repair counterchecks have **136 passing expectations, zero defects and no harness errors**, exit 0. [SecondReviewCases.cs](SecondReviewCases.cs) now checks the repaired boundaries; [NativeRepairCases.cs](NativeRepairCases.cs) adds the repeated-review controls. A passing fixture set remains bounded evidence, not certification of every gameplay path.

## Isolated Run

Use a separately prepared save directory, such as `work/beta-stabilization-audit-engine`, containing the exact 0.20.0 package in `Mods/Soulmates.tmod`. Its isolated `Mods/enabled.json` must contain only `Soulmates` and `SoulmatesAuditProbe`. Do not use the normal `My Games/Terraria/tModLoader` directory. The probe rejects a different Soulmates version.

Build with the native tModLoader compiler, from the tModLoader installation or the prepared compatible runtime directory:

```powershell
dotnet .\tModLoader.dll -server -nosteam -build "<repo>\tests\SoulmatesAuditProbe" -tmlsavedirectory "<repo>\work\beta-stabilization-audit-engine"
dotnet .\tModLoader.dll -server -nosteam -tmlsavedirectory "<repo>\work\beta-stabilization-audit-engine"
```

The compiler writes the test package into the isolated `Mods` directory. Runtime results are written to `Soulmates-audit-checks.txt` there. The source requests exit 0 for passing expectations, 2 for reproduced safety failures, and 1 for a harness exception. The October 4 runner returned 1 despite a completed report with nine `DEFECT` lines and no `HARNESS ERROR`; do not infer the failure category from the numeric status alone. Treat every nonzero exit as a failed audit and inspect the result file. Completion of the probe does not mean the product passed.

Do not upload test packages or private journal files with a release. Connected multiplayer, graphics and normal gameplay remain separate verification work.
