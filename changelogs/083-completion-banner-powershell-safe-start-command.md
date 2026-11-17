# 083 — Completion banner: PowerShell-5.1-safe start command (drop `&&`)

**Type:** Bug fix
**Model:** Opus, medium effort, thinking on — Group G2 diagnosis half (root-cause a shell
failure across PS 5.1 / PS 7 / cmd.exe, verify by reproducing, not by guessing). The paired
"Run app" GUI button remains the Sonnet half, not done here.

## Summary

The green-run completion banner (`Program.cs` `PrintCompletionBanner`) printed:

```
Start: cd {projectDir} && dotnet run
```

which **hard-fails at parse time in Windows PowerShell 5.1** — Stefan's confirmed default shell.

## Diagnosis (reproduced, not assumed)

- Confirmed shell: **Windows PowerShell 5.1** (Desktop edition, `powershell.exe`), build 19041,
  console code page 65001 (UTF-8). Not PowerShell 7+.
- `cd "d:\Projects\ДПС-НН" && dotnet --version` →
  `The token '&&' is not a valid statement separator in this version.` The `&&`/`||` chain
  operators were **added in PowerShell 7 (Core)**; PS 5.1 rejects `&&` before `cd`/`dotnet` ever
  run. (`cmd.exe` accepts `&&`, which is why the shape looked fine until PS 5.1.)
- The Cyrillic `ДПС-НН` path is a **red herring**: `cd 'd:\Projects\ДПС-НН'` resolves fine in PS
  5.1 (the filesystem is Unicode). It only *looked* implicated because the `&&` parse error echoes
  the whole offending line, path included. (Only under a non-UTF-8 console code page would the path
  *display* as mojibake — it still resolves.)

## Fix

Emit a single command with no chain operator and no `cd`:

```
Start: dotnet run --project "{projectDir}"
```

`{projectDir}` is `ctx.ProjectDir` (== `Path.Combine(WorkDir, ProjectName)`), the exact dir the old
`cd` targeted — behaviourally equivalent, path quoted for safety. Works identically in PS 5.1,
PS 7, and cmd.exe.

## Verified

- `dotnet build src/Genesis.Cli/GenesisV6.Cli.csproj` — 0 warnings, 0 errors.
- GUI banner-parser test (`node --test gui/test/report.test.ts`) — 3/3. The parser is
  format-agnostic (slices after `Start:`), so it required no logic change; its fixture + assertion
  were updated to the new shape, and its doc comment synced.
- **Not** yet exercised on a fresh `run --all` real banner — the emit is a single interpolated
  literal, low-risk, but the next full generate will show it live.

## Affected files

- `src/Genesis.Cli/Program.cs` (banner `Start:` line + explanatory comment)
- `gui/src/main/report.ts` (banner-shape doc comment synced)
- `gui/test/report.test.ts` (fixture banner + `startCommand` assertion updated to new shape)

## Follow-up (Group G2 Sonnet half, not done here)

Add the "Run app" button to the GUI completion screen. It should spawn `dotnet` with argv
`["run", "--project", outDir]` **directly (no shell)** — immune to all quoting/operator differences,
so it never re-introduces this class of bug regardless of the user's shell.
