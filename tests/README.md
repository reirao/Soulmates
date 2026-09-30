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

An actual single-player client pass and unaltered screenshots are documented in [PLAYTEST-2026-09-30.md](PLAYTEST-2026-09-30.md). Automated checks do not replace live multiplayer, progression, or long-session testing.

MSBuild compilation reports no warnings or errors. The final Workshop package uses tModLoader's native release compiler without `-eac`, which reports one CS8632 nullable-annotation warning and no errors. The 2,669 engine checks passed against that exact release package. tModLoader's packager still emits `Image loading failed: unknown image type`; the package builds and loads successfully, but that image diagnostic remains unresolved.
