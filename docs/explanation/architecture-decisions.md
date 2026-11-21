<!-- last-verified: e95fedf -->
# Architecture decisions — why it's shaped this way

**Diataxis type:** explanation (synthesis + index). **Source of truth:** `docs/adr/*` — this doc
holds **no decision detail of its own**; every claim below links the ADR that owns it. Per
`plan/archive/task-a-doc-architecture.md` decision D1, the ADRs stay the decision *store*, individually
cross-referenced elsewhere in the repo; this file is the overview *over* them, not a replacement.

## The shape, in one paragraph

dps-nn turns a spec into a running app through a **staged, resumable, verify-gated** pipeline
because a single generation run is long, expensive (LLM calls, `dotnet build`, EF migrations), and
can fail partway for reasons that have nothing to do with the spec being wrong. Every structural
choice below exists to make a failed or interrupted run cheap to retry, and to make two runs of the
same spec produce comparable, auditable output rather than two unrelated black boxes.

## Why the pipeline state is split three ways

A single JSON file cannot simultaneously be the user's immutable input, the tool's expanded working
copy, and the pipeline's run-to-run state — writes from those three roles race and destroy
reproducibility. The three-file split (`project-spec.json` / `project-spec.lock.json` /
`plan/state.json`) exists specifically so `spec expand` is safe to re-run, `verify` has a stable
manifest to check against, and a state reset never touches the user's original spec.
→ **[ADR-001](../adr/adr-001-spec-lock-state-split.md)**

## Why variation is deterministic, not random

Generated sample projects must not look like clones of each other, but a professor re-running the
same spec must get the same output — `Random`/`GetHashCode()` are process-randomized and would
break that. Variation is instead a hash of the project's own seed, so it's simultaneously
diverse-across-projects and stable-within-a-project.
→ **[ADR-004](../adr/adr-004-variation-profile.md)**

## Why LLM stages are a file contract, not a direct API dependency

The tool must work identically with or without an API key, and every LLM exchange must be
auditable rather than a black-box call whose output silently flows into generated code. Framing
every LLM step as `prompts/out/<stage>-request.md` → `prompts/in/<stage>-response.json` makes the
manual and automatic paths byte-identical, and turns canned response files into test fixtures for
free.
→ **[ADR-005](../adr/adr-005-llm-file-contract.md)**

## Why seed data uses symbolic refs, not LLM-invented IDs

SQL Server assigns identity-column primary keys at insert time; an LLM has no way to know them in
advance, so asking it to invent numeric foreign keys guarantees drift the moment two runs insert
rows in a different order. Seed rows instead carry symbolic ref strings, resolved to real
DB-assigned IDs after insertion.
→ **[ADR-003](../adr/adr-003-seeding-symbolic-refs.md)**

## Why Identity localization is two separate mechanisms

"Bulgarian-only UI" splits into two problems that don't share a solution: scaffolded Razor page
*markup* (deterministic, must never corrupt Razor syntax — ruled out LLM translation) versus
*runtime error strings* (a `.resx`-shaped problem, solved via `IdentityErrorDescriber`). Treating
them as one problem was the wrong abstraction; ADR-002b later went further and replaced core-page
markup translation with wholesale custom-styled emission once a real reference project's design
requirements exceeded what a translation map could express.
→ **[ADR-002](../adr/adr-002-identity-localization-static-map.md)**,
**[ADR-002b](../adr/adr-002b-identity-pages-wholesale.md)**

## Why scaffold deviations are regex, not a Roslyn rewriter

Spec-declared deviations (removed actions, redirects, sorting) transform `aspnet-codegenerator`
output that is stable for a given pinned SDK version. A full syntax-tree rewriter is roughly 10x the
code for output whose actual variability is an SDK bump — and targeted regex anchors that fail loud
when their pattern goes missing surface that exact bump immediately, rather than silently emitting
wrong code.
→ **[ADR-006](../adr/adr-006-scaffold-deviations-regex.md)**

## Why soft delete is a query filter, not a flag developers remember to check

Admin-visible "undo" and public-invisible "gone" are different requirements that both want to
preserve relational data. An EF global query filter makes forgetting to filter structurally
impossible for every consuming query, which a convention developers have to remember to apply does
not. The one documented gap — soft-deleting a user does not cascade-hide their content, because EF
filters can't cheaply join across `AspNetUsers` — is a deliberate, revisitable trade-off, not an
oversight.
→ **[ADR-007](../adr/adr-007-soft-delete-audit.md)**

## Why distinctive design is a token overlay, not per-project bespoke layout

dps-nn emits N different projects from one template set; hand-tuning layout per generated app would
destroy the reusability that makes the tool a generator rather than a one-off scaffold. A CSS
token overlay (accent, type pairing, depth language) raises the visual ceiling for every project
from one place, at the cost of capping distinctiveness at what tokens can express.
→ **[ADR-008](../adr/adr-008-design-overlay.md)**

## Why the rename to dps-nn stopped at the user-visible surface

Renaming the C# namespaces/assemblies alongside the user-facing tool name would have been a very
large diff whose only failure mode is breaking the build, for a rename component (`Genesis.*`
internals) no end user ever sees. The boundary — rename the installed command, logs, and banners;
leave internal identifiers alone — is the same reasoning later applied to the GUI's folder
placement (a full physical repository restructure for no functional benefit).
→ **[ADR-009](../adr/adr-009-dps-nn-rename-boundary.md)**

## Why the GUI wraps the CLI instead of reimplementing it

The GUI adds a second consumer of the pipeline (alongside the batch/debug CLI workflow), and the
one thing that must never happen is pipeline logic forking between two implementations. The GUI is
an Electron shell that spawns the existing, unchanged CLI as a child process and adapts to its
on-disk contracts (`dps-nn.log.jsonl`, `state.json`, the exit-3 LLM-pending signal) — it never
drives stages one-by-one or reimplements stage behavior in Node.
→ **[ADR-010](../adr/adr-010-gui-electron-cli-child.md)** (full design:
`docs/explanation/gui-architecture.md`)

## Why images are cached persistently and served locally, never as URLs

The generated apps used a per-run "cache" that never survived a regenerate, seeded profile
pictures as raw `picsum.photos` URLs (broken at 4 of 5 render sites), and referenced a fallback
image that was never emitted. The redesign moves the download cache to a repo-root
`.image-cache/` outside `out/`, ships generic SVG fallbacks copied into each `wwwroot/`, and makes
every `ProfilePicturePath` a local relative path — so no rendered page depends on an external
image host and the five avatar-render sites collapse to one code path.
→ **[ADR-011](../adr/adr-011-image-pipeline-cache-and-fallbacks.md)** (full design:
`docs/explanation/image-pipeline.md`)

## Why small models consolidate onto one Specifications page, and filters are auto-derived

Two inconsistent handlings of small models (read-only `isLookup` join tables vs. full-CRUD
atomic reference models with their own nav clutter) unify onto a single tabbed admin page whose
tabs are read-only or CRUD by a declared flavour (`isLookup` vs. the new `isSpecification`), with
CRUD reusing the scaffolded controllers as hidden sub-controllers. Filtering is one generic
auto-derived convention (FK→select, user→select, enum→select, rating→select) applied across every
admin listing rather than bespoke per page — the rename from "Характеристики" happens last.
→ **[ADR-012](../adr/adr-012-specifications-consolidation-and-filters.md)** (full design:
`docs/explanation/specifications-consolidation.md`)

## Why the agent repairs the spec, never the generated code

`dps-nn create` closes the loop: the model drafts the spec, the generator runs, and failures go
back to the model — but its only lever is the spec. Patching generated code would turn every app
into a one-off the templates can no longer fix and would cost a read of the whole codebase per
repair; editing the spec keeps every app pure template output under the same verify gate. What
the spec cannot express is recorded as `meta.unsupportedRequests` — the backlog for extending the
generator once, for everyone. Machine problems (SDK, tools, LocalDB, API key) stop the loop at once.
→ **[ADR-013](../adr/adr-013-agent-mode-spec-only-repair.md)** (full design:
`docs/explanation/agent-mode.md`)

## Why the installed app splits a read-only install root from a writable workspace

A checkout used one folder for both what the CLI reads (templates, schema) and what it writes
(logs, prompts, output). An installed program can't write into its own install folder — updates
replace it and per-machine installs forbid it. The installer therefore ships a self-contained
`dps-nn.exe` next to its templates (found via the exe's own directory, as `GenesisRoot()` already
tried first) and runs it with cwd = `Documents\DPS-NN`; in a checkout both roots stay the repo
root. Prerequisites that can't be bundled (.NET SDK, EF/scaffolding tools, LocalDB) are reported
by `dps-nn doctor`, which the GUI runs at startup.
→ **[ADR-014](../adr/adr-014-install-root-and-workspace.md)** (how to build it:
`docs/how-to/build-the-installer.md`)

## Reading further

For the pipeline's mechanics (not the why) see `docs/reference/control-flow.md`. For hard-won
implementation patterns and traps, see `docs/explanation/techniques.md`. For the dated,
tactical history of specific bugs and their fixes, see `plan/decisions.md` — that log, not this
synthesis, is what to check before re-proposing a fix for a recurring symptom.
