<!-- front-matter -->
# Preface — how to read this book

This book describes one system end to end: **Genesis**, a generator that turns two JSON files
into a complete, seeded, verified ASP.NET Core application.

It moves from breadth to depth: a reader needs the shape of a system before any of its parts make
sense, and each part assumes only the ones before it.

## The ladder

| Part | Chapters | You learn |
|---|---|---|
| **I. Overview** | 1–3 | the problem it solves, one generation told as a story, and what a generated app looks like to its user. No internals. |
| **II. Using it** | 4–7 | run it, write a spec from a requirements paper, write seed data, let the agent do both. |
| **III. Inside** | 8–12 | the architecture in one picture — style, patterns, coding conventions, tools; then the pipeline as a machine, the output, the two contracts and the design principles. |
| **IV. Architecture in depth** | 13–16 | the components and their dependency rules; how quality is enforced and tested; how the project itself is built; how it runs and ships. |
| **V. Deep dives** | 17–21 | the long-form chapters the narrative only summarises: the CLI, the GUI, the image pipeline, the Specifications page, and the traps that cost real time. |
| **VI. Reference** | 22–25 | what it cannot do, what the buyer receives (including ownership marks), a glossary, and the decision record. |

Even if you only intend to use the tool, read Part I; the vocabulary comes from there. Read Part VI
before promising the tool to anyone else.

## Who each part is for

| Reader | Read |
|---|---|
| Reviewer, professor, recruiter | Parts I and VI, then Chapter 8 for the architecture and Chapter 9 for the machinery |
| Student building a project | Parts I–II, with Chapter 22 open |
| Contributor or coding agent | Parts I, III, IV, then the file map and the ADRs |

## Conventions

- Code blocks carry a language tag (`SHELL`, `JSON`, `C#`, …). An untagged block is illustrative
  output, not a command to run.
- Paths are repo-relative unless they start with `/` or a drive letter.
- **Generated** means "produced into `out/`"; **verified** means the `verify` stage passed — never
  "it compiled".
- Diagrams are Mermaid source, rendered as vector figures here.
- Counts and names were checked against the tree at the version on the cover; where a claim could
  drift, the command that re-checks it is given.
- ADRs are referenced by number (`ADR-004`) and indexed in [Chapter 25](25-decisions.md).

## Status of this edition

**Edition 2.** The same system, written as one narrative instead of a stack of
repository documents. It follows an audit of the first edition against the code, and a second audit
of this edition's claims against the code; the corrections are recorded in the repository's
changelog (entries 137–139), and the gaps the audit found are in [Chapter 22](22-limits.md).

This edition has been adopted, so the documents listed below should be retired rather than kept in
parallel. Two
descriptions of one system drift apart, and the copy nobody reads is the one that turns out wrong.

| Chapters | Replaces |
|---|---|
| 1–3, 22 | `docs/explanation/project-overview.md` |
| 4–7 | `docs/tutorial/genesis.md` and the two authoring skills' prose |
| 9 | `docs/reference/control-flow.md` |
| 10 | `docs/explanation/generated-app-anatomy.md` |
| 11 | `docs/reference/spec-format.md` + `docs/reference/seed-format.md` |
| 12 | the variation half of `docs/explanation/architecture-decisions.md` |
| 13 | `docs/reference/components.md` + `docs/reference/file-map.md` (summary) |
| 14 | `docs/explanation/testing-policy.md` |
| 15 | (nothing today — the AI-assisted process, the skills and the cost figures were never written down) |
| 16 | `docs/how-to/build-the-installer.md` + `mirror-the-lite-edition.md` (summarised) |
| 17–21 | nothing — these chapters *are* those docs, included as the book's deep dives |
| 23–25 | (nothing today — delivery marks, the glossary and the decision index were not documented) |

Not replaced, deliberately: `docs/explanation/gui-architecture.md`, `image-pipeline.md`,
`specifications-consolidation.md`, `techniques.md`, and the ADRs — deep enough that this book links
them instead of paraphrasing.
