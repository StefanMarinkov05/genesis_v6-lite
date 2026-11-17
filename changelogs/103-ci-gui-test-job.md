# 103 — CI: add a GUI test job to ci.yml

**Type:** Feature (tooling/infra)
**Model:** Sonnet, low effort, thinking off — mechanical addition to an already-working workflow,
no design decisions.

## Background

Changelog 099 shipped `.github/workflows/ci.yml` as a .NET-only build+test gate, deliberately
scoped narrow — Stefan chose to ship the simpler workflow first and prove it green on a real push
before adding a second job for the GUI's `node --test` suite. That follow-up was flagged in
`tasks.md` since.

## What shipped

New `gui-test` job in `ci.yml`, running alongside the existing `build-and-test` job (both trigger
on push/PR to `main`, independent — a GUI failure doesn't block the .NET job or vice versa).
`working-directory: gui` via `defaults.run` so every step step runs relative to `gui/` without
repeating `cd`. Steps: `actions/setup-node@v4` pinned to Node 24.x (matches local dev — no
`.nvmrc`/`engines` field existed to pin against, so this documents the convention for the first
time) with npm's built-in cache keyed off `gui/package-lock.json`; `npm ci`; then the 3 existing
package.json scripts in sequence — `typecheck` (`tsc --noEmit`), `build` (`electron-vite build`,
all 3 targets), `test:runner` (`node --test "test/**/*.test.ts"`, currently 42 tests).

No new scripts were added to `package.json` — the job just invokes what already existed, so a
local `npm run typecheck && npm run build && npm run test:runner` is the exact same sequence CI
runs.

## Verified

Ran all 3 commands locally exactly as the workflow invokes them (not just trusting the YAML):
`npm run typecheck` clean, `npm run build` all 3 electron-vite targets green, `npm run test:runner`
42/42 passing. Windows runner chosen to match the existing `build-and-test` job and this project's
Windows-specific dev workflow (LocalDB, `.bat` scripts) — same reasoning as changelog 099.

## Affected files

- `.github/workflows/ci.yml`
- `tasks.md`
