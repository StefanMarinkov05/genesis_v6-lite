<!-- last-verified: 8d6ccce -->
# How to build the Windows installer

**Diataxis type:** how-to. Layout and the reasoning behind it: [ADR-014](../adr/adr-014-install-root-and-workspace.md).

## Release (the normal path)

1. Set the version in `VERSION` (and `gui/package.json` for consistency); commit.
2. Tag and push: `git tag v6.6.6 && git push origin v6.6.6` — the tag must equal `v` + `VERSION`.
3. `.github/workflows/release.yml` runs on `windows-latest`: tests, stages, builds the NSIS
   installer and attaches it to a **draft** release.
4. Review the draft's notes on GitHub and publish it. Publishing triggers `handbook.yml`, which
   attaches the Architecture Handbook PDF.

Dry run without a release: run the workflow manually (Actions → Release → Run workflow); the
installer is uploaded as a workflow artifact.

## Locally (Windows)

```powershell
cd gui
npm ci
npm run dist          # build GUI → stage build\genesis → electron-builder --win
```

The installer lands in `gui\dist\DPS-NN-Setup-<version>.exe`.

## What gets staged

`gui/scripts/stage-genesis.mjs` builds `build/genesis/`, which becomes
`<install>\resources\genesis\`:

- `dps-nn.exe` — `dotnet publish` self-contained single file (`win-x64`).
- Everything the CLI reads from its `GenesisRoot()`: `templates/`, `feature-templates/`,
  `spec.schema.json`, `images/`, `utils/claude-skills/dps-nn-spec-authoring/`.
- The sample: `fixtures/project-spec.facenote.json`, `seeds/facenote-rich-seed.json`.
- `design/footer.md`, `LICENSE`, `VERSION`.
- **Full edition only:** the git-tracked files under `design/music/` (untracked local files are
  never shipped). With none present — the lite mirror, or `--no-music` — it builds the lite edition.

If you add a new file the CLI reads at runtime, add it to the list in `stage-genesis.mjs`.

## Checking a build

- `build\genesis\dps-nn.exe doctor` from any folder: `templates` must point into `build\genesis`
  and `workspace` at the current folder.
- Install, launch, open the debug terminal: the doctor report is there. The FaceNote sample is
  preselected; Start should run to completion on a machine where the doctor reports no `[FAIL]`.
- Generated projects, logs and prompts appear under `Documents\DPS-NN\`, never in the install folder.

## Known limits

- The installer is unsigned: Windows SmartScreen warns on first run ("More info" → "Run anyway").
- The full edition is ~1 GB (the music is ~970 MB). LFS objects are cached between workflow runs
  to save LFS bandwidth.
