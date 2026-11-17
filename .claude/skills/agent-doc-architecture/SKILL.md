---
name: agent-doc-architecture
description: Structuring project documentation that both AI agents and humans read from a single source. Use when setting up or reorganizing docs for any codebase - deciding what belongs in CLAUDE.md vs on-demand docs, designing a docs tree, writing architecture/reference/how-to/tutorial docs, or when docs are bloated, stale, contradictory, or an agent keeps re-learning the same project each session. Covers the tiered router pattern, the Diataxis split, and anti-drift rules. Trigger before writing more than one documentation file for a project, and whenever a CLAUDE.md is created or edited.
---

# Agent Documentation Architecture

Documentation for a project that both humans and coding agents read. The goal is one source of truth that stays cheap to load and hard to rot.

## The two failure modes this exists to prevent

1. **Context budget.** Anything auto-loaded every session competes directly with reasoning tokens. A large "explains everything in detail" doc loaded on startup is the most expensive and least useful doc you can own. Detail must be loadable on demand, not always resident.
2. **Drift.** A wrong doc is worse than no doc, because the agent trusts it and is misled. The more detailed and hand-maintained a doc is, the faster it rots. Everything below optimizes against drift first.

## Tiered structure (the core pattern)

- **Tier 0 - Router (always loaded, tiny).** `CLAUDE.md`. Three sentences on what the project is, a routing table ("doing X? read docs/Y.md"), and only the critical DO-NOT rules. It is a router, not a container - it holds almost no content, it points to it. Keep it under ~150 lines; adherence degrades past ~200.
- **Tier 1 - Orientation (read at session start when needed).** README (human narrative + one overview diagram) and a one-time end-to-end tutorial. Serves a new human and a fresh agent equally.
- **Tier 2 - Reference & explanation (loaded on demand only).** Specs, per-file map, control flow, techniques, architecture decisions, how-to guides. Read when a task needs them, never at startup.

This mirrors how skills work (a small always-visible description plus reference files pulled in on demand). Apply the same discipline to project docs.

## Diataxis split (how to cut the Tier 2 docs)

Cut docs along two axes - practical vs theoretical, study vs work - into four types. Do not mix types in one file; mixed files rot as a unit and are hard to load selectively.

- **Reference** (information): specs, schemas, per-file map, stage tables. Dry, precise, look-up-able.
- **Explanation** (understanding): why the system is shaped this way, architecture decisions, techniques, hard-won failure knowledge. The highest-value docs, because this is the one thing not recoverable from the code.
- **How-to** (task): "how to add a stage", "how to add a template".
- **Tutorial** (learning): one-time end-to-end orientation.

## Anti-drift rules (non-negotiable)

- **Single source of truth per fact.** Never state a fact in two docs. The day they disagree, both become untrustworthy. Cross-reference instead of duplicating.
- **Same-commit rule.** A doc changes in the same commit as the code it describes. A PR that changes control flow but not the control-flow doc is a review smell. This is the single highest-leverage habit.
- **Staleness stamp.** Each doc header carries `last-verified: <commit-sha>`. Makes rot visible instead of silent.
- **Generated over hand-written.** Anything derivable from code or a spec (file trees, API signatures, stage lists) is generated, never hand-maintained. A hand-written "what every file does" list is the fastest-rotting, most-misleading doc there is - generate it from per-file header comments instead.
- **Diagrams are Mermaid.** State machines, control flow, DAGs - text-based, diffable in git, renders on hosting, and readable by the agent as source. Never a flat image; an image is opaque to reasoning and invisible in diffs.
- **One source, two audiences - never write it twice.** Humans want narrative + diagrams; agents want dense structured reference. Write the *why* once in prose; let both read it. Maintaining parallel human and AI doc sets doubles the drift surface and always decays.

## Claude Code placement

- `CLAUDE.md` (repo root): auto-loaded every session, delivered as context (not enforced policy), so keep it short and specific. Imperative rules only ("never edit `generated/`", not "keep generated code clean").
- `.claude/rules/*.md`: also auto-loaded - keep lean. Never put a large architecture doc here; it would tax every session.
- `docs/` topic files: read on demand with normal file tools. This is where detail lives.
- Skills: load by description match, on demand.

## File layout template

```
CLAUDE.md                      # Tier 0 router. Tiny. Auto-loaded.
README.md                      # Human narrative + overview Mermaid diagram.
.claude/rules/                 # Lean always-on rules (see documentation.md).
docs/
  01-tutorial.md               # One-time end-to-end orientation.
  reference/
    control-flow.md            # Mermaid state diagram + stage table.
    spec-format.md             # Schema, field by field.
    file-map.md                # GENERATED from file headers. Do not hand-edit.
  explanation/
    architecture-decisions.md  # Why, ADR-style.
    techniques.md              # Hard-won patterns and failure knowledge. Highest value.
  how-to/
    <task>.md
  FAILED-ATTEMPTS.md           # What was tried and why it failed; pruned when obsolete.
```

## Checklist before shipping a docs set

- [ ] CLAUDE.md is a router under ~150 lines, not a content container.
- [ ] No fact appears in two files.
- [ ] Every doc has a `last-verified` stamp.
- [ ] Anything generatable is generated, not hand-written.
- [ ] All diagrams are Mermaid source.
- [ ] Each file is a single Diataxis type.
- [ ] The always-loaded surface (CLAUDE.md + rules) is small enough that even if deep docs rot, the router still points somewhere real.

## Guard against maintenance theater

The default fate of a hand-maintained doc tree is: accurate for two weeks, then real work outpaces discipline and it rots into a liability. If the three habits (tiny router, generate the generatable, same-commit rule) will not be kept, build *fewer* docs, not more - a small accurate set beats a large decaying one. If only one thing is maintained well, maintain the explanation docs (techniques / failed-attempts): that knowledge is the expensive-to-rediscover part and is not recoverable from the code.
