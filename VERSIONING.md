# Soulmates version stages

Soulmates uses pre-1.0 semantic versions to describe maturity honestly. The earlier `9.9.x` numbers were experimental build counters, not a claim that the mod was almost finished. `0.10.0` starts the coherent public line, `0.11.0` is its first substantial behavior release, and `0.12.0` adds deliberate mining strategies and contextual ore controls.

## 0.10.0+ - AETHER-EGG Alpha

The companion identity, Soulcore creator, Soulwheels, conversations, combat, work, pack, initiative, and adaptive learning are playable together. Systems may still be rebalanced or rebuilt when longer play sessions expose weak behavior.

- Patch releases (`0.11.1`, `0.11.2`) fix behavior without intentionally changing the feature set.
- Minor releases (`0.11.0`, `0.12.0`) add or substantially reshape a tested system.
- Existing Soulbound Sigils should remain compatible. Any future data change must include a migration path.

## 0.20.0 - Beta

Feature complete for the planned 1.0 scope. Save persistence, single-player, host-and-play multiplayer, dedicated servers, progression pacing, and the primary interface have all completed repeatable test passes. Beta work focuses on defects, balance, clarity, and compatibility.

## 0.90.0 - Release candidate

No known save-loss or progression-blocking defects, no planned structural rewrite, complete English and German text, and a clean upgrade path from supported alpha and beta saves.

## 1.0.0 - Stable

Long-session and multiplayer testing support calling the core experience stable. Later minor versions may expand companions without invalidating established identities.

## Legacy number note

Manual `9.9.x` `.tmod` files compare as numerically newer than the `0.10.0+` alpha line. Players who installed a GitHub build by hand should remove that duplicate before using the Workshop AETHER-EGG line. A normal Workshop subscription replaces its own file automatically.
