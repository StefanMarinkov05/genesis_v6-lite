# Print the actual URL on every startup + auto-open a browser tab

**Type:** Feature / change
**Model:** Opus — small feature, but still in the compiler-less, pre-Claude-Code Opus-only era.

## Summary
Stefan asked to always see the actual localhost port when running the generated app, either printed
or auto-opened in a browser. Implemented both, since they're complementary (one still works in
environments where a browser can't auto-launch, e.g. a bare remote shell).

## Techniques
- `program.sbn`: registered a callback on `app.Lifetime.ApplicationStarted` that reads the actual bound
  addresses via `IServer`/`IServerAddressesFeature` (fully-qualified names, no new `using` needed) and
  prints them prominently to console. This reflects the real bound address even if it's overridden by
  an environment variable or a dynamic port, unlike just hardcoding whatever `launchSettings.json` says.
- `BuildStages.cs` (`ScaffoldStage`): sets `launchBrowser: true` on every profile in the generated
  `launchSettings.json` that has an `applicationUrl`, using the same structure-aware `JsonNode`-based
  editing already used for `appsettings.json` - so `dotnet run` also auto-opens a tab on startup.
- Both apply to every future generated project automatically going forward - no action needed per
  project once `2-generate.bat` runs the updated pipeline.

## Affected files
- `templates/project/program.sbn`
- `src/Genesis.Core/Stages/BuildStages.cs`
