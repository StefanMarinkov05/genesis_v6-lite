# Changelog model attribution + funny English worker-name variant

**Type:** Feature / change
**Model:** Sonnet — a documentation/convention change plus a small, well-scoped parameterization of an existing helper (`ParallelEmit.WorkerNames` → `ResolveWorkerNames`); no cross-cutting architecture decisions involved.

## Summary
Two independent requests bundled (both change the project, so both get recorded per changelog
discipline, but they're unrelated enough that this note calls out the split):

1. Every changelog entry now records which Claude model authored the change and *why that model*.
   Backfilled across all 52 prior entries (001-052).
2. `ParallelEmit`'s worker-name crew now has a funny-English variant, selectable via an environment
   variable, alongside the existing Bulgarian one from issue #5.

## Part 1 — Model attribution
- `changelogs/README.md` now documents the convention: a `**Model:**` line right after `**Type:**`,
  naming the model AND the reason (per the `coding-standards` skill's own routing rubric — Opus for
  cross-cutting/architectural work or root-cause debugging with real ambiguity; Sonnet for standard
  multi-file feature work following an established pattern; Haiku for mechanical/pattern-copy work).
- Backfilled all of `001-*.md` through `052-*.md` via a one-off script
  (reconstructed, not guessed from nothing): sourced from `plan/state.md`'s own session headers
  (which already tag most sessions `(Opus: ...)` / `(Sonnet: ...)`), the v6 task-breakdown's own
  per-task model routing table for the batches that map 1:1 to breakdown tasks, and the explicit
  `haiku batch` / `sonnet batch` labels already in the git log for 049/050. A few early entries
  (026, 027, 030 — the Claude Code migration/setup changes) have no explicit model recorded anywhere;
  marked Opus by chronological placement (immediately before Session 19, which is explicitly the
  first Sonnet session) rather than left blank or guessed as something unverifiable.
- This is a **best-effort historical reconstruction**, not a re-verified fact for every single entry —
  said explicitly in `changelogs/README.md` so a future reader doesn't over-trust the early ones.

## Part 2 — Funny English worker-name variant
- New `ParallelEmit.EnglishWorkerNames = ["Bubba", "Chuck", "Duke", "Rusty", "Slim"]`, alongside the
  existing Bulgarian `WorkerNames`.
- **Deliberately NOT gated on the generated project's `spec.meta.language`.** That field is
  schema-locked to `"bg"` only (`spec.schema.json`), matching CLAUDE.md's explicit "Bulgarian-only UI
  is a hard requirement." Widening it to accept `"en"` would let someone set the generated app's
  language without anything else in the generator (BG validation messages, the identity replacement
  map, the hardcoded-Bulgarian LLM seed prompt) actually changing — silently half-working, the exact
  "two code paths, one concept, drift" bug class this project has been bitten by before (sessions
  5/8/33). Confirmed this reasoning with Stefan before implementing (AskUserQuestion) rather than
  guessing which he meant.
- Instead: a new `DPS_NN_LOG_LANGUAGE` environment variable (`en` → funny English crew; unset/anything
  else → Bulgarian, preserving issue #5's original ask as the default). This is a **tool console**
  preference, decoupled from the generated app's own (hard-locked) UI language.
- `ParallelEmit.ResolveWorkerNames(string? envValue)` is a pure function of the raw env value (not an
  inline `Environment.GetEnvironmentVariable` read inside `RunAsync`), so it's unit-testable without
  mutating real process-global state — env-var mutation across parallel xunit tests would be flaky.
  `RunAsync` reads the real env var once at the top and threads the resolved array through.

## Verification
- `dotnet build` clean; `dotnet test` **97/97** (85 baseline + 12 new: English-names-in-order,
  4 default-to-Bulgarian cases incl. null/empty/garbage, 3 case-insensitive `en` cases).
- Smoke-tested against the real generated project: `DPS_NN_LOG_LANGUAGE=en dotnet run ... emit-models`
  produced `[Bubba] wrote Models/AuditableEntity.cs`, `[Chuck] wrote Models/Brand.cs`, etc. — confirmed
  live, not just asserted in a unit test. A full `run --all` regenerate wasn't needed: this is a
  parameterization of an already-verified code path (changelog 051), with zero effect on emitted file
  content.

## Affected files
- `changelogs/README.md` (convention documented)
- `changelogs/001-*.md` through `changelogs/052-*.md` (Model line backfilled, 52 files)
- `src/Genesis.Core/Pipeline/ParallelEmit.cs` (`EnglishWorkerNames` + `ResolveWorkerNames`)
- `tests/Genesis.Tests/CoreTests.cs` (6 new test methods, 12 cases)
