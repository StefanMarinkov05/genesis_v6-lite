<!-- last-verified: 8d6ccce -->
# ADR-014: Installed layout — a read-only install root and a writable workspace

**Status**: accepted (changelog 134)

**Context**: Until the Windows installer, dps-nn only ever ran from a repo checkout, where one
folder played two roles: the CLI read `templates/`, `spec.schema.json`, `templates/features/` from
its `GenesisRoot()`, and wrote `logs/`, `prompts/`, `out/`, the image cache and the spec's
`.lock.json` relative to its cwd — and the GUI (ADR-010) always spawned it with cwd = that same
root. An installed app cannot work that way: the install directory is the program's, it may be
read-only, it is replaced on every update, and users expect their generated projects in Documents.
The CLI also ran as `dotnet run --project src/Genesis.Cli`, which needs the source tree.

**Decision**: Two roots, kept apart everywhere.
- **Install root** (read-only): `<install>\resources\genesis\` — a self-contained single-file
  `dps-nn.exe` next to `templates/` (with `features/` and `static/` inside), `spec.schema.json`, the
  spec-authoring guide, the FaceNote sample spec + seed, `design/footer.md`, and (full edition
  only) `design/music/`. The CLI finds it through `AppContext.BaseDirectory` — the lookup
  `GenesisRoot()` already tried first — so the CLI needed no install-mode flag.
- **Workspace** (writable): `Documents\DPS-NN\`, the CLI child's cwd. Everything written goes
  here: `logs/`, `prompts/`, `out/`, `.image-cache/` (moved from `GenesisRoot` to cwd for this),
  uploads, and copies of the sample spec/seed — copied, because the CLI writes the spec's
  `.lock.json` beside it.
- In a repo checkout both roots are the repo root, so development behaves exactly as before.
- The GUI resolves them in `gui/src/main/paths.ts` (`installRoot()`, `workspaceRoot()`), switching
  on `app.isPackaged`; a packaged build defaults its CLI command to the bundled `dps-nn.exe`.
- Prerequisites the installer cannot bundle (.NET 10 SDK, `dotnet-ef`,
  `dotnet-aspnet-codegenerator`, LocalDB — the generated app is built and migrated with them) are
  checked by `dps-nn doctor`, which the GUI runs at startup into its debug terminal.

**Consequences**:
- An installed GUI starts ready to generate the sample without an API key (sample spec + seed are
  preselected) and without the repo or Node.
- The self-contained exe has no `dotnet` parent, so nothing set `DOTNET_ROOT` for the global tools
  it launches; with a per-user .NET install they could not find the runtime. The CLI now infers
  `DOTNET_ROOT` from the `dotnet` on `PATH` at startup (`DotnetHost`), which is what `dotnet run`
  did implicitly.
- New read-only runtime inputs must be added to `gui/scripts/stage-genesis.mjs`, or the installed
  build will not have them — every `Path.Combine(genesisRoot, ...)` is a staging obligation.
- Uninstalling never touches the workspace; the user's projects and logs survive updates.

**Alternatives considered**:
- *Write into the install directory* (per-user install is technically writable) — rejected: an
  update would delete the user's projects, and a per-machine install would fail outright.
- *A `--workspace` CLI option* — unnecessary: cwd already *is* the workspace contract (ADR-010);
  only the image cache was keyed to the wrong root.
- *Framework-dependent CLI + bundled source* — rejected: needs a build on first launch, and
  `dotnet run` startup on every invocation.

**Related**: ADR-010 (GUI spawns the CLI), ADR-011 (image cache), `docs/how-to/build-the-installer.md`.
