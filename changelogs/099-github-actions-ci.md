# 099 — GitHub Actions CI: build+test gate on push/PR to main

**Type:** Feature (tooling/infra)
**Model:** Sonnet, low effort — straightforward workflow file, no pipeline behavior touched.

## Background

Two infra threads had been left as open discussion (not decided) across recent sessions: wrapper
shell scripts to reduce repeated regen/test invocations, and whether to add CI/CD at all.

- **Wrapper scripts**: skipped. `1-build-and-check.bat`/`2-generate.bat` already exist and cover
  the repeated-invocation pain point for a solo repo; further wrapping would be marginal
  convenience, not worth building.
- **CI/CD**: chose GitHub Actions over Jenkins — zero extra infra, already on GitHub, solo repo so
  Jenkins buys nothing. For "Claude in the pipeline," went with the low-risk half only (build/test
  gate); auto-fix-and-push was explicitly rejected as too risky without heavy guardrails, given no
  PR-review culture in this repo today.

## What shipped

New `.github/workflows/ci.yml`: triggers on push/PR to `main`, runs on `windows-latest` (matches
the project's Windows-specific dev workflow — LocalDB, `.bat` scripts, cp65001 codepage handling).
Three steps: `dotnet restore`/`build`/`test` against `dps-nn.slnx` (Release config), using the SDK
version pinned in `global.json` (10.0.100 via `actions/setup-dotnet@v4`, `10.0.x`).

**Scope, deliberately narrow:**
- Covers `tests/Genesis.Tests` only (120 .NET tests) — no GUI `node --test` job (32 tests) yet;
  Stefan chose to ship the simpler workflow first and prove it green on a real push before adding
  a second job.
- No LocalDB/real-pipeline-generation step — that stays a manual gate (`2-generate.bat`), same as
  before; a GitHub-hosted runner can't provision LocalDB or exercise the full CLI generation loop
  cheaply.
- No Claude step of any kind (no auto-review, no auto-fix) — pure green/red build+test gate only.

## Not yet done / next candidates

- GUI `node --test` as a second job (straightforward follow-up once this workflow is proven).
- A low-risk Claude step (PR/commit summary) — still on the table per the original framing, not
  started.

## Affected files

- `.github/workflows/ci.yml` (new)
