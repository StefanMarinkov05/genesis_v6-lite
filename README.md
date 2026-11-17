<!-- last-verified: f624752 -->
# ДПС-НН (dps-nn)

> **Lite edition.** The same code as [StefanMarinkov05/genesis_v6](https://github.com/StefanMarinkov05/genesis_v6), minus the
> ~1 GB of music the GUI's IMPERATOR theme can play. Mirrored automatically from there on every
> push - please open issues and pull requests in the full repo.

**Describe a web app in a paragraph — get a complete, seeded, verified ASP.NET Core 10 MVC application.**

[![CI](https://github.com/StefanMarinkov05/genesis_v6/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/StefanMarinkov05/genesis_v6/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Electron](https://img.shields.io/badge/GUI-Electron-47848F)

ДПС-НН generates full-stack ASP.NET Core 10 MVC + EF Core (SQL Server) applications with a
Bulgarian-only UI — models, Identity with roles, admin and client areas, controllers, views, seed
data with images — from one declarative `project-spec.json`. You can write that spec yourself, or
paste a plain-language description and let Claude write it: the agent runs the generator, reads
what failed, fixes the spec and tries again.

It was built as a sample-project factory for a university software course: every generated app
must build, migrate, boot, log in, render every page, and contain no leftover English — or the run
fails. The internal namespace is still `Genesis.*` (the project's original name; see ADR-009).

```mermaid
flowchart LR
  desc["description\n(any language)"] -->|"agent drafts"| spec["project-spec.json"]
  you["you"] -->|"or write"| spec
  spec --> pipe["9-stage pipeline\nexpand · scaffold · models · identity\ncontrollers+views · seed · verify · report"]
  pipe -->|"verified"| app["running, seeded\nASP.NET MVC app"]
  pipe -->|"failed: error → agent\nrepairs the spec"| spec
```

## Highlights

- **An agent with a narrow lever.** In agent mode the LLM's only output is the spec; the
  generator's validation, build and verify failures are what it observes. It never edits generated
  code, so every app stays pure template output. Requests the generator can't express are reported
  (`meta.unsupportedRequests`) instead of hallucinated, and machine problems (missing SDK, no
  LocalDB) stop the loop instead of burning tokens. → [ADR-013](docs/adr/adr-013-agent-mode-spec-only-repair.md)
- **"Done" means verified, not "it compiled".** The `verify` stage builds, applies migrations,
  boots the app, logs in through the real form as the seeded admin, fetches every public and
  authenticated route, and scans every rendered page for English text.
- **Resumable, idempotent pipeline.** Every stage checkpoints to `state.json`, every code-emitting
  stage ends in a `dotnet build` gate, and `--from <stage>` re-runs from any point.
- **LLMs behind a file contract.** Every LLM exchange is a request/response file pair, so it works
  with an API key (automatic) or without one (paste into any chat), is schema-validated with
  retries, hash-logged, and replayable as a test fixture. → [ADR-005](docs/adr/adr-005-llm-file-contract.md)
- **Deterministic variation.** A variation seed resolves code-style axes (DI style, naming, role
  names, comment density…) and a design overlay picks the visual tokens, so many apps from the
  same features don't read like clones — while the same seed always resolves the same way.
  → [ADR-004](docs/adr/adr-004-variation-profile.md), [ADR-008](docs/adr/adr-008-design-overlay.md)
- **Desktop GUI over the same CLI.** An Electron shell spawns the unchanged CLI and reconstructs
  live progress from its log — no second implementation of the pipeline. → [ADR-010](docs/adr/adr-010-gui-electron-cli-child.md)
- **Documented decisions.** 15 ADRs, a Diátaxis docs tree, 190+ automated tests (.NET + GUI) in CI.

## Install (Windows)

Download `DPS-NN-Lite-Setup-<version>.exe` from [Releases](https://github.com/StefanMarinkov05/genesis_v6-lite/releases)
and run it (it is unsigned — SmartScreen: *More info* → *Run anyway*). The installer brings the GUI
and the generator; it cannot bring the .NET toolchain the generated apps are built with. On first
launch the GUI checks for it and prints what's missing, with the command to fix it, in the debug
terminal (**Debug** toggle) — the same report as `dps-nn doctor`. You need the first four rows of
the prerequisites table below; Node.js is only for building from source.

The FaceNote sample is preselected, so **Start** generates a complete app without an API key.
Your generated projects and logs live in `Documents\DPS-NN\`; uninstalling never deletes them.

## Quick start (Windows, from source)

**Prerequisites**

| Tool | Why | Install |
|---|---|---|
| .NET 10 SDK | builds the generator and every generated app | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |
| SQL Server Express LocalDB | the generated apps' database | Visual Studio installer or the SQL Server Express installer |
| `dotnet-ef` 10.x | migrations | `dotnet tool install -g dotnet-ef --version 10.0.*` |
| `dotnet-aspnet-codegenerator` | Identity + controller scaffolding | `dotnet tool install -g dotnet-aspnet-codegenerator` |
| Node.js 24 | the GUI only | [nodejs.org](https://nodejs.org) |
| Anthropic API key | optional; required for agent mode | [console.anthropic.com](https://console.anthropic.com) |

> An outdated `dotnet-ef` builds the generated project and *then* fails with "the specified
> deps.json does not exist" — update it with `dotnet tool update -g dotnet-ef --version 10.0.*`.

`dotnet run --project src/Genesis.Cli -- doctor` checks all of these and prints the fix for anything missing.

**Build and test the generator** (or double-click `1-build-and-check.bat`)

```powershell
dotnet build
dotnet test
```

**Generate the sample app — no API key needed.** The repo ships a sample spec (FaceNote — a social shop with
posts, catalogues, follows, cart and orders) and matching seed data; putting the seed where the LLM response would go lets the
whole pipeline run offline (images still download on first run).

```powershell
mkdir prompts\in -Force; copy seeds\facenote-rich-seed.json prompts\in\seed-response.json
dotnet run --project src/Genesis.Cli -- run --all --spec fixtures/project-spec.facenote.json --out out
```

The run ends with a banner: the URL, the seeded admin login, and the command to start the app.

**Or describe your own app** (needs `ANTHROPIC_API_KEY`):

```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-..."
dotnet run --project src/Genesis.Cli -- create --describe "Онлайн книжарница: книги, автори, отзиви, количка и поръчки"
```

**Or use the GUI**

```powershell
cd gui; npm ci; npm run dev
```

All commands, options, exit codes and environment variables: [`docs/reference/cli.md`](docs/reference/cli.md).

## How it works

| # | Stage | Produces |
|---|---|---|
| 1 | `spec-build` | spec drafted from notes by the LLM (optional) |
| 2 | `spec-expand` | `project-spec.lock.json` — features expanded, variation resolved |
| 3 | `scaffold` | `dotnet new mvc` + packages + connection string |
| 4 | `emit-models` | models, DTOs, `DbContext`, validation attributes (Scriban templates) |
| 5 | `identity` | ASP.NET Identity scaffold, Bulgarian error describer and pages, roles, seed users |
| 6 | `emit-controllers-views` | scaffolded + templated controllers and views, declarative deviations |
| 7 | `seed` | seed rows (LLM or file) with symbolic references, images, migrations |
| 8 | `verify` | build → migrate → boot → login → every route → English-leak scan |
| 9 | `report` | `wwwroot/output/project-summary.json` with code pointers |

Diagram, gate order and resume semantics: [`docs/reference/control-flow.md`](docs/reference/control-flow.md).

## Repository layout

```
src/Genesis.Core/     pipeline engine: spec model, stages, LLM bridge, agent loop
src/Genesis.Cli/      the dps-nn command line
tests/Genesis.Tests/  xUnit suite
templates/            Scriban templates rendered into generated apps
feature-templates/    reusable feature fragments (cart, posts, catalogues, orders, rating, follows…)
fixtures/             the FaceNote sample spec;  seeds/  its seed data
spec.schema.json      JSON Schema for project-spec.json (source of truth)
gui/                  Electron desktop app over the CLI
docs/                 tutorial, reference, how-to, explanation, ADRs
design/               GUI themes, backgrounds, icon, music
```

## Documentation

Start with the [tutorial](docs/01-tutorial-genesis.md). Then, by need:

- **Reference** — [CLI](docs/reference/cli.md) · [pipeline & control flow](docs/reference/control-flow.md) · [spec format](docs/reference/spec-format.md) · [file map](docs/reference/file-map.md)
- **Explanation** — [architecture decisions](docs/explanation/architecture-decisions.md) (indexes all 15 [ADRs](docs/adr/)) · [agent mode](docs/explanation/agent-mode.md) · [GUI architecture](docs/explanation/gui-architecture.md) · [image pipeline](docs/explanation/image-pipeline.md) · [techniques & traps](docs/explanation/techniques.md)
- **How-to** — [add a stage](docs/how-to/add-a-stage.md) · [add a template](docs/how-to/add-a-template.md) · [build the installer](docs/how-to/build-the-installer.md) · [the lite mirror](docs/how-to/mirror-the-lite-edition.md)

The same docs are built into a single **Architecture Handbook PDF** by CI (`scripts/build-handbook.py`).
Contributors and AI agents: `CLAUDE.md` routes each task to the right doc; `plan/` holds the
working history.

## Tech stack

C# / .NET 10 · ASP.NET Core MVC · EF Core (SQL Server) · ASP.NET Identity · Scriban ·
System.CommandLine · JsonSchema.Net · xUnit · Anthropic Messages API · Electron · TypeScript ·
Vite · xterm.js · GitHub Actions

## License

Code and documentation: [MIT](LICENSE) © 2026 Stefan Marinkov.
The recordings under `design/music/` are third-party works and are **not** covered by the MIT
license; all rights remain with their respective owners.
