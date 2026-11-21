<!-- last-verified: ad7411e -->
# Project overview — Genesis at a glance

**Diataxis type:** explanation (orientation). This is the one-chapter summary of the whole project:
what it is, its two inputs, its technical specs, the decisions that shape it, and a briefing for
anyone — human or AI agent — who has to work on it. It holds no detail of its own; every section
links the doc that owns the facts.

## What it is

**Genesis** (executable `dps-nn`) is a code generator. You give it two JSON files — a **spec** that
describes a web application, and a **seed** that fills it with realistic sample data — and it
produces a **complete, building, seeded ASP.NET Core 10 MVC application** with a Bulgarian-only UI,
then proves the result works by building it, migrating its database, booting it and requesting its
pages. A desktop GUI wraps the generator; an agent mode drafts the spec from a plain-language
description and repairs it until the generated app passes.

It was built as a **diploma/sample-project factory for university software courses**: a student
brings a paper with requirements, the requirements become a spec (by hand, by an LLM from a
description, or by the agent loop), and the output is an app that is defensible line by line —
ordinary ASP.NET Core MVC, ordinary EF Core, nothing the student cannot explain. That origin is why
the hard requirements exist at all: every generated app must build, migrate, boot, log in, and show
no leftover English on the pages it fetches — or the run fails. The generator's core module is
called **Genesis** (`src/Genesis.*`); the product name changed to dps-nn later, and the internal
names deliberately stayed ([ADR-009](../adr/adr-009-dps-nn-rename-boundary.md)).

The audience shapes a second requirement. A generator like this is rarely used once: one developer
often delivers several projects to the same school, even the same class, where the submissions are
read side by side. Each project therefore resolves its own style, role names, theme, design preset
and seed content from its own seed, deterministically — one project never changes between runs,
two projects never look like one source. Style, naming, content and which features are enabled all
vary; the page structure still comes from one template set
([ADR-004](../adr/adr-004-variation-profile.md), [ADR-008](../adr/adr-008-design-overlay.md)).

## The two inputs

Everything else in the pipeline is derived from these. Keeping their responsibilities apart is the
single most useful mental model for this codebase:

| Input | Answers | Owner | Contract |
|---|---|---|---|
| `project-spec.json` | *What is the app?* — models, properties and their Bulgarian `displayName`s, validations, DTOs, identity (roles, seed users, Identity pages), areas, controllers and their provenance, views and page kinds, enabled features, row counts | a human, the `spec build` flow, or agent mode | [spec format](../reference/spec-format.md) + `spec.schema.json` |
| seed JSON (`prompts/in/seed-response.json`, `seeds/*.json`) | *What is in it?* — one array of realistic rows per model, parents linked to children by symbolic `refKey`, owners as `$seedUser:N`, image search phrases per image-bearing row | the `seed` LLM exchange, a hand-written file, or `utils/claude-skills/dps-nn-seed-authoring` | [seed format](../reference/seed-format.md) |

The spec is validated in two layers (JSON Schema, then semantic cross-checks) before anything is
generated; the seed is validated against a schema **derived from that spec**, so a seed is only ever
valid for the models it was written against.

## Technical specs

| Area | Spec |
|---|---|
| Version | 6.6.6 (single source: `VERSION`, stamped into the CLI) |
| Generator | C# / .NET 10, a console app (`dps-nn`) built on System.CommandLine, Scriban templates, JsonSchema.Net |
| Generated apps | ASP.NET Core 10 MVC, EF Core 10 on SQL Server LocalDB, ASP.NET Identity with Bulgarian error messages and pages, seeded users, roles and data |
| Pipeline | 9 stages in `PipelineRunner.CanonicalOrder`; 7 of them run inside `run --all`, every code-emitting one gated by `dotnet build` — [control flow](../reference/control-flow.md) |
| Inputs | `project-spec.json` validated against `spec.schema.json` ([spec format](../reference/spec-format.md)) and a seed JSON validated against a spec-derived schema ([seed format](../reference/seed-format.md)) |
| Reuse | 67 template files under `templates/` (63 Scriban `.sbn` + 4 data maps), 7 feature fragments under `templates/features/` and 2 fallback SVGs under `templates/static/` (cart, catalogues, follows, orders, personalization, posts, rating) |
| LLM | Anthropic Messages API, optional: every LLM step is a file exchange that works by hand without a key ([ADR-005](../adr/adr-005-llm-file-contract.md)) |
| Agent mode | `dps-nn create`: description → spec → run → repair loop on the spec only ([ADR-013](../adr/adr-013-agent-mode-spec-only-repair.md)) |
| GUI | Electron + TypeScript + electron-vite + xterm.js; spawns the CLI as a child process ([ADR-010](../adr/adr-010-gui-electron-cli-child.md)); 5 themes, 10 wallpapers |
| Distribution | Windows NSIS installer with a self-contained `dps-nn.exe`; full edition (with music) and lite edition ([build the installer](../how-to/build-the-installer.md)) |
| Prerequisites | .NET 10 SDK, SQL Server Express LocalDB, `dotnet-ef` 10.x, `dotnet-aspnet-codegenerator` — checked by `dps-nn doctor` |
| Quality gates | xUnit suite + GUI `node --test` suite, run by CI on Windows on every push and pull request; a generated app must pass `verify` |
| Docs | Diátaxis tree under `docs/`, 15 ADRs, this handbook built from the same Markdown by CI |
| License | MIT for code and docs; the recordings under `design/music/` are third-party and excluded |

## How a run flows

1. **spec-build** — an LLM turns free-form notes into `project-spec.json`. This is the `spec build`
   command, **not** part of `run --all`; the common path is that you (or agent mode) supply a spec
   file and the run starts at step 2.
2. **spec-expand** — resolves `$variation:` placeholders and feature fragments into
   `project-spec.lock.json`, injects an admin CRUD surface for every model, and derives seed-user
   passwords. Always re-runs first, ahead of the stage loop.
3. **scaffold** — `dotnet new mvc`, connection string, first build.
4. **emit-models** — entity classes, `DbContext`, validation attributes with Bulgarian messages.
5. **identity** — Identity scaffold, Bulgarian error describer and page text, seed users.
6. **emit-controllers-views** — scaffolded, templated and hand-written controllers and Razor views.
7. **seed** — seed data (LLM or manual) as symbolic references, images cached locally, migration
   applied.
8. **verify** — build → migrate → boot → HTTP smoke checks → English-leak scan of every page that answered.
9. **report** — `wwwroot/output/project-summary.json`, the machine-readable result.

Stages 2–9 record completion in `{out}/{Project}/plan/state.json`, so a failed run resumes at the
stage that failed. Exit codes are a contract: `0` success, `1` a stage failed (or `doctor`/agent
stopped), `2` bad usage, `3` an LLM exchange is waiting for a manual answer
([CLI reference](../reference/cli.md)). What each stage actually writes into the generated project:
[generated app anatomy](generated-app-anatomy.md).

## The decisions that shape it

One line each; the reasoning lives in [architecture decisions](architecture-decisions.md) and the ADRs.

| Decision | In one line | Record |
|---|---|---|
| Three-file state | The user's spec, the expanded lock file and run state never share a file, so re-runs are safe. | [ADR-001](../adr/adr-001-spec-lock-state-split.md) |
| Bulgarian Identity | Identity text is localized by a replacement map plus whole-page markup, not by translating at runtime. | [ADR-002](../adr/adr-002-identity-localization-static-map.md), [ADR-002b](../adr/adr-002b-identity-pages-wholesale.md) |
| Symbolic seed refs | Seed rows reference each other by name; the database assigns the keys. | [ADR-003](../adr/adr-003-seeding-symbolic-refs.md) |
| Deterministic variation | Code-style axes come from a hash of the project's own seed: different across projects, stable across re-runs. | [ADR-004](../adr/adr-004-variation-profile.md) |
| Distinctive look | Visual identity comes from a design-token overlay picked the same deterministic way, not per-template styling. | [ADR-008](../adr/adr-008-design-overlay.md) |
| LLM as a file contract | Request and response are files, so the manual and automatic paths are identical and auditable. | [ADR-005](../adr/adr-005-llm-file-contract.md) |
| Regex deviations | Scaffolder output is patched by regex on known shapes, not a Roslyn rewriter. | [ADR-006](../adr/adr-006-scaffold-deviations-regex.md) |
| Soft delete | A global query filter plus an audit base class, so no query can forget it. | [ADR-007](../adr/adr-007-soft-delete-audit.md) |
| Design tokens | Distinct project looks come from a token overlay, not per-template styling. | [ADR-008](../adr/adr-008-design-overlay.md) |
| Rename scope | dps-nn / ДПС-НН is the user-visible name; internal namespaces stay `Genesis.*`. | [ADR-009](../adr/adr-009-dps-nn-rename-boundary.md) |
| GUI over CLI | The GUI spawns the CLI and reads its log; there is one pipeline implementation. | [ADR-010](../adr/adr-010-gui-electron-cli-child.md) |
| Local images | Images are downloaded once, cached and served from the app; never hot-linked. | [ADR-011](../adr/adr-011-image-pipeline-cache-and-fallbacks.md) |
| Specifications page | Small lookup models share one admin page ("Спецификации"); list filters are derived automatically. | [ADR-012](../adr/adr-012-specifications-consolidation-and-filters.md) |
| Agent repairs the spec | The LLM may change the spec and seed, never generated code; gaps become a report. | [ADR-013](../adr/adr-013-agent-mode-spec-only-repair.md) |
| Install root vs workspace | The installed program is read-only; everything written goes to `Documents\DPS-NN`. | [ADR-014](../adr/adr-014-install-root-and-workspace.md) |

## Repository map

| Path | Holds |
|---|---|
| `src/Genesis.Core/` | The engine: `Spec/` (model + two-layer validation), `Variation/`, `Pipeline/` (stage runner, state, process wrapper), `Stages/` (one class per stage), `Llm/`, `Agent/`, `Diagnostics/` (`doctor`), `Cli/` (pickers), `Util/` (template engine) — module by module in [components](../reference/components.md) |
| `src/Genesis.Cli/` | The `dps-nn` command line — argument parsing only, no pipeline logic |
| `tests/Genesis.Tests/` | xUnit suite (agent loop, stage logic, doctor, spec/semantic rules) |
| `templates/`, `templates/features/` | What generated apps are made of |
| `spec.schema.json`, `fixtures/`, `seeds/` | The spec's schema, the FaceNote sample spec (bg + en), its seed data |
| `utils/claude-skills/` | The two portable authoring skills for manual spec/seed work in a chat; the spec one doubles as the agent's drafting prompt |
| `gui/` | The Electron app: `src/main/` (processes, paths, IPC), `src/renderer/` (UI, themes, i18n), `scripts/` (packaging) |
| `scripts/` | `generate-file-map.ps1`, `build-handbook.py`, `lite-tree.py`, `gen-rich-seed.py` |
| `prompts/`, `logs/`, `.image-cache/` | LLM exchange files + the permanent spec/seed archive; run registry and action logs; downloaded-image cache |
| `docs/` | Tutorial, explanation, reference, how-to, ADRs — the source of this handbook |
| `plan/`, `changelogs/` | Working history: session log, decisions, one changelog entry per change |
| `.github/workflows/` | `ci.yml`, `handbook.yml`, `release.yml`, `lite-mirror.yml` |

Every file under `src/` carries a one-line `// FILE-MAP:` header; those are collected into the
generated [file map](../reference/file-map.md). It covers `src/` only — templates, the GUI and the
scripts are mapped at module level in [components](../reference/components.md).

## Briefing for agents working on this codebase

Read this before changing anything. It is the model of the system that every other doc assumes.

**The mental model.** Two JSON files go in — a spec (shape) and a seed (contents); a generated
project comes out under `out/`. The generator is deterministic apart from the LLM steps, and the
LLM steps are files. So almost every bug is one of: a template emits wrong code, a stage transforms
the spec/seed wrongly, or the spec asks for something the templates cannot express. Find which of
the three before writing a fix.

**The invariants — break one and the change is wrong:**

- **Never edit generated output** under `out/`. Fix the template or the stage that produced it.
- **Bulgarian-only UI.** Every non-key model property needs a `displayName`; `verify` fails a
  project whose pages leak English.
- **Every code-emitting stage ends in a build gate.** A stage that emits code it did not build is
  incomplete.
- **Stages are idempotent and resumable.** A stage must be safe to re-run after a crash.
- **The GUI never reimplements the pipeline.** It spawns the CLI and parses its log
  (`dps-nn.log.jsonl`); new behavior goes into the CLI first.
- **The agent never edits generated code.** A capability the spec cannot express is reported, not
  hacked into `out/`.
- **The spec's shape is owned by `spec.schema.json`.** Change the schema, the loader and
  `spec-format.md` together.

**How work is recorded.** Every change gets a numbered entry in `changelogs/`; `plan/state.md` logs
each session; `plan/decisions.md` keeps the history of every bug and the theories already tried.
Read those before proposing a fix for a recurring problem: a theory recorded as failed must not be
proposed again.

**How to verify a change.** Build and test the solution (`dotnet build`, `dotnet test`), run the
GUI tests if `gui/` changed, and for anything that affects generated apps run the full pipeline
on the FaceNote fixture: `dotnet run --project src/Genesis.Cli -- run --all --spec
fixtures/project-spec.facenote.json`. A change is done when `verify` passes, not when it compiles. Every bug fix follows the
[testing policy](testing-policy.md): a failing test first, then the fix, then the fix reverted to
prove the test catches it.

**Where to look:**

| Task | Read |
|---|---|
| Orient in the generator's modules | [components](../reference/components.md), then the generated [file map](../reference/file-map.md) |
| Understand what a run produces | [generated app anatomy](generated-app-anatomy.md) |
| Change pipeline order or a stage | [control flow](../reference/control-flow.md), [add a stage](../how-to/add-a-stage.md) |
| Change what generated apps contain | [add a template](../how-to/add-a-template.md), [techniques and traps](techniques.md) |
| Change the spec's shape | `spec.schema.json`, [spec format](../reference/spec-format.md) |
| Change the seed's shape or content rules | [seed format](../reference/seed-format.md), `Stages/SeedStage.cs` |
| Work on agent mode | [agent mode](agent-mode.md) |
| Work on the GUI | [GUI architecture](gui-architecture.md) |
| Package or release | [build the installer](../how-to/build-the-installer.md), [the lite mirror](../how-to/mirror-the-lite-edition.md) |
| Understand a past decision | [architecture decisions](architecture-decisions.md) and the ADR it links |

## Authorship

Designed and built by Stefan Marinkov. Code and documentation are MIT-licensed; see `LICENSE`.
