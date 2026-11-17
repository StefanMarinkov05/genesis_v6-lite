# 075 — Task A documentation fill phase

**Type:** Feature / change (fills every doc stubbed in changelog 074 — Task A of
`plan/dps-nn-6.5.0-spec.md` §1)
**Model:** Sonnet — well-specified implementation work following the Opus design record
(`plan/task-a-doc-architecture.md`); each fill's contract, source of truth, and Diataxis type were
already fixed, leaving mechanical-to-moderate synthesis rather than architectural judgment. One
subtask (`techniques.md`) ran at Sonnet-high per the design record's own routing.

## Summary

Filled every doc stubbed in changelog 074, in the design record's build order, one commit-worthy
step at a time: fill → flip the relevant CLAUDE.md routing row → stamp `last-verified` (same-commit
discipline applied throughout, not batched at the end).

- **`docs/reference/control-flow.md`** — Mermaid flow diagram, a stage table cross-checked against
  every stage class's own doc comment (not just `Pipeline.CanonicalOrder`), and the verify-gate
  sequence re-derived from `VerifyStage.RunAsync` directly. This surfaced that `docs/architecture.md`
  was itself already stale — it said "GET first cardGrid route" / "GET first adminTable route",
  but the code (B4) now scans the *whole* discovered route surface, not one page per kind.
  `docs/architecture.md` deleted per design decision D2.
- **`docs/explanation/architecture-decisions.md`** — read and synthesized all 11 ADRs into one "why
  it's shaped this way" narrative, each claim linking its ADR. Holds no decision detail of its own
  (D1) — the ADRs stay the store.
- **`docs/reference/spec-format.md`** — concept groups + the two non-obvious mechanisms
  (`$variation:*` placeholder resolution, `$primaryModel` feature-fragment expansion) and the
  seedUsers-has-no-password design (passwords are derived via variation, never authored).
- **`docs/reference/file-map.md` + `scripts/generate-file-map.ps1`** — a real generator, not a
  hand-written map. Added a `// FILE-MAP: <purpose>` header line to all 14 hand-written `.cs` files
  under `src/`. Confirmed `dotnet build` unaffected (0 errors) after every header edit.
- **`docs/explanation/techniques.md`** — the highest-value doc. Distilled the Scriban helper surface,
  the `@@media` Razor-escaping trap, the full 4-theory `dotnet ef` "deps.json does not exist" saga
  (with its debugging-discipline lesson: stop guessing after 2-3 failed theories, add diagnostics
  instead), the BG residue-map identifier-corruption trap, the dual `_ViewImports.cshtml` layers
  trap, and the pipeline stage-ordering trap (`ProfileHash.cs`). **Flagged, not fixed**: a real
  ADR-004 deviation found while reading `TemplateEngine.ResolveDesignPreset` — it picks the design
  preset via `variation.Seed.GetHashCode()`, not the deterministic `SHA256(seed+axis)` every other
  axis uses. Cosmetic-only (design preset, not build-breaking), but a genuine inconsistency with the
  ADR's stated invariant — worth a look, out of scope for a docs task to silently patch.
- **`docs/FAILED-ATTEMPTS.md` (D4) — dropped.** Searched `plan/decisions.md` for genuine structural
  dead-ends (grep for abandoned/reverted/not-viable language); found none. Every "we rejected X"
  already lives correctly in an ADR's Alternatives-Considered section. Per D4's own stated default
  ("if it would only echo decisions.md, drop it"), the stub was deleted rather than shipped empty.
- **`docs/how-to/add-a-stage.md`, `add-a-template.md`** — numbered task recipes, cross-referencing
  rather than duplicating the reference/explanation docs.
- **`README.md`** — added the Tier-1 overview Mermaid diagram; fixed three real drift items found
  during design (074): "moved (not copied)" → "copied" (matches `ArchiveCompletedProject`), "ADR-
  001..009" → all 11, and the Quickstart's `dotnet new sln -n Genesis` (contradicted CLAUDE.md's
  "don't recreate a `.sln`" rule — `dps-nn.slnx` is checked in) removed; doc-map section rewritten
  for the new tree.
- **`docs/01-tutorial-genesis.md`** — one linear walkthrough, clean checkout to a running seeded app,
  narrative only.
- **`Genesis.sln.notes.md` deleted** — it instructed recreating a `.sln`, directly contradicting
  CLAUDE.md; actively wrong, not merely stale.

## Verified

- `dotnet build`: 0 warnings, 0 errors, after every header-comment edit (not just once at the end).
- `dotnet test`: 111/111 passing.
- The file-map generator was run for real, not assumed correct — it initially mangled em-dashes
  (Windows PowerShell 5.1's `Get-Content`/`Set-Content -Encoding utf8` misdetects UTF-8-without-BOM
  source under a non-UTF8 system codepage) and truncated multi-line header comments to their first
  line. Both are real bugs a "should work" read wouldn't have caught. Fixed by switching to raw
  `[System.IO.File]` read/write with explicit `UTF8Encoding(false)`, and by collapsing every
  `FILE-MAP` header to one line (the generator only ever reads line 1).
- Grepped the whole repo for stale references to everything deleted (`docs/architecture.md`,
  `FAILED-ATTEMPTS.md`, `Genesis.sln.notes.md`) after the fact; the only remaining hits are
  intentional historical mentions in changelogs/`plan/decisions.md`/the design record itself.
- `CLAUDE.md` re-checked at 94 lines (was 90) — still well under the ~150-line router budget despite
  six new routing rows.

## Affected files

- Filled: `docs/reference/{control-flow,spec-format,file-map}.md`,
  `docs/explanation/{architecture-decisions,techniques}.md`,
  `docs/how-to/{add-a-stage,add-a-template}.md`, `docs/01-tutorial-genesis.md`.
- New: `scripts/generate-file-map.ps1`.
- Deleted: `docs/architecture.md`, `docs/FAILED-ATTEMPTS.md`, `Genesis.sln.notes.md`.
- Header comment added (first line) to all 14 files under `src/Genesis.Core/` + `src/Genesis.Cli/`.
- `CLAUDE.md` — six routing rows added/flipped to the filled docs.
- `README.md` — overview diagram, three drift fixes, doc-map rewrite.
- `tasks.md`, `plan/task-a-doc-architecture.md` — every checklist item and open question resolved.
