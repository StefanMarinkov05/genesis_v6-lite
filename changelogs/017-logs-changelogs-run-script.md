# logs/ folder, changelogs/ folder, and a one-click run script

**Type:** Feature / change
**Model:** Opus — infra addition threaded through existing call sites without disturbing any of them.

## Summary
Three infrastructure additions requested together: a `logs/` folder that maps every project to the
spec + seed it was generated from and holds a per-project leveled action log; this `changelogs/`
folder itself (backfilled with one entry per past request/change, `001` through `016`); and
`run.bat`, a double-clickable script that bootstraps the solution (first run only), restores,
builds, tests, and runs the generator, showing the interactive spec/seed picker if more than one
candidate exists.

## Techniques
- `StageContext.Info()`/new `StageContext.Debug()` now also append a structured JSON line
  (timestamp/level/message) to `logs/<Project>/actions.log.jsonl`, on top of the existing console
  output - a small, constructor-signature-free addition (computed from `Directory.GetCurrentDirectory()`,
  not a new required parameter) so no existing call site needed to change.
- New `logs/registry.jsonl` (full append-only history, one line per invocation) and
  `logs/registry.json` (a project-name-keyed snapshot of just the latest run) recording which spec
  and which seed-response file (if any) each project was generated from. Both are best-effort: a
  logging failure can never break the pipeline itself.
- `run.bat` checks for the .NET SDK (never auto-installs - that always needs explicit consent),
  creates `dps-nn.sln` only if it doesn't already exist, then `dotnet build` / `dotnet test` /
  `dotnet run --project src\Genesis.Cli -- run --all --out out --resume`. Deliberately omits `--spec`
  so the interactive picker engages when there's more than one candidate spec/seed lying around.
- Changelog entries backfilled from `plan/decisions.md` (the authoritative running record kept
  throughout every session), not reconstructed from memory, to keep the history accurate.

## Affected files
- `src/Genesis.Core/Pipeline/Pipeline.cs`
- `src/Genesis.Cli/Program.cs`
- `run.bat` (superseded by `1-build-and-check.bat` + `2-generate.bat` in the next request, see `018`)
- `changelogs/` (new folder, `README.md` + `001`-`017`)
