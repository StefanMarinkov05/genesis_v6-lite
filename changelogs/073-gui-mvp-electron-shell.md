# 073 — GUI MVP: Electron shell over the Genesis CLI (spec B.0 phase 1)

**Type:** Feature / change (first real GUI app code — Task B of `plan/dps-nn-6.5.0-spec.md`)
**Model:** Opus — the session spanned architecture-adjacent wiring (process lifecycle, IPC
contract, progress reconstruction against the CLI's on-disk log/state) where getting the
CLI-contract details wrong is a one-way door; routing rubric puts that on Opus even though
individual pieces (theme port, i18n) are Sonnet-grade.

## Summary

Built the first working GUI: an Electron shell that wraps the existing headless Genesis CLI
as a child process, per `docs/explanation/gui-architecture.md` (+ ADR-010) and the NOVA token
system in `design/design-specs.md`. Scope is exactly spec B.0 phase 1 — **NOVA theme only,
EN only**: spec/seed input (upload + list-select), GUI-side dedup, Start/Stop, stage-granular
progress, an embedded xterm.js debug terminal, and the completion/error generation screens,
wired end-to-end to `dps-nn run --all … --no-interactive`. Pause/Resume and the "Claude" seed
button are declared in the IPC surface but **UI-disabled** (phase 2), as the phasing requires.

The GUI never reimplements pipeline logic (the inviolable rule): it spawns one `run --all`,
tails `{out}/{Project}/dps-nn.log.jsonl` for progress (reconciled with `state.json` baseline),
pipes stdout/stderr to the terminal, and resolves by exit code (0 → parse completion banner;
3 → awaiting-LLM; other → error). The API key lives only in the main process and is injected
into the child env, never crossing to the sandboxed renderer.

## What was verified

- `tsc --noEmit` clean; `electron-vite build` produces all three targets (main/preload/renderer).
- Completion-banner parser unit-tested (`node --test`, 3/3) against a real `PrintCompletionBanner`
  layout — the bug-prone logic that turns captured stdout into the report readout.
- The real CLI's `run --help` confirms the exact invocation the runner builds
  (`--all --from --spec --out --no-interactive`).

**Not yet verified (Stefan's manual step, `cd gui && npm run dev`):** the visual render in a
real window, and a live end-to-end generation (needs a display + LocalDB + a full multi-minute
pipeline run — not runnable in the build session). The wiring is contract- and build-verified;
the on-screen + live-generation confirmation is the remaining gate before phase 2.

## Techniques

- **electron-vite + vanilla TS** (no renderer framework) — mirrors the vanilla `design/preview.html`
  and keeps the dependency surface tiny (electron, electron-vite, @xterm/xterm, @xterm/addon-fit).
- **No node-pty**: the debug terminal only *displays* piped stdout/stderr, so xterm.js in
  write-only mode suffices — avoids native compilation entirely.
- **Progress reconstruction**: poll-tail the jsonl from the spawn-time byte offset (the log
  persists across runs and is never reset), increment on each `"end"` phase, seed the index from
  the `state.json` completed-stage baseline so a resume shows an accurate bar.
- **Dedup** is GUI-side only (`sha256(spec)+sha256(seed)` → `logs/gui-build-manifest.json`),
  reusing the *idea* of the CLI registry without touching the CLI.
- Frameless window; the min/max/close controls are real renderer DOM (R6), styled per NOVA.

## Affected files

- New `gui/` app: `package.json`, `electron.vite.config.ts`, `tsconfig.json`, `.gitignore`.
- `gui/src/shared/ipc-types.ts` — the typed GUI↔CLI IPC contract (single source of truth).
- `gui/src/main/` — `index.ts`, `ipc.ts`, `paths.ts`, `settings.ts`, `dedup.ts`, `footer.ts`,
  `report.ts`, `files.ts`, `genesis-runner.ts`.
- `gui/src/preload/index.ts` — the contextBridge surface.
- `gui/src/renderer/` — `index.html`, `main.ts`, `styles.css` (NOVA port), `state.ts`,
  `env.d.ts`, `i18n/en.ts`.
- `gui/test/report.test.ts` — banner-parser tests.
- `docs/explanation/gui-architecture.md` — added the `shell:openExternal`/`shell:openPath`
  channels and the `outDir` field on `build:done` (same-commit rule for the two new channels).
