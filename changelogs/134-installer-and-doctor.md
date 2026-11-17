**Type:** Feature / packaging (public-release prep phase 4)
**Model:** Opus — new CLI command, install layout, installer + release workflow; verified by running the packaged build.

## Summary

DPS-NN can now ship as a Windows installer. `dps-nn doctor` checks the prerequisites the installer
cannot bundle and prints the fix for each gap; the GUI runs it at startup into its existing debug
terminal. The CLI publishes as a self-contained `dps-nn.exe` that sits next to its read-only
templates (install root) and writes everything to its cwd (workspace, `Documents\DPS-NN`) —
ADR-014. electron-builder builds an NSIS installer; `release.yml` builds it on a `v*` tag and
attaches it to a draft release. No visual UI changes.

## Techniques

- **`dps-nn doctor`** (`Diagnostics/Doctor.cs`): .NET 10 SDK, `dotnet-ef` ≥ 10, `dotnet-aspnet-codegenerator`,
  LocalDB (`sqllocaldb info` → MSSQLLocalDB), templates, writable workspace, API key (warning only).
  Exit 1 on any FAIL. Every process call goes through an injected runner, so 12 unit tests cover
  the parsing and verdicts without the real tools.
- **Install root vs workspace (ADR-014):** the CLI already looked for `templates/` next to its own
  exe first, so the only CLI change was moving `.image-cache/` from `GenesisRoot` to cwd. The GUI's
  `paths.ts` now has `installRoot()` (read-only: templates, footer, music) and `workspaceRoot()`
  (CLI cwd: logs, prompts, out, uploads, spec/seed lists); both are the repo root in dev. Packaged,
  the workspace gets copies of the sample spec + seed (the CLI writes `.lock.json` beside the spec).
- **Bug found by running the staged build — `DOTNET_ROOT`:** `identity` failed with "You must
  install .NET to run this application" from `dotnet-aspnet-codegenerator`. `dotnet run` sets
  `DOTNET_ROOT` for its child; the self-contained exe has no such parent, so tools could not find a
  per-user .NET install. New `Util/DotnetHost.EnsureDotnetRoot` sets it from the `dotnet` on `PATH`
  at CLI startup (3 tests). Recorded in `techniques.md`.
- **GUI:** packaged builds default the CLI command to the bundled `dps-nn.exe`; `GenesisRunner.doctor()`
  streams the report through the existing `build:onLog` channel (no new IPC); a run started while
  the doctor is still running waits for it (two `dotnet run` builds of the CLI collide on Windows
  file locks). The renderer preselects `project-spec.facenote.json` + `facenote-rich-seed.json`
  when nothing is chosen, so Start works on a fresh install with no key.
- **Packaging:** `gui/scripts/stage-genesis.mjs` stages `build/genesis/` (publish + copy of every
  file the CLI reads from `GenesisRoot`); music = only the **git-tracked** files under
  `design/music/` (52 files, ~970 MB), so a local build ships what CI ships; none → lite edition.
  `gui/electron-builder.yml` (electron-builder 25.1.8, NSIS, per-user, workspace never removed).
- **Release workflow** (`release.yml`, windows-latest): tag must equal `v`+`VERSION`; LFS objects
  restored through `actions/cache` (LFS bandwidth is metered); tests → stage → installer → draft
  release (publishing it triggers `handbook.yml`). Lite artifact name when the stage marker says lite.
- **Flaky test fixed:** `ParallelEmitTests.Every_item_runs_under_a_bulgarian_worker_name` failed under
  load (twice, sessions 34 and 35). Reproduced 30/30 with `taskset -c 0`: instantly completing
  items let the first lane drain the queue. Items now wait until a second lane has started; 0/30
  failures on one CPU. Test-only change.

## Verified

- `dotnet build` 0 warnings; `dotnet test` 158/158 + the fixed test 30/30 on one CPU; GUI typecheck,
  build, 52/52 tests (Node 24).
- `dotnet publish -r win-x64` self-contained single file: 75 MB.
- Staged `linux-x64` build, install root `chmod -R a-w`, pipeline run from a separate workspace:
  scaffold → emit-models → identity → emit-controllers-views passed, nothing written to the install
  root, lock file + image cache in the workspace.
- `electron-builder --win dir` on Linux (exe-resource editing skipped, needs Wine): correct
  `resources/genesis` + `app.asar` layout.
- Packaged **Linux** build launched under xvfb, driven over CDP: doctor report in the debug
  terminal; workspace created with the sample; spec/seed lists from the workspace; settings show the
  bundled CLI; sample preselected, Start enabled with no key; Start → full run through the bundled
  CLI to `seed`, 215 images cached in the workspace, then the expected LocalDB stop.

## Not verified

- The NSIS installer itself and anything Windows-specific (`dps-nn.exe`, LocalDB, Documents path,
  SmartScreen) — needs a Windows run: the `release.yml` dry run (workflow_dispatch) or `npm run dist`.
- Whether NSIS handles the ~1 GB full edition (makensis has a known mmap limit near 2 GB).

## Affected files

`src/Genesis.Core/Diagnostics/Doctor.cs` (new), `src/Genesis.Core/Util/DotnetHost.cs` (new),
`src/Genesis.Cli/Program.cs`, `src/Genesis.Core/Stages/SeedStage.cs`, `tests/Genesis.Tests/DoctorTests.cs`
(new), `tests/Genesis.Tests/CoreTests.cs`, `gui/src/main/{paths,settings,genesis-runner,ipc,dedup,footer,recent-projects,registry-parser}.ts`,
`gui/src/renderer/main.ts`, `gui/scripts/stage-genesis.mjs` (new), `gui/electron-builder.yml` (new),
`gui/package.json` + lock, `.github/workflows/release.yml` (new), `docs/adr/adr-014-install-root-and-workspace.md`
(new), `docs/how-to/build-the-installer.md` (new), `docs/reference/cli.md`, `docs/reference/file-map.md`
(regenerated), `docs/explanation/{gui-architecture,image-pipeline,architecture-decisions,techniques}.md`,
`docs/adr/adr-011-…md`, `scripts/build-handbook.py`, `README.md`, `CLAUDE.md`, `plan/state.md`.

## Found, not fixed

- `seed` hard-fails the whole stage when one stock-photo download hits the 100 s `HttpClient`
  timeout (seen once on a slow network). A re-run resumes from the cache, but a per-image
  fallback to the bundled SVG would be kinder to first runs.
