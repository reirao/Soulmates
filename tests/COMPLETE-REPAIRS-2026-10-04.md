# Soulmates 0.19.7: Repairs and Critical Recheck

**Subsequent review:** [the fresh full 0.19.7 review](FULL-REVIEW-0.19.7-2026-10-04.md) found additional native-rule and event-order defects. The repair results below remain historical evidence for their stated checks, not a current all-clear.

## Scope and Status

This pass repairs all nine findings in the [full 0.19.6 source review](FULL-CODE-REVIEW-2026-10-04.md). Existing uncommitted work is preserved. The preceding review covered all 66 production C# files; this pass rechecks the changed logic, its callers and its interactions with the earlier repairs. Historical requirements still marked partial/open in that review are not silently implemented or promised here.

**Local candidate only.** No normal-client installation, character/world save, Git commit/push, Steam description or Workshop publication was changed. The last recorded installation is 0.19.5; the recorded public baseline is 0.19.3. Those external states were not rechecked here.

## Repairs

| Previous finding | Repair and countercheck |
| --- | --- |
| Late owner-slot replacement loses an intervening item | A transfer has a unique ID, exact before-images and acknowledged completion. The client applies all changed slots only if their preimages still match. A conflict leaves the client's items untouched and rejects the transfer; the server restores the companion's before-profile and reconciles owner changes from the receipt. Tests cover valid withdrawal, conflicting withdrawal, moved deposit inputs, malformed acknowledgements and duplicate delivery. |
| Unearned trinket equipment through a custom request | The custom request permits only None/removal. Real native item use still equips the reusable owned trinket. The no-item forged-equipment fixture is now rejected. |
| Stale obsolete custom emote authorizes a newer question | Remove the unused sender/handler and leave its enum ordinal reserved. The supported token-bound question protocol and Terraria's live native observer remain. A captured obsolete packet cannot approve replacement work. |
| Prefix sender/reader width mismatch | Both sides use native full-width Int32 prefix metadata. Wide and ordinary prefix controls exercise actual handlers. |
| Terrain changes underneath a captured destructive order | Send the original tile type and revalidate it for Mine/Forest before planning. Replaced terrain is rejected; normal captured terrain still works. |
| Autonomous pickup bypasses native grace delay | The common eligibility and storage boundary reject noGrabDelay > 0. Only the validated receipt of an actual native net catch has an immediate-collection exception; ordinary delayed drops remain in the world. |
| Deferred critter-care question expires behind a longer cooldown | Retain its bounded offer window while general cooldown, speech or other opening conditions prevent an offer. Test both retention after 1,801 updates and actual opening after cooldown expires. Deferred care does not suppress all ambient conversation during the long wait. |
| Unbounded feedback retry buffer | At most 64 automatic records, 30-second backoff after IO failure, and dropped-event accounting. Ordinary typed notes append independently to notes-inbox.jsonl; the mailbox retains entered text on failure. Existing bug-inbox reports remain local. |
| Learned modded material uses unstable IDs | Save and transmit stable content keys. Resolve current native IDs on load; preserve missing keys without aliasing unrelated tiles. New keys take precedence. Legacy vanilla IDs migrate; unidentified old modded numeric IDs must be relearned. Clone and binary transport preserve missing identities. |

## Critical Second Pass

The broad native suite caught a real regression in the new synchronization path: applying Sigil metadata failed to refresh the native client cache's favorite flag. The cache now clones the current physical Sigil, preserving favorite/prefix and avoiding an unnecessary native equipment resend. The repaired full suite is rerun against the final package, not only the focused probe.

A second source check found that temporarily unavailable material names also need to occupy places in the 48-material knowledge limit. Learning now counts those entries; an explicit full-missing-content countercheck ensures later learning does not silently displace them on save.

Transfer ordering is checked beyond simple rejection:

- Proposals include before-images for affected physical slots plus compact hashes of the original 58-slot inventory. Receipts serialize only affected or independently changed slots, rather than every unchanged potentially large Sigil. Slot 58/the cursor is never replaced.
- Profile metadata travels by companion GUID. An accepted transfer applies its after-profile to the client's bound Sigil together with physical changes, not only after an eventual acknowledgement. Native ExtraAI consumes its packet but cannot restore the pre-transfer profile while that profile awaits completion.
- Receipt fields are fully validated before commit. Duplicate proposals reuse their cached receipt and never overwrite later client edits; duplicate receipts and completions do not commit or restore old state twice.
- Replies are deferred until the transfer resolves. Authoritative companion work waits during a pending owner transfer, preventing overlapping cargo mutation. Legitimate victory experience and witnessed critter-loss reactions are deferred, not erased by a rejected transfer's rollback.
- Reconciliation returns excess cargo as native world drops on a server instead of introducing another unilateral owner-slot write. Single-player retains the native immediate inventory-return path.
- Active transactions retry the same proposal every 120 ticks. An inactive or replaced owner stops retries. There is no ambiguous timeout refund that could duplicate an already accepted transfer.

The unsupported private ActivityFor helper and obsolete custom emote sender are removed. Public wrappers/getters remain intentionally available as compatibility/convenience APIs; absence of an internal caller alone is not sufficient evidence to delete them. No broad feature group was disabled to make the checks pass.

Some old test expectations changed because the production contract changed: the obsolete custom emote must now be inert, inventory transfers use proposal/receipt/completion instead of blind equipment replacement, and typed notes use a durable separate inbox. The fixtures still retain malformed, conflict and valid-operation controls. Cooldowns or eligibility checks were not removed to force a pass.

## Final Verification

| Verification | Final result | Scope |
| --- | --- | --- |
| Production MSBuild | 0 errors, 0 warnings | No automatic installation. |
| Native production compiler/package | 0 errors, 0 warnings | Canonical isolated Soulmates source, version 0.19.7. |
| Static/localization/math suite | 47,922 assertions pass; 845 keys, 443 references, nine catalogs | Structural, arithmetic and layout checks, not rendering. |
| General native engine suite | 39,738 assertions, 0 failures | Final exact package, disconnected packet replay included. |
| Independent safety probe | 96 expectations pass, 0 defects, no harness errors | All earlier controls plus repaired boundaries, inventory transactions and stable knowledge. |
| Creator/mailbox geometry | Ten expectations pass within the independent probe | Native effective UI scale and measurement-only font; no screenshots. |
| Embedded description | 7,733 UTF-8 bytes | Below the 8,000-byte limit, no live Steam edits. |
| Package identity | Both isolated test copies and exported artifact match | SHA256 checked after final native runs. |
| Git whitespace | Passed with CRLF accepted | Existing unrelated changes retained. |

No further confirmed defect remains from the nine-finding review or the added critical counterchecks. This is a bounded repair verdict, not a claim that every feature or possible execution path is bug-free.

Exact candidate: [Soulmates-0.19.7.tmod](../outputs/complete-repairs-0.19.7/Soulmates-0.19.7.tmod), **1,517,108 bytes**, SHA256 `CE0AB6136B91B3DB1BA0243626F8661AD634D4E9CD5A85773F7D0000C9104A13`.

Saved results: [independent safety checks](../outputs/complete-repairs-0.19.7/Soulmates-audit-checks.txt) and [general native checks](../outputs/complete-repairs-0.19.7/Soulmates-engine-checks.txt). Test source and safe isolation instructions: [SoulmatesAuditProbe](SoulmatesAuditProbe/README.md) and [regression instructions](README.md). The old 0.19.6 artifact and its failing result remain unchanged.

The tests use installed tModLoader 2026.8.3.0 and .NET 8.0.425, without dependency downloads. Production MSBuild reports zero errors and warnings. The independent probe compiles without warnings; the older general probe still has 31 test-only nullable/Hjson reference diagnostics. Deliberately malformed packets and the controlled feedback append failure emit expected diagnostics. The sandbox denies optional .tmod registry association; it does not prevent package build/load.

Both probes terminate before world loading, use isolated save folders and never open a listener. The general probe now replays actual production proposals, client receipts, server resolutions and deferred responses through disconnected in-memory sockets in their respective event roles. This is stronger than a one-sided packet construction check, but is **not connected multiplayer**. The focused probe also exercises rejected and duplicate delivery. Synthetic wide-prefix metadata and a deliberately unwritable session-file fixture are explicitly controlled boundary tests.

## Remaining Risk

No new graphical client playthrough, connected host/client latency or disconnect test, full progression, long-session balance test, third-party mod-set reload playthrough or fresh screenshots were performed. Compilation, a full-file review and passing fixtures cannot establish that every gameplay transition is correct.

In particular, pending inventory transfers rely on the owner's reliable connection and briefly pause authoritative companion activity until acknowledgement. Real disconnect/reconnect, native inventory movement under latency, arbitrary third-party network item metadata and packet-size extremes still need live observation. Stable-key missing-content fixtures are not a real two-mod-set saved-world test. Seven translations remain AI-authored and need native-speaker review; some multiplayer replies still use server language.

The previously documented feature boundaries remain: no independent always-available player wheel without a companion, full Bestiary pet AI, arbitrary learned task programs, companion-owned vanilla vanity-pet summoning, wall deconstruction or trap-warning implementation. This repair does not rename these wishes as complete features.

No private journal text, real player/world names, positions or saves are copied into the exported reports. The original failing reports remain historical evidence, not current verdicts or repeated Workshop update text.
