# 084 — GUI: "Run app" button on the completion screen

**Type:** Feature
**Model:** Sonnet, low effort, thinking off — Group G2 Sonnet half, paired with 083's Opus
diagnosis (the PowerShell-5.1 `&&` root cause).

## Summary

Added a "Run app" button to the report/completion screen, next to "Open in browser" / "Open
folder" / "Close". Launches the just-generated app directly from the GUI instead of requiring
Stefan to copy the `Start:` command out of the readout and paste it into a terminal — which is
exactly the workflow step that changelog 083 found broken on his default shell (PowerShell 5.1
choking on `&&`).

## Implementation

Deliberately does **not** reuse or shell out the CLI's own `Start:` command text at all — it
spawns `dotnet run --project <dir>` as **argv, `shell: false`** (mirrors the exact pattern
`GenesisRunner.spawnChild` already uses for the CLI child process). This sidesteps the whole
class of shell-operator/quoting bug from 083 structurally: there is no shell parsing this
command on any platform, so it can never again break because of `&&`, quoting, or the Cyrillic
path, regardless of what shell the user has.

- `gui/src/shared/ipc-types.ts` — new `runApp(projectDir)` on `GuiApi`, channel
  `shell:runApp`.
- `gui/src/main/genesis-runner.ts` — new `runGeneratedApp(projectDir)`: spawns
  `dotnet run --project <dir>`, `cwd` set to the project dir, detached + `unref()`'d so the
  dev server outlives the GUI if the GUI closes, `stdio: 'ignore'` (fire-and-forget — the GUI
  doesn't own this process's lifecycle the way it owns the pipeline's).
- `gui/src/main/ipc.ts` — handler wired next to the existing `shell:*` handlers.
- `gui/src/preload/index.ts` — exposed on the typed bridge.
- `gui/src/renderer/index.html` / `main.ts` / `i18n/en.ts` — new `report-run-app` button,
  primary style, disabled when `report.outDir` is absent (same disabled-gating pattern as
  `report-open-browser`'s `!report.url` check).
- `docs/explanation/gui-architecture.md` — IPC channel surface doc updated in the same commit
  (same-commit rule).

## Verified

`tsc --noEmit` clean, `electron-vite build` all 3 targets, `node --test` 3/3 (unchanged —
this feature has no unit-testable pure logic, it's an IPC wire + a spawn call). Launched
`npm run dev` for real and confirmed a genuine Electron window process started with no
console/runtime errors (`Get-Process` showed 4 live `electron` processes, main+renderer+
GPU/utility, then cleaned up). Full click-through (does the button actually boot a real
generated FaceNote app end-to-end) is left to Stefan's manual gate per usual for GUI work —
no generated project exists in this session to click "Run app" against.

## Affected files

- `gui/src/shared/ipc-types.ts`
- `gui/src/main/genesis-runner.ts`
- `gui/src/main/ipc.ts`
- `gui/src/preload/index.ts`
- `gui/src/renderer/index.html`
- `gui/src/renderer/main.ts`
- `gui/src/renderer/i18n/en.ts`
- `docs/explanation/gui-architecture.md`
