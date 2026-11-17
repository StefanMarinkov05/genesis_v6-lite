<!-- last-verified: e95fedf -->
# Task A — documentation architecture (design record)

Detailed design for **Task A** of `plan/dps-nn-6.5.0-spec.md` §1 (the "Genesis Documentation"
task). This is the **Opus deliverable**: it fixes the target doc tree, the Diataxis cut, and the
per-doc contract every Sonnet fill-subtask builds against — analogous to how
`docs/explanation/gui-architecture.md` is the contract the GUI subtasks build against. It does
**not** write the doc bodies; it defines what each doc is, where it lives, what it may and may not
hold, and which subtask fills it.

The anti-drift rules are **not restated here** — they already live in `.claude/rules/documentation.md`
(single source of truth, same-commit, staleness stamp, generate-the-generatable, Mermaid-only,
router-not-container, detail-on-demand) and in the `agent-doc-architecture` skill. This record only
adds the project-specific *structure* those rules apply to.

## 1. Why the spec's §1 tree is not built verbatim

`dps-nn-6.5.0-spec.md` §1 sketched an aspirational tree written before the docs it describes
existed. Reality has since diverged in ways that make a literal port *worse*, not better. The three
collisions and their resolutions (the real Opus judgment calls):

- **D1 — The "why" layer already exists as 11 ADRs, not one file.** The spec proposed a single
  `explanation/architecture-decisions.md`. But `docs/adr/adr-001..010` (+ `002b`) is already a
  well-factored, individually-cross-referenced decision store (CLAUDE.md, README, `plan/decisions.md`
  and the GUI docs all link specific ADRs). Collapsing them into one file would break every
  cross-reference, create a second "why" location (violating single-source-of-truth), and produce a
  large mixed file that rots as a unit. **Resolution:** the ADR folder stays the decision *store*
  unchanged. `explanation/architecture-decisions.md` becomes a **thin synthesis + index** — the
  high-level "why it's shaped this way" narrative (staged, resumable, verify-gated, deterministic
  variation, LLM file-contract) where each point *links to* its authoritative ADR and states no
  decision detail of its own. Overview lives there; records live in `adr/`; no fact in both.

- **D2 — `docs/architecture.md` is mostly reference and must be split, then retired.** Its content
  is ~90% reference (data flow, component map, generated-app shape, verify-gate sequence) with one
  sentence of rationale (cheap→expensive gate ordering). **Resolution:** its flow/stage/gate content
  migrates into `reference/control-flow.md` (expanded, with the Mermaid state diagram the flat ASCII
  lacks); the one rationale sentence rides along as a short note there (splitting a single coupled
  sentence into its own file is over-factoring). The component-map paragraph is superseded by the
  generated `reference/file-map.md`. `docs/architecture.md` is **deleted in the same commit** that
  fills `control-flow.md` — not left as a stub (a stale pointer is worse than none).

- **D3 — spec-format.md is a concepts companion, not a field dump.** `spec.schema.json` is the
  authoritative field-by-field shape (JSON Schema, `additionalProperties:false`). A hand-written
  field enumeration would duplicate and rot against it. **Resolution:** `reference/spec-format.md`
  explains the spec's *concepts and non-obvious mechanisms* — the `meta`/`models`/`identity`/
  `views`/`controllers`/`outputReport` groupings, the `$variation:` placeholder mechanism, feature
  placeholders, `seedUsers` — and defers to `spec.schema.json` as the authoritative shape. It is
  explanation-flavored reference, never a regenerated schema mirror.

- **D4 — FAILED-ATTEMPTS.md is scoped to *structural* dead-ends only; RESOLVED: dropped.**
  `plan/decisions.md` is already the chronological "tried this fix, it failed, never re-propose" log
  (CLAUDE.md mandates reading it before proposing any recurring-bug fix). To avoid a second
  chronological store, `docs/FAILED-ATTEMPTS.md` was scoped to hold **only approaches that are now
  architecturally impossible / a permanent no**, distinct from decisions.md's tactical, dated log. A
  search of `plan/decisions.md` for that pattern (during the fill phase) found **zero** genuine
  structural dead-ends — every "we rejected X" already lives correctly in an ADR's *Alternatives
  Considered* section (Roslyn rewriter, LLM page translation, `.resx`, Tailwind swap, live SQL
  existence-check), and everything else is tactical bug-fix history that belongs in decisions.md. Per
  this decision's own stated default, the stub was **dropped** rather than shipped empty. *(Stefan:
  reversible — if a genuine structural dead-end shows up later, recreate the file scoped exactly as
  above.)*

## 2. Target tree (reconciled with reality)

```
CLAUDE.md                          # Tier 0 router (exists, 90 lines). Router, not container.
README.md                          # Tier 1 narrative + overview Mermaid (exists; needs the diagram + drift fixes)
docs/
  01-tutorial-genesis.md           # Tier 1 tutorial — one-time end-to-end orientation           [NEW]
  reference/
    control-flow.md                # Mermaid state diagram + 9-stage table + verify-gate sequence [NEW; absorbs architecture.md]
    spec-format.md                 # Concepts companion to spec.schema.json (D3)                  [NEW]
    file-map.md                    # GENERATED from per-file header comments — never hand-edited  [NEW + generator]
  explanation/
    architecture-decisions.md      # Thin "why it's shaped this way" synthesis → indexes docs/adr/ (D1) [NEW]
    techniques.md                  # Distilled Scriban / @@media Razor / EF-migration patterns   [NEW; highest value]
    gui-architecture.md            # GUI Electron-over-CLI contract (exists; correct as-is)
  adr/                             # 11 ADRs — the decision STORE, unchanged (D1)
  how-to/
    add-a-stage.md                 # Task guide                                                   [NEW]
    add-a-template.md              # Task guide                                                   [NEW]
```

(`docs/FAILED-ATTEMPTS.md` was scoped and evaluated per D4, then dropped — see §1.)

Retired in the fill phase: `docs/architecture.md` (split per D2). Not touched by Task A:
`plan/*`, `changelogs/*`, `design/*`, `spec.schema.json` (all keep their current roles).

## 3. Per-doc contract (what each fill-subtask must produce)

Each doc is a **single Diataxis type**. `SoT` = the authoritative source it must not duplicate.

| Doc | Type | SoT it defers to | Holds | Must NOT hold | Fills (model) |
|---|---|---|---|---|---|
| `01-tutorial-genesis.md` | tutorial | README, control-flow | one linear "notes→running app" walkthrough | reference tables, rationale | Sonnet low |
| `reference/control-flow.md` | reference | `Pipeline.CanonicalOrder` | Mermaid state diagram, stage table, verify-gate sequence | *why* beyond one gate-order note | Sonnet med |
| `reference/spec-format.md` | reference | `spec.schema.json` | concept groups, `$variation:`/feature mechanisms | a regenerated field enumeration | Sonnet low |
| `reference/file-map.md` | reference (generated) | per-file header comments | generated file→purpose map | anything hand-written | Sonnet med (script + headers) |
| `explanation/architecture-decisions.md` | explanation | `docs/adr/*` | why-staged/resumable/verify-gated synthesis, each linking its ADR | per-decision detail (lives in the ADR) | Sonnet med |
| `explanation/techniques.md` | explanation | code + changelogs | Scriban helpers, `@@media` Razor escaping, EF migration ordering, the deps.json-too-old + BG-residue-map traps | chronological logs (that's decisions.md) | Sonnet high (→Opus if shallow) |
| `how-to/add-a-stage.md` | how-to | code | steps to add a pipeline stage | deep rationale | Sonnet med |
| `how-to/add-a-template.md` | how-to | code | steps to add a Scriban template | deep rationale | Sonnet med |

Every fill lands `last-verified: <sha>` in its header and is a **single Diataxis type**; a doc that
starts mixing types is a bug.

## 4. Target CLAUDE.md routing table (each row flips in its fill's commit)

The router is **not** rerouted to empty stubs now. Each row below is flipped in the **same commit**
as the subtask that fills its target (same-commit rule), so an agent is never routed to a stub.

| If you are... | Read (target) |
|---|---|
| Orienting for the first time | `README.md` → `docs/01-tutorial-genesis.md` for the deep walk |
| Working on pipeline order / stages | `docs/reference/control-flow.md` |
| Asking *why* it is built this way | `docs/explanation/architecture-decisions.md` → `docs/adr/` for detail |
| Touching `project-spec.json` shape | `spec.schema.json` (authoritative) + `docs/reference/spec-format.md` |
| Wondering what a source file does | `docs/reference/file-map.md` |
| Fighting Scriban / Razor / EF migrations | `docs/explanation/techniques.md` |
| Adding a stage / a template | `docs/how-to/add-a-stage.md` / `add-a-template.md` |
| Restructuring docs (this task) | `plan/task-a-doc-architecture.md` (this file) |

## 5. Known drift found while designing — RESOLVED during the fill phase

Recorded here so it wasn't re-lost between the design and fill sessions; all four are now fixed:

- **README "moved (not copied)"** — fixed to "copied" (matches `Program.cs ArchiveCompletedProject`,
  which copies and leaves originals in place).
- **README "ADR-001..009"** — fixed to reflect all 11 (through `010` + `002b`), and the doc-map
  section now lists the new `docs/reference/` + `docs/explanation/` + `docs/how-to/` tree.
- **README Quickstart's `dotnet new sln -n Genesis`** — removed; the checked-in `dps-nn.slnx` needs
  no recreation, and CLAUDE.md already said not to.
- **`Genesis.sln.notes.md`** — deleted. It instructed recreating a `.sln`, which directly
  contradicted CLAUDE.md's "don't recreate a `.sln`" rule — actively wrong, not just stale.

## 6. Build order for the fill phase

1. `reference/control-flow.md` (unblocks retiring `architecture.md` + fixes the routing anchor).
2. `explanation/architecture-decisions.md` (thin index over the ADRs).
3. `reference/spec-format.md`, `reference/file-map.md` (+ its generator + per-file headers).
4. `explanation/techniques.md` (highest value; deepest code/history read; also resolves D4).
5. `how-to/*`, then `README.md` overview diagram + drift fixes, then `01-tutorial-genesis.md`.

Each step: fill the doc, flip its CLAUDE.md routing row, stamp `last-verified`, delete/adjust the
superseded source — **one commit per doc**.
