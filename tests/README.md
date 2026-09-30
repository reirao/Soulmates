# Soulmates Regression Checks

These checks use the installed tModLoader libraries, without downloading test packages.

## Static and Math Checks

From the repository root:

```powershell
dotnet run --project tests\Soulmates.Regression.csproj -- .
```

The harness parses both Hjson catalogs, checks source key references, enum names, placeholder parity, description byte limits, text wrapping, and the production UI coordinate conversions. Its small Terraria stub simulates the engine's world/UI screen-state switching; it does not simulate gameplay or rendering.

## Actual Engine Checks

The test-only `SoulmatesRegressionProbe` is excluded from the released mod. Build it against the current Soulmates assembly, place both `.tmod` files in an isolated save directory, and enable only those two mods there. Run tModLoader with `-server -nosteam -tmlsavedirectory <isolated-directory>`.

At `PostSetupContent` it validates every packaged translation in English and German, saved-key migration, mixed-item cargo and reserve limits, independent clones, save/load, binary profile transport, connected ore traversal, and exposed-edge mining rules. It writes `Soulmates-engine-checks.txt` in that isolated directory and exits before loading a world. Never enable the probe in a normal play session.

For 0.12.2, the isolated engine run passed 2,669 assertions. The static/math run passed 2,013 assertions. Engine coverage now includes actual world-to-pack pickup transactions, exact conservation, full packs, ownership reservations, reusable Soulcores, UI input ownership, release handling, deferred initiative prompts, and speech visibility. The earlier 0.12.1 engine run passed 2,115 assertions.

For 0.13.0, 4,051 isolated engine/geometry assertions and 2,065 static/math assertions pass. New coverage includes all 20 resource tiers, independent equipment/resource slots, coin collection with full cargo, huge wallet balances, partial withdrawals into nearly full inventories, legacy migration on summon, clone independence, save/load, binary transport, resource page wrapping, and the production slot-bound calculation at six widths. Geometry checks exercise the actual arithmetic without rendering a client UI; they are not visual playtesting.

An actual single-player client pass and unaltered screenshots are documented in [PLAYTEST-2026-09-30.md](PLAYTEST-2026-09-30.md). Automated checks do not replace live multiplayer, progression, or long-session testing.

MSBuild compilation reports no warnings or errors. The native release compiler without `-eac` reports one existing CS8632 nullable-annotation warning and no errors. All 4,051 engine/geometry checks passed against the exact 0.13.0 release package. tModLoader's packager still emits `Image loading failed: unknown image type`; the package builds and loads successfully, but that diagnostic remains unresolved. The creator confirmed the new cargo works in play; broader rendering, long sessions, and live multiplayer remain test gaps.
