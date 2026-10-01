# Soulmates Regression Checks

These checks use the installed tModLoader libraries, without downloading test packages.

## Static and Math Checks

From the repository root:

```powershell
dotnet run --project tests\Soulmates.Regression.csproj -- .
```

The harness parses both Hjson catalogs, checks source key references, enum names, placeholder parity, description byte limits, text wrapping, and the production UI coordinate conversions. Its small Terraria stub simulates the engine's world/UI screen-state switching; it does not simulate gameplay or rendering.

Malformed or unreadable catalogs now report their path and parser diagnostic, then exit with status 1 instead of throwing an unhandled exception. A deliberately malformed local fixture was checked separately from the passing production catalogs.

## Actual Engine Checks

The test-only `SoulmatesRegressionProbe` is excluded from the released mod. Build it against the current Soulmates assembly, place both `.tmod` files in an isolated save directory, and enable only those two mods there. Run tModLoader with `-server -nosteam -tmlsavedirectory <isolated-directory>`.

At `PostAddRecipes`, after item/NPC/emote hooks and mod networking IDs are initialized, it validates every packaged translation in English and German, saved-key migration, mixed-item cargo and reserve limits, independent clones, save/load, binary profile transport, connected ore traversal, and exposed-edge mining rules. It writes `Soulmates-engine-checks.txt` in that isolated directory and exits before loading a world. Never enable the probe in a normal play session. Earlier versions ran at `PostSetupContent`; they could not exercise finalized catch or emote-observer hooks.

For 0.12.2, the isolated engine run passed 2,669 assertions. The static/math run passed 2,013 assertions. Engine coverage now includes actual world-to-pack pickup transactions, exact conservation, full packs, ownership reservations, reusable Soulcores, UI input ownership, release handling, deferred initiative prompts, and speech visibility. The earlier 0.12.1 engine run passed 2,115 assertions.

For 0.13.0, 4,051 isolated engine/geometry assertions and 2,065 static/math assertions pass. New coverage includes all 20 resource tiers, independent equipment/resource slots, coin collection with full cargo, huge wallet balances, partial withdrawals into nearly full inventories, legacy migration on summon, clone independence, save/load, binary transport, resource page wrapping, and the production slot-bound calculation at six widths. Geometry checks exercise the actual arithmetic without rendering a client UI; they are not visual playtesting.

An actual single-player client pass and unaltered screenshots are documented in [PLAYTEST-2026-09-30.md](PLAYTEST-2026-09-30.md). Automated checks do not replace live multiplayer, progression, or long-session testing.

For the final 0.14.0 package on tModLoader 2026.8.3.0, 6,629 engine assertions and 2,178 static/math assertions pass. New coverage includes a 500-decision starvation stress check, independent cooldowns, saved individual permissions, changed item identity, timeout during combat, low mood/energy, read-only Look, directed forestry, a hovered companion not replacing captured UI, and speech visibility while a wheel is open. A bounded real-client control pass is recorded in [PLAYTEST-2026-10-01.md](PLAYTEST-2026-10-01.md).

Both MSBuild and the native release compiler without `-eac` report zero compiler warnings/errors for 0.14.0. Explicit nullable contexts removed the native compiler's annotation diagnostics. tModLoader's packager still emits `Image loading failed: unknown image type`; the package builds and loads successfully, but that diagnostic remains unresolved. The client also logged a small immediate asset-load warning for native wheel navigation. Neither diagnostic is treated as a passing visual test. Broader rendering, long sessions, and live multiplayer remain test gaps.

When compiling this checkout outside a folder named `Soulmates`, use `-p:BuildMod=false -p:TargetFramework=net8.0` for the MSBuild-only check, then package with tModLoader's `-build` pointing at the canonical `ModSources/Soulmates` directory. Do not install the incorrectly named `ich.tmod` produced by a default MSBuild packaging step in this checkout.

For 0.15.0, 9,950 engine assertions and 2,335 static/math assertions pass. Coverage adds resident affinity and memory transitions, bounded world-scoped history, cloned/save/network state, all native-emote response keys, actual NPC emote observation with one reply per visit, replaced residents and explicit-job cancellation, actual native insect catches in single-player and server authority modes, full cargo and missing nets, inactive/statue/released insects, client-side rejection, and flock removal after returning real items. A disconnected socket fixture permits server packet construction without opening a network listener. This is not a live multiplayer test. No 0.15.0 graphical client run is claimed; see [0.15.0 scope](../releases/0.15.0.md).
