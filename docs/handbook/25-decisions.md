# Decision index

Every structural choice in this book is recorded as an ADR in `docs/adr/`. Those files are the
authority: they hold context, alternatives and consequences, and they are append-only (a superseded
decision keeps its number and gains a "superseded by" line). This chapter is the index — read the
line, follow the link when the "why" matters.

## The decisions

| ADR | Decision | Gain | Cost |
|---|---|---|---|
| **001** | Three-file split: spec / lock / state | re-expansion is safe; resume never edits your input; `verify` has a stable manifest | one more file to understand |
| **002** | Identity page localization via a static replacement map | deterministic, cannot corrupt Razor | only handles known strings |
| **002b** | Core Identity pages emitted wholesale (supersedes 002 as primary) | full Bulgarian copy and app styling | more templates to maintain |
| **003** | Seeding uses symbolic refs, DB-assigned keys | seeds survive re-runs; LLM cannot invent foreign keys | parents must be inserted in order (solved by topo-sort) |
| **004** | Deterministic per-project variation (hash of seed + axis) | diverse output, stable per project | a spec author cannot "randomize" |
| **005** | LLM stages are file contracts | offline mode, auditability, replayable tests | two files per exchange |
| **006** | Scaffold deviations via regex on known shapes | cheap, and fails loud on SDK drift | brittle by construction |
| **007** | Soft delete via global query filters + audit base | no query can forget the filter | one more base class, one implicit filter |
| **008** | Distinctive design as a token overlay | looks vary without touching templates | design lives in a preset, not the spec |
| **009** | Rename boundary: user-visible name only | no breakage of internals, fixtures or logs | two vocabularies (`dps-nn` and `Genesis`) |
| **010** | GUI is an Electron shell over the CLI | one pipeline implementation | the GUI depends on the log format |
| **011** | Persistent image cache + shipped fallbacks, no served URLs | fast regenerates, no hot-linking, no broken images | cache is another directory to manage |
| **012** | "Specifications" consolidation + generic admin filters | admin nav stays usable as models grow | two model flags to apply by rule |
| **013** | Agent mode authors/repairs the spec, never generated code | every app stays pure template output | the model cannot fix a template gap |
| **014** | Read-only install root, writable workspace | the installed program never writes next to itself | asset resolution has two roots |

## The three decisions that are really principles

The three that matter most:

1. **ADR-001 and 005 together** are why a run is auditable: inputs are immutable, expansion is a
   separate artifact, and the LLM's contribution is a file.
2. **ADR-013 is the trust boundary.** The moment the model could edit generated code, no generated
   app would be explainable, and `verify` would be checking the model's work rather than the
   generator's.
3. **ADR-004 and 008 together** are the answer to "why do twenty generated apps not look identical?"
   — and the reason the answer is deterministic rather than random: the preset pick uses the same
   SHA-256 as the axes.

## The decisions this book proposes but the code has not made

Worth recording before adopting this edition. (Earlier proposals that are done: a reference for the
generated app's anatomy and a written seed contract — `docs/explanation/generated-app-anatomy.md`
and `docs/reference/seed-format.md`; and, in changelog 141, a strict `verify` with an anonymous-access
check, a working `cart` and `personalization`, and a reproducible design preset.)

| Proposal | Why |
|---|---|
| Retire the per-doc chapters listed in the Preface in favour of this book's chapters | one source per fact; today the overview, the tutorial and the reference repeat each other |
| Make `--from spec-build` / `--from spec-expand` fail loudly instead of running nothing | silent no-op is the sharpest edge found while writing this book |
| Split the product name by language: **Genesis** in English, a Bulgarian title in the Bulgarian edition, `dps-nn` for the executable | ADR-009 fixes one user-visible name for both editions; two language editions need two display names, and the boundary rule already allows that if the ADR is amended |

Adding a decision: one file under `docs/adr/`, numbered, with Context / Decision / Alternatives /
Consequences; link it from wherever it governs; never edit an accepted one.
