**Type:** Feature (agent mode — public-release prep phase 2)
**Model:** Opus — new subsystem with a design decision (spec-only repair, ADR-013), not a pattern-copy.

## Summary

`dps-nn create` turns a free-text project description into a running app on its own: an LLM
drafts the spec, the pipeline runs, and on failure the LLM repairs the **spec** (never the
generated code) and the run starts over — at most `--max-repairs` rounds. Asks the generator
can't express land in `meta.unsupportedRequests` and `agent-report.json` as a backlog for new
feature-templates. The GUI gets one description textarea + one button in the existing
Specification panel (explicitly allowed; nothing else in the UI changed).

## Techniques

- **`AgentRunner`** (`src/Genesis.Core/Agent/`) is pure orchestration over delegates (author,
  pipeline, validator, reset, log), so every branch is unit-tested without dotnet or a key.
- **Validation errors reach the model:** `SpecValidationException.Message` omits the per-rule
  errors (they're in `.Errors`/`ToString()`), so the loop sends `ToString()`.
- **Environment triage before repair:** outdated/missing `dotnet-ef`, missing scaffolder, no
  LocalDB, no SDK, rejected API key stop the loop at once with the fix (matched on the raw error,
  before condensing). No spec edit fixes those; retrying would only burn tokens.
- **Condensing:** build failures embed full STDOUT/STDERR; >6000 chars → first line + distinct
  error/`CSxxxx` lines (max 60).
- **Reset between rounds:** the old spec's project dir (only inside `--out`) + lock file are wiped
  so a repaired spec regenerates from scratch instead of resuming a half-built project.
- **`PipelineRunner.FailedStage`** — the stage that threw, so the agent can tell the model where.
- **Models:** `LlmModels` — Sonnet 5.5 for spec exchanges, Haiku 5.5 for seed; overridable via
  `DPS_NN_MODEL_SPEC`/`DPS_NN_MODEL_SEED`. `LlmBridge` gained `ANTHROPIC_BASE_URL`, a 10-min HTTP
  timeout (default 100 s is too short for a full spec) and `max_tokens` 32000.
- **Prompt source:** the repo's own `utils/claude-skills/dps-nn-spec-authoring/SKILL.md` (one guide
  for humans-in-a-chat and the agent) + the FaceNote fixture as a verified reference spec + a
  preamble overriding the guide's "ask the user" steps.
- **`run --all` body extracted** into `RunPipelineAsync` so `run` and `create` share one path.
- **GUI:** `build:create` IPC channel → `GenesisRunner.create` writes `description.txt` and spawns
  `create --describe-file`. The project name is unknown until the spec exists, so the tail polls
  the spec for `meta.projectName`, re-targets on rename, and restarts when the log is recreated.
  Error screen shows the agent's own stop reason. Helpers in Electron-free `agent-helpers.ts`.
- Strings added to the 6 hand-written i18n tables (joke tables derive from EN).
- **Pre-existing bug fixed — explicit exit codes never reached the shell.** `Program.cs` ended with
  `return await root.InvokeAsync(args)`; System.CommandLine returns 0 for void handlers and Main's
  return value overrides `Environment.ExitCode`, so every `Environment.ExitCode = 2/3` was lost and
  the CLI exited 0. The exit-3 "LLM pending" signal the GUI keys `build:awaiting-llm` off had never
  surfaced; thrown failures were unaffected (the exception middleware returns 1). Now: the invoke
  result if non-zero, else `Environment.ExitCode`. Caught by the agent e2e run printing
  "stopped" with exit 0. Verified for real: no description → 2, no key → 2, `spec build` without a
  key → 3, agent environment stop → 1, `--version` → 0.

## Affected files

`src/Genesis.Core/Agent/{AgentRunner,LlmSpecAuthor}.cs` (new), `src/Genesis.Core/Llm/LlmModels.cs`
(new), `src/Genesis.Core/Llm/LlmBridge.cs`, `src/Genesis.Core/Pipeline/Pipeline.cs`,
`src/Genesis.Core/Spec/ProjectSpec.cs`, `spec.schema.json`, `src/Genesis.Cli/Program.cs`,
`tests/Genesis.Tests/AgentTests.cs` (new), `gui/src/main/{genesis-runner,ipc,agent-helpers}.ts`,
`gui/src/preload/index.ts`, `gui/src/shared/ipc-types.ts`, `gui/src/renderer/{index.html,main.ts,styles.css}`,
`gui/src/renderer/i18n/{en,bg,de,ru,romani,tr}.ts`, `gui/test/agent-helpers.test.ts` (new),
docs: ADR-013 (new), `docs/explanation/agent-mode.md` (new), `control-flow.md`, `spec-format.md`,
ADR-005, `architecture-decisions.md`, `gui-architecture.md`, README, tutorial, CLAUDE.md.

## Verified

- `dotnet build` 0 warnings / 0 errors; `dotnet test` 143/143 (22 new agent tests).
- GUI: `npm run typecheck` clean, `npm run build` OK, `node --test` 52/52 under Node 24 (3 new).
- End to end, `dps-nn create` against a local Anthropic stub (`ANTHROPIC_BASE_URL`; no real key on
  this Linux box): draft with a planted bad relation → attempt 1 fails at `spec-validate` → repair
  (Sonnet, error included) → scaffold, emit-models, identity, emit-controllers-views all build →
  seed (Haiku) → `ef database update` fails on missing LocalDB → **environment stop**, exit 1,
  `agent-report.json` with both attempts + the `unsupportedRequests` list. Re-running reused the
  spec (resume). Not verified: a real API key, a successful verify on Windows/LocalDB, on-screen GUI.

## Found, not fixed

- `fixtures/seed-response.facenote.json` predates the current seed schema (no `refKey`, 10 models
  missing) — the e2e stub failed seed validation 3× with it. `seeds/facenote-rich-seed.json` is current.
- GUI `npm run test:runner` needs Node ≥ 23.6 (native `.ts` stripping); Node 22 fails all files with
  `ERR_UNKNOWN_FILE_EXTENSION`. CI uses 24. Worth an `engines` field later.
- `ParallelEmitTests.Every_item_runs_under_a_bulgarian_worker_name` is flaky under CPU load (30
  instant tasks can all drain through one lane) — failed once during a parallel build, then 3/3 passes.
