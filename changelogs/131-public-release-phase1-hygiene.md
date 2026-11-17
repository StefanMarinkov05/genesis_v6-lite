**Type:** Feature / change (repo hygiene, public-release prep phase 1)
**Model:** Opus — first pass of a multi-phase public-release plan; judgement about repo shape, not pattern-copy.

## Summary

First phase of preparing the repo for a public GitHub release (two editions: full repo with music
for students, a music-free lite mirror for recruiters; Windows installer later). No UI changes.

## Techniques

- **LICENSE (MIT)** added — the repo had none, which legally means all-rights-reserved even when public.
- **CI never ran:** `.github/workflows/ci.yml` triggered on `main`, but the only branch is `master`.
  Both `push` and `pull_request` now target `master`.
- **Single version source:** `VERSION` (was 6.6.0, GUI said 6.6.6) set to `6.6.6` and stamped into
  the CLI/Core assemblies via a new `src/Directory.Build.props`. It lives in `src/`, not the repo root,
  because a root-level props file would be inherited by every generated app under `out/` (MSBuild
  walks up the directory tree). `dps-nn --version` now prints `6.6.6+<commit>`.
- **Internal working docs moved off the root:** `context.md` → `plan/handoff.md`, `tasks.md` →
  `plan/tasks.md`; references in `plan/state.md`/`plan/handoff.md` updated; CLAUDE.md routing table
  gained a "what's open right now" row.
- **Stale claims fixed:** CLAUDE.md said the GUI "does not exist yet" and listed it under "Not yet
  built"; `images/README.md` described it as an empty GUI drop folder, while it actually holds the
  ADR-011 fallback SVGs copied into generated apps.
- Pending changelog-130 work (rich FaceNote seed + `scripts/gen-rich-seed.py`) committed separately first.

## Affected files

`LICENSE`, `VERSION`, `src/Directory.Build.props`, `.github/workflows/ci.yml`, `CLAUDE.md`,
`images/README.md`, `plan/handoff.md` (moved), `plan/tasks.md` (moved), `plan/state.md`.

Verified: `dotnet build` 0 warnings/0 errors, `dotnet test` 121/121, `dps-nn --version` → `6.6.6+d5008ba`.
