<!-- last-verified: e95fedf -->
# Tutorial — Genesis end to end

**Diataxis type:** tutorial (one-time learning). This is a linear walkthrough — no reference
tables, no rationale. For the *why* behind any step, follow the links at the end rather than
looking for it here.

## 1. Prerequisites

- **.NET SDK 10.0.100+** (pinned via `global.json`).
- **SQL Server or LocalDB**, reachable via the generated app's `DefaultConnection`.
- **The Identity scaffolder**: `dotnet tool install -g dotnet-aspnet-codegenerator`.
- **`dotnet-ef`, matching the generated project's EF Core 10.0.\* packages**:
  `dotnet tool update -g dotnet-ef --version 10.0.*` (use `tool install` instead if it isn't
  installed at all).

If `dotnet-ef` is outdated, it can still build the generated project successfully and only then
fail with `the specified deps.json does not exist` — that message means the tool itself is too old
for a net10.0 project, not a problem with what was generated. If you hit it, `docs/explanation/
techniques.md` has the full story before you try anything else.

## 2. Build the tool itself

From the repo root (assemblies stay `Genesis.*` by design — see `docs/explanation/
architecture-decisions.md`):

```bash
dotnet build
dotnet test
```

`dps-nn.slnx` is already checked in — nothing to create first.

## 3. Generate a project from the fixture spec

```bash
dotnet run --project src/Genesis.Cli -- run --all --spec fixtures/project-spec.facenote.json --out out
```

Your working directory must be the repo root — `templates/` and `spec.schema.json` resolve
relative to it. Watch the console: each of the nine stages prints a `=== stage: <name> ===` line as
it starts, and the run ends with a stage-timings summary.

**If it exits with code 3**, an LLM stage (usually `seed`) is waiting for a response and printed
instructions. Three ways forward:
- Use the bundled FaceNote seed: copy `seeds/facenote-rich-seed.json` to
  `prompts/in/seed-response.json` and re-run with `--resume` — no key needed.
- Set `ANTHROPIC_API_KEY` before running, and it resolves automatically next time (Haiku for seed data, Sonnet for spec exchanges).
- Or follow the printed instructions to paste a response into the indicated
  `prompts/in/<stage>-response.json`, then re-run the same command with `--resume`.

## 4. Watch it pass the `verify` gate

Stage 8 (`verify`) is the one that actually proves the generated app works: a clean build, EF
migrations applied, the app booted on a free port, every public and admin route fetched (the admin
one via a real seeded-user login), and — for a Bulgarian project — a scan confirming no English
text leaked into the rendered pages. Any failure here stops the run with `state.json` **not**
marked done for that stage, so re-running `run --all` retries it.

## 5. Open the generated app

A successful run ends with a banner: the project name, its local URL, and every seeded user's
email/password. From the repo root:

```bash
cd out/FaceNote
dotnet run
```

Then open the printed URL and log in with one of the seeded credentials from the banner.

## 6. Where the run left its trail

- `out/FaceNote/dps-nn.log.jsonl` — the structured stage-boundary log (what the GUI's progress bar
  tails).
- `out/FaceNote/plan/state.json` — which stages completed (what makes a re-run resumable).
- `logs/registry.json` — maps this project back to the exact spec + seed file that produced it.
- `prompts/out/schemas/facenote-schema.json` + `prompts/out/seeds/facenote-seed.json` — a permanent
  copy of the spec and seed used, archived automatically on a successful run.
- `wwwroot/output/project-summary.json` (inside the generated project) — the machine-readable
  report, if the spec's `outputReport` section is configured.

## Where to go next

- The full 9-stage breakdown (Mermaid diagram, stage table, verify-gate detail):
  `docs/reference/control-flow.md`.
- *Why* it's staged and verify-gated this way: `docs/explanation/architecture-decisions.md`.
- The spec's concept groups, if you're about to write your own: `docs/reference/spec-format.md`.
- Adding a stage or a template: `docs/how-to/`.
- If something breaks in a way this tutorial didn't cover: `docs/explanation/techniques.md` first,
  then `plan/decisions.md` for the dated history of prior bugs and fixes.
