# 074 — Task A documentation architecture (design + skeleton)

**Type:** Feature / change (design record + doc skeleton — Task A of `plan/dps-nn-6.5.0-spec.md` §1)
**Model:** Opus — the routing table assigns the "design the doc tree + CLAUDE.md router" subtask to
Opus (high thinking): it's the architectural step that sets the structure every Sonnet fill-subtask
fills, and it required reconciling the spec's aspirational tree against docs that now exist — a
one-way-door-ish call (7+ docs get written against whatever structure this fixes).

## Summary

Produced the Opus deliverable for Task A: the documentation-architecture **design record** plus the
physical **stub skeleton** the Sonnet fill-subtasks build against. No doc bodies were written — this
fixes *what each doc is, where it lives, its Diataxis type, its source of truth, and which subtask
fills it*, exactly parallel to how `docs/explanation/gui-architecture.md` is the contract for the GUI
subtasks.

The real work was reconciliation: the spec's §1 tree was written before the docs it describes
existed, and reality is better-factored in three places. Resolved (design record §1, decisions D1–D4):
- **D1** — the 11 cross-referenced ADRs stay the decision *store*; `explanation/architecture-decisions.md`
  becomes a thin synthesis that *indexes* them, not a collapse of them into one file.
- **D2** — `docs/architecture.md` (mostly reference) is split into `reference/control-flow.md` and
  retired in the fill commit.
- **D3** — `reference/spec-format.md` is a concepts companion to `spec.schema.json`, never a
  regenerated field dump.
- **D4** — `FAILED-ATTEMPTS.md` is scoped to *structural* dead-ends only (not a clone of
  `plan/decisions.md`'s chronological log); may be dropped if it would only echo it. **Flagged for
  Stefan** — this narrows a spec artifact.

Anti-drift rules are **not** restated — they already live in `.claude/rules/documentation.md` and the
`agent-doc-architecture` skill; the design record references them and only adds project structure.

## Byproduct: known drift found while designing (logged for the fill phase)

- README says the successful-run archive is "moved (not copied)" — the code (`ArchiveCompletedProject`)
  **copies**. README is wrong.
- README says "ADR-001..009"; there are 11.
- README Quickstart still says `dotnet new sln -n Genesis`; repo ships `dps-nn.slnx` and CLAUDE.md
  says not to recreate a `.sln`.
- `Genesis.sln.notes.md` at the root is orphaned cruft.

Each is a fix-target for the relevant fill commit, recorded in the design record §5 so it isn't
re-lost.

## Techniques

- Applied the tiered router + Diataxis split from the `agent-doc-architecture` skill, but let reality
  override the spec's tree where a literal port would create duplication (two "why" stores) or
  fastest-rotting hand-maintained content (a field dump vs the schema).
- Each stub carries its **own contract inline** (Diataxis type, source-of-truth, what it must/ must-not
  hold, filler subtask, interim authoritative source) so a Sonnet fill-subtask that opens its file is
  self-contained — no need to re-read the whole design record.
- CLAUDE.md was **not** rerouted to the empty stubs; each routing row flips in the same commit as its
  fill (same-commit rule), so an agent is never routed to a stub. Only a pointer to the design record
  was added.

## Affected files

- New `plan/task-a-doc-architecture.md` — the design record.
- New stubs: `docs/01-tutorial-genesis.md`, `docs/reference/{control-flow,spec-format,file-map}.md`,
  `docs/explanation/{architecture-decisions,techniques}.md`, `docs/how-to/{add-a-stage,add-a-template}.md`,
  `docs/FAILED-ATTEMPTS.md`.
- `CLAUDE.md` — added the Task A routing row (still under 150 lines).
- `plan/dps-nn-6.5.0-spec.md` — pointer from §1 to the design record.
- `tasks.md` — Task A expanded into the ordered fill checklist.
