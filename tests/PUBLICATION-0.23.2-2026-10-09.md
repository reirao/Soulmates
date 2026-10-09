# Soulmates 0.23.2 Publication Checks

October 9, 2026. Documentation and publication follow the creator's positive playtest. No gameplay or keyboard claim is inferred from upload success.

## Package Identity

- The creator's Workshop update appears at **11:15 CEST**, entry `1791537350`, on the existing item `3807130821`. Steam still reports moderation pending at this check.
- The downloaded `2026.8/Soulmates.tmod` is version **0.23.2**, **1,588,547 bytes**, SHA256 `311908B351DB096E59286739ED4D42D76D9A118ECBEDA013EE70BEDE7344C7B4`.
- Native payload integrity is valid; all **81** packaged production source hashes match the repository. There are **109** entries and no packaged private/test content. This exact artifact is the GitHub release asset.
- This is a rebuild, not the earlier 1,599,265-byte installation artifact. The earlier [installation record](CLEAR-PET-ACTIONS-0.23.2-2026-10-09.md) and hash remain unchanged as historical evidence.
- A later local client rebuild has SHA256 `09E08961B082E1E03E3A1937D56D76EC0F077EE0E19046B2B8F746299FBA8B93`. It too matches all 81 production source files. Publication work did not replace it or edit saves, activation or controls.

## Repeated Checks On The Workshop Artifact

| Check | Result |
| --- | --- |
| Native regression, isolated installed engine | 93,883 assertions, 0 failures |
| Independent native audit | 182 expectations, 0 failures; probe builds with 0 warnings/errors |
| Static/localization recheck | 54,971 assertions, 933 keys in nine languages, 497 references |
| Source/package integrity | All 81 production source files match; native payload valid |
| Public English/German descriptions | 7,768 / 7,914 UTF-8 bytes, both below 8,000 |

The general test-only probe retains its 33 known nullable/Hjson warnings. Rejected malformed packets, an intentionally blocked write and the sandbox's denied optional file-association registration are not test failures. Native regression/audit runs exit successfully; they do not constitute connected multiplayer or rendered keyboard acceptance. Receipts and raw test results are retained locally in `outputs/release-0.23.2`.

## Public Text

English and German Workshop descriptions were saved through their language-specific editors and checked after saving. They describe twelve real pet slots, giving/summoning/returns, restored ore commands, current scope and local-only feedback. Gallery images are explicitly older-build evidence, not new screenshots. AI assistance and native-asset attribution remain disclosed.

The existing 0.23.2 change entry was edited, not duplicated. It contains only this patch's changes. The earlier 0.23.1 V claim remains quoted with an explicit dated correction: a software trigger did not establish a physical keypress. V is **not certified fixed**. Wheel Details and Pets are the documented routes. The [README lesson](../README.md#the-v-key-lesson) preserves the mistake and required distinction between handler, engine mapping and real-client input tests.

GitHub main and the existing development branch are kept at the same release source. Upload, Steam approval, exact package download and each subscriber's loaded version remain different checks. No subscriber-wide delivery or complete beta acceptance is claimed.
