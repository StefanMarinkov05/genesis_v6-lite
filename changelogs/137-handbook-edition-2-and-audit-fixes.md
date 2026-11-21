# Architecture Handbook, edition 2 (proposal) — plus audit fixes to the first edition

**Type:** Docs / tooling
**Model:** Opus — a full documentation audit against the code, a re-authored handbook, and a new
build entry point.

## Summary

1. **Edition 2 of the handbook**, a separately authored manuscript under `handbook2/` (18 chapters,
   5 parts, 7 diagrams), built to `dist/dps-nn-handbook-v2.pdf` by a new
   `scripts/build-handbook2.py` that reuses the first edition's LaTeX/Mermaid machinery. It is a
   proposal for review, not the project's documented source of truth — the preface says so and lists
   which per-doc chapters each chapter would supersede.
2. **Structure follows a gradient**, not a file dump: overview → one generation told as a story →
   what the output looks like → hands-on (run, spec, seed, agent) → the machine (stages, output
   architecture, contracts, principles) → components → quality → runtime → limits, glossary,
   decisions.
3. **Audit corrections** to the existing docs (below), including two facts that were simply wrong.
4. **New reference chapters in `docs/`** for the two gaps the audit found: the seed contract
   (`docs/reference/seed-format.md`) and a module-level component map
   (`docs/reference/components.md`), plus the output anatomy
   (`docs/explanation/generated-app-anatomy.md`).
5. **The seed authoring skill was stale**: it told the model to emit `imageKeyword` (singular,
   string) while the stage requires `imageKeywords` (array; 1 entry for `single`, 2–4 for
   `multiple`). Following the skill produced a seed that fails schema validation.

## Audit findings (verified against the code, then fixed or recorded)

| # | Finding | Where | Status |
|---|---|---|---|
| 1 | `spec-build` is **not** part of `run --all`. It is the separate `spec build` command; the runner drives seven stages, and `spec-expand` runs ahead of the loop in the CLI. The control flow diagram implied all nine ran. | `docs/reference/control-flow.md`, `README.md`, project overview | fixed |
| 2 | `--from spec-build` (or any stage name outside the runner's seven) starts **nothing at all** — silent no-op, not an error. | control-flow resume semantics | documented; behaviour unchanged (proposal in Ch. 17) |
| 3 | The project overview attributed generated-app "look" to ADR-004; ADR-004 is code-style axes, the visual side is ADR-008. | `docs/explanation/project-overview.md` | fixed |
| 4 | "67 Scriban template files" counts 4 JSON data maps as templates. Real split: 63 `.sbn` + 4 `.json`. | project overview | fixed |
| 5 | "Every source file carries a one-line purpose header; they are collected in the file map" — the map covers `src/**/*.cs` only. | project overview | fixed (wording + the component map now covers the rest at module level) |
| 6 | The **seed JSON contract was undocumented** outside one line in ADR-003, although it is half the product's input. | new `docs/reference/seed-format.md` | fixed |
| 7 | The seed skill used `imageKeyword` (string); the code and the shipped seed use `imageKeywords` (array, length fixed by `images.mode`). | `utils/claude-skills/dps-nn-seed-authoring/SKILL.md` | fixed |
| 8 | `VariationResolver`'s FILE-MAP header said "8 axes"; the registry has 11 (the visual axes were added later). Header and the generated row in `file-map.md` re-synced. | `src/.../VariationResolver.cs`, `docs/reference/file-map.md` | fixed |
| 9 | The generated-app file tree, written as a fenced block with box-drawing characters, wrapped into unreadable "↪" continuations in the PDF. | new anatomy doc | fixed (table instead of a tree) |
| 10 | Mermaid rejects backticks inside edge labels; the first v2 build failed on one. | control-flow diagram | fixed |
| 11 | README's "190+ automated tests" could not be verified here: the suite has 137 `[Fact]`/`[Theory]` methods (.NET) plus the GUI `node --test` files; the exact executed case count needs a Windows run. | `README.md` | recorded, not changed |
| 12 | `plan/codebase-map.md` is stale (still says no .NET SDK is available in the container). | `plan/` | recorded, left as history |

## Techniques

- **One typesetting pipeline, two manuscripts.** `build-handbook2.py` imports
  `build-handbook.py` by path (so `__main__` does not run), overrides `PARTS` and the output path,
  and reuses the cover, palette, Lua filter, table weighting and Mermaid rendering unchanged. The
  only edit to the original was a module flag (`UNLISTED_NOTE`) so a second manuscript does not
  print a "not in the handbook" list of every `docs/` file.
- **Chapter numbers come from LaTeX, not from prose.** Titles are plain ("Components"); the first
  attempt prefixed them with "Chapter N —", which produced "Chapter 2 — Chapter 1 — …" once
  `--top-level-division=chapter` added its own numbering.
- **Diagrams sized for print.** The reading-order diagram for the 8-stage chain rendered as a
  full-page vertical strip (the builder flips LR→TD at ≥6 edges); the story chapter now uses a
  5-node loop, and the detailed 8-stage picture lives in the workflow chapter where a full page is
  earned.
- **Style pass, mechanical where possible:** bullets normalised from `**x** —` to `**x:**`,
  headings from `A — b` to `A: b`, glossary converted from 37 dash definitions to a two-column
  table, and the most formulaic sentences rewritten by hand (measured em-dash density per chapter
  before and after).
- **Verify rendering, not just compilation.** Builds were checked by extracting page text and
  rendering pages to PNG; that is how the tree block and the tiny component diagram text were found.

## Build

```bash
# edition 1 (docs/)
python3 scripts/build-handbook.py            # -> dist/dps-nn-handbook.pdf   (90 pages)

# edition 2 (handbook2/) — proposal
python3 scripts/build-handbook2.py           # -> dist/dps-nn-handbook-v2.pdf (59 pages)
```

Both need pandoc 3.x, XeLaTeX with IBM Plex, and mermaid-cli; `PUPPETEER_EXECUTABLE_PATH` reuses an
existing Chromium.

## Affected files

- **New:** `handbook2/` (18 chapters), `scripts/build-handbook2.py`,
  `docs/reference/seed-format.md`, `docs/reference/components.md`,
  `docs/explanation/generated-app-anatomy.md`
- **Changed:** `docs/explanation/project-overview.md`, `docs/reference/control-flow.md`,
  `docs/reference/spec-format.md`, `docs/reference/file-map.md`, `README.md`, `CLAUDE.md`,
  `scripts/build-handbook.py`, `utils/claude-skills/dps-nn-seed-authoring/SKILL.md`,
  `utils/claude-skills/dps-nn-spec-authoring/SKILL.md` (added `seedHint`),
  `src/Genesis.Core/Variation/VariationResolver.cs` (header comment only)

## Verified

- Both PDFs build from the current tree: 90 pages (edition 1) and 59 pages (edition 2). The edition-2
  TOC contains all 18 chapters in the intended order; spot-rendered pages checked for overflow,
  table balance and figure legibility.
- Every relative link in the three new `docs/` files resolves.
- The `imageKeywords` contract was confirmed in `SeedStage.BuildSeedSchema` (min/max items by
  `images.mode`) and in the shipped `seeds/facenote-rich-seed.json`.

## Not verified

- The CI handbook workflow with the new script (it is not wired into `handbook.yml`; the proposal
  edition is a local/review artifact for now).
- The README test count and a real `run --all` on Windows in this session (no LocalDB here).

## Found, not fixed

- `--from <unknown-stage>` (including `spec-build`/`spec-expand`) exits 0 after running nothing.
  Documented; the proposal in edition 2 is to fail loudly.
- `plan/codebase-map.md` is a historical artifact with stale environment claims; `plan/` is treated
  as append-only history, so it was left alone.

## Addendum — English naming: the book is titled Genesis

The English edition now displays **Genesis**, not ДПС-НН:

- cover (`scripts/build-handbook.py` `COVER`): big title `Genesis`, top label
  `GENESIS · APPLICATION GENERATOR`, subtitle unchanged;
- running head on every content page: `Genesis · Architecture Handbook`;
- PDF metadata title: `Genesis - Architecture Handbook`;
- edition 2's opening chapters and the project-overview H1 use Genesis; a small "Names" table in
  Chapter 1 keeps the three spellings straight (Genesis / ДПС-НН / `dps-nn`, with the Windows
  executable and env vars on `dps-nn`).

**Conflict, recorded not resolved:** ADR-009 defines *one* user-visible name (`dps-nn` / ДПС-НН)
for the product. Two language editions need two display names, so the ADR needs an amendment for
the split to be consistent. Until then this is a book title, not a product rename: the CLI, README,
CLAUDE.md and the ADRs keep the old name. Chapter 17 of edition 2 lists the split as a proposal.

**Still Bulgarian on the cover:** the logo asset (`gui/src/renderer/public/logo/logo.png`) carries
`ДПС-НН · Дипломни проекти за серсеми · Нова надежда`. The title beside it says Genesis, so a
English-only cover needs a Genesis logo asset — a design decision, not a text edit.

Also appended to edition 2, Chapter 15: **"What the design already makes possible"** — the report
stage as a requirements→code traceability map, `unsupportedRequests` as a feature backlog loop, and
the verifier as an outcome-based gate that could supervise another backend.

## Addendum — the same-seller motivation for variety

Both editions now state *why* variety exists, not just that it does: one developer often delivers
several projects to the same school, sometimes the same class, and those submissions are compared.
If every app shared markup, CSS, variable names and seed rows, two submissions would be visibly one
source.

- Edition 2, Chapter 1: new subsection **"The same seller, many projects"**, with a table of what
  actually varies (code style, naming, presentation, content, feature composition) and an explicit
  boundary — page *structure* still comes from one template set.
- Edition 2, Chapter 11: the variation principle now names that use case and repeats the boundary.
- `docs/explanation/project-overview.md`: one paragraph in "What it is", so edition 1 carries the
  same reasoning.

Nothing about the variation mechanism changed; this is the rationale that was missing from the
documentation, not a new capability.

## Addendum — delivery marks, design process, and an English-only text

Five changes to edition 2:

1. **Chapter 1 opens concretely.** The vague "a web app with these functionalities" paragraph is
   replaced by the actual inventory: accounts, authorization, content, commerce, media, pages,
   presentation, seeded data — then the verification list.
2. **Chapter 17, "The delivered application"** (new, near the end): what a buyer receives item by
   item, a capability-by-capability inventory keyed to the spec field that enables it, and the
   ownership-mark section — marks exist, clean builds are the default, marked builds are produced
   only for chosen seeds with private tooling, and a marked build may behave differently (fail at
   startup, play music, rewrite itself, loop). The mechanism itself is deliberately not described.
3. **Chapter 14, "How the project is built"** (new): the AI-driven development lifecycle and why
   Diátaxis is its doc structure; documentation as an agent interface (router, ADRs, changelog,
   decision log); skills instead of ad-hoc prompts (spec + seed skills, reused in-window and
   embedded in the agent's own prompt); Sonnet/Haiku routing with the author-measured **under
   US$0.17 per project**; the red-green-revert-green rule and "nothing ships on red"; CLI for
   agents and scripts, GUI for people, one engine.
4. **Chapter 5 gains "How much of the output the spec controls"**: controllers, views, DTOs,
   feature fragments, naming, roles, validation messages, theme and pinned axes — the mechanism
   behind spec-level originality.
5. **English-only text.** All Cyrillic removed from the English manuscript (role names, page names,
   the story's bookshop, the seed example, the agent's example description). The Bulgarian name of
   the product is described rather than printed. The cover logo still contains Bulgarian art — a
   separate asset, unchanged.

Also fixed: chapter numbering. The preface is now LaTeX front matter (`\chapter*` + a TOC entry),
so printed chapter numbers match the numbers the prose cites; before, every reference was off by
one. The builder gained a one-line `<!-- front-matter -->` marker for this.

## Addendum — the architecture chapter, the deep dives, and ADRs as characteristics

Three structural additions to edition 2 (now 25 chapters, 96 pages):

1. **Chapter 8, "The architecture, in one picture"** — the missing synthesis. It states the
   architecture in one sentence (a compiler with a verifier, with the model behind a file boundary),
   shows one layered diagram (input → engine → integration boundary → output → front ends), names
   the architectural style and why each alternative lost, catalogues **13 patterns with the place
   each one lives and where it stops paying**, lists the patterns deliberately absent (no
   repository, no CQRS, no DI container in the generator), documents the **coding style** of both
   the generator and the generated app, maps nine problems to the mechanism that answers each, and
   gives a tools table with the rejected alternative per tool. Ends with six checkable claims and a
   reading guide.
2. **Part V, "Deep dives"** — the long-form chapters of edition 1's Parts II–III, included rather
   than paraphrased: CLI reference, GUI architecture, image pipeline, Specifications-page
   consolidation, techniques and traps. They are the same `docs/` files, so there is still one
   source; the English edition renders them through a small build-time term map that removes
   Cyrillic (`bh.TEXT_TRANSFORMS`, new hook in `build-handbook.py`, default empty).
3. **Chapter 1, "Signature choices"** — the ADR decisions stated as observable characteristics of
   the product (14 rows: what you can observe, the choice behind it, the reason), with the ADR index
   left as the record rather than the only place the reasoning appears.

Renumbering: chapters 8–16 and 22–25 shifted; every cross-reference was remapped by target file and
the printed TOC was checked against the prose. Preface ladder, provenance table and reader guidance
updated.

Verified: 26 docs, 11 diagrams, 96 pages; extracted text contains **zero** Cyrillic characters; no
broken relative links in the manuscript; edition 1 still builds (90 pages) with the patched builder.

## Addendum — four editions: English, Bulgarian, German, Galactic

The book now ships in four languages, all from one builder and one manuscript tree.

| Edition | Output | Pages | Title | Type & colour | Artwork |
|---|---|---|---|---|---|
| English | `dist/dps-nn-handbook-v2.pdf` | 96 | Genesis | IBM Plex, navy/cyan | ДПС-НН (as supplied) |
| Bulgarian | `dist/dps-nn-handbook-bg.pdf` | 75 | ДПС-НН | IBM Plex, green + tricolour band | ДПС-НН (as supplied) |
| German | `dist/dps-nn-handbook-de.pdf` | 75 | Genesis | IBM Plex, charcoal/gold + black-red-gold band | авторско лого (black/red/gold) |
| Galactic | `dist/dps-nn-handbook-galactic.pdf` | 110 | Genesis | FreeSans, grass/stone | авторско лого (purple) |

`scripts/build-handbook.py` became the typesetting engine for all four: `FONTSETUP`,
`COVERLOGO`/`COVERLOGOSTYLE`/`FOOTLOGO`, `COVER_TOP`/`COVER_BOTTOM`/`COVER_STRIPE`,
`COVER_TITLE_SIZE`, `BOOK_TITLE`/`BOOK_COVER_TITLE`/`BOOK_META_TITLE`/`BOOK_HANDBOOK_META`,
`BOOK_HANDBOOK`/`BOOK_HANDBOOK_UPPER`, `BOOK_PART_LABEL`, `BOOK_CHAPTER_LABEL`,
`BOOK_TOC_TITLE`, `BOOK_COVER_TAGS`, `BOOK_LICENSE_LABEL`, `TEXT_TRANSFORMS`/`TEXT_TRANSFORM_FN`.
`scripts/build-handbook2.py --lang {en,bg,de,xx}` sets them per edition.

### Defects found and fixed in this pass

| # | Defect | Cause | Fix |
|---|---|---|---|
| 1 | Every cover printed the literal `@HANDBOOKUPPER@` | the placeholder was added to `COVER` but only substituted in the header text | added to the cover substitution chain |
| 2 | German build died with *font "IBMPlexSerif-Regular" cannot be found* | `@FONTSETUP@` was substituted **after** `@PLEX@`/`@EXT@`, so the font block never got its paths | substitute the font block first |
| 3 | Both national flags drawn upside down | TikZ's y axis grows upward; the band rectangles were in top-down order | white/green/red and black/red/gold written bottom-up |
| 4 | Bulgarian, German and English editions all showed the Bulgarian logo | one global `LOGO` | per-edition artwork, `logo_file` key |
| 5 | Artwork appeared on a white card / inside a black box | a white TikZ plate, and (from my own first attempt) an alpha-flattening "background removal" that turned transparent pixels opaque black | plate removed; the supplied PNGs are already transparent and are used unmodified |
| 6 | Galactic edition showed `??` boxes and missing letters | first cut used Deseret; then one font per script could not cover all 26 symbols, and bold/italic faces lacked them entirely | Minecraft Standard Galactic Alphabet, one LaTeX macro per letter pinned to a font that has the glyph (FreeSans / FreeSerif / Noto Sans CJK JP), weight and slant synthesised with `FakeBold`/`FakeSlant` |
| 7 | Diagram labels stayed English in the galactic edition | Mermaid renders before the text transform | label text transliterated in the `.mmd` source (syntax, `<br/>` and node ids untouched; edge labels quoted because the galactic “y” is `||`) — all 14 diagrams pre-rendered to verify |
| 8 | `ARCHITECTUREHANDBOOK` on the cover | letterspacing swallows the `~` tie | explicit `\hspace{0.5em}` (all four editions + the edition-1 default) |
| 9 | Five sections of chapters 8, 11 and 13 were missing from the bg/de translations | translated from memory of the shape, not the text | sections written in both languages; heading parity re-checked for chapters 7–16 |

### Verification

- `pdftotext` scan of all four books: **no** `@PLACEHOLDER@`, **no** Cyrillic in en/de/xx, no English
  headings or furniture in bg (the single `How-to` in de is the Diátaxis quadrant name).
- Every build finishes with zero `Missing character` warnings.
- Covers and sample pages rendered to PNG and inspected for each edition.

### Honest limits

- bg and de are **condensed** translations: same structure and headings, tighter prose than the
  English. Part V is summarised. Both prefaces say so.
- The Galactic edition is a transliteration of the English text, not a translation; the alphabet key
  is printed in its preface.
- The German and galactic artwork the author supplied carries Bulgarian lettering inside the image
  (Дипломни проекти за серсеми / Нова надежда). That is deliberate branding and is left as supplied.
- ADR-009 (one user-visible name vs the English/Bulgarian split) is still an open proposal.
