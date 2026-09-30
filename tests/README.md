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

For 0.12.1, the isolated engine run passed 2,115 assertions. The static/math run passed 2,013 assertions. These checks are not a live multiplayer, screenshot, or long-session gameplay test; speech placement and UI feel still require normal client playtesting.

The compiler reports no warnings or errors. tModLoader's packager still emits `Image loading failed: unknown image type`; the package builds and loads successfully, but that image diagnostic remains unresolved.
