# Localization Maintenance

English (`Localization/en-US.hjson`) is the source catalog; German remains independently authored in `de-DE.hjson`.

`translations.json` contains all English-relative keys and seven translated values per key, in this fixed order:

1. Italian (`it-IT`)
2. French (`fr-FR`)
3. Spanish (`es-ES`)
4. Russian (`ru-RU`)
5. Brazilian Portuguese (`pt-BR`)
6. Polish (`pl-PL`)
7. Simplified Chinese (`zh-Hans`)

The regression runner mechanically exports these values as JSON-compatible Hjson catalogs. Dotted leaf keys are intentional and supported by tModLoader. The authoring data is excluded from mod packaging with the rest of `tests`.

Edit translations here, then regenerate and verify from the repository root:

```powershell
dotnet run --project tests/Soulmates.Regression.csproj -- . --generate-locales
dotnet run --project tests/Soulmates.Regression.csproj -- .
```

Checks cover exact key sets, nonempty text, format arguments, generated-file consistency, source references, Unicode wrapping and bounded labels. The isolated native probe also switches all nine actual engine cultures and compares every registered value with its packaged catalog. English wording changes still require a translation review, even when the key stays the same.

Keep numbered placeholders, command names/subcommands and proper names unchanged. Do not translate player-selected names. Preserve the distinction between personality and Soft/Direct/Playful voice, and do not promise unavailable actions or future features.

These new translations are AI-authored, not certified native-speaker translations. Corrections to grammar, Terraria terminology, readability and character voice are welcome. Structural checks do not replace graphical or linguistic review.

## Multiplayer Scope

Menus and locally generated replies use the client's selected culture. `CompanionSpeech` sends a localization key, and minigame results send moves, so those are also resolved on the client. Existing `TalkResponse`, `QuickActionResponse` and `ProfileUpdate` messages still carry server-formatted text; those replies can retain the server's language in a mixed-language session. Complete per-client localization needs structured key-and-argument transport, not reverse translation of rendered strings. Stored literal memories also keep their original wording; keyed memories resolve in the current culture.

The installed Terraria 1.4.4 runtime supports nine languages. Japanese, Korean and Traditional Chinese belong to the newer 1.4.5 language set and are not claimed as supported by this build.
