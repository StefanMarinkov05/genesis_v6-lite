# Trump-bg handbook — voice rewritten, edition label removed, track record added

**Type:** Docs
**Model:** Opus — `docs/handbook/trump-bg/` only (26 chapters) plus one line in
`scripts/build-handbook2.py`.

## What was wrong

The edition read as a stump speech rather than a book, and one joke had worn through:

| Symptom | Count before | Count after |
|---|---|---|
| `„Господине…“` formula (once in nearly every chapter) | 27 | **0** |
| vocative `хора` | 82 | **14** |
| `повярвайте ми` | 21 | **1** |
| `Тръмп` as an edition label (prose, running head, cover badge, version line) | 5 places | **0** |

The preface also carried a political disclaimer the author asked to remove, and the full name of the
product was never expanded anywhere in the book.

## Changes

1. **Edition label gone.** Running head is `ДПС-НН`; the version line reads `· българско издание`;
   the cover badge reads **ИЗДАНИЕ ЗА ПОБЕДИТЕЛИ** instead of `ИЗДАНИЕТО ТРЪМП`; the preface's status
   section describes the edition instead of naming a persona. It is documentation, not a stump tour.
2. **Political disclaimer removed** (`без подкрепа, без политика, без кампания… Единствените неща,
   които се обиждат тук, са бъговете…`). Replacement states what the edition is and keeps the one
   honesty rule that matters: where an adjective and a table disagree, the table wins, because the
   table is measured.
3. **Full name expanded** — `ДПС-НН` = **Дипломни проекти за серсеми: Нова надежда** — in the
   preface and at first use in chapter 1.
4. **Track record stated, not hidden:** *над петдесет защитени дипломни проекта от лятото на 2024
   година насам — ученици в програмите на SoftUni и студенти в университети.* Preface (as the
   reason the tool looks the way it does) and chapter 1 (as the concrete form of the problem
   statement).
5. **Twenty-six openings rewritten.** Every chapter now opens with its own construction instead of
   the same `Хората ме питат: „Господине…“` device: a question the reader is about to ask, a
   reviewer's reaction, a concession, a number, a claim followed by its evidence. The register stays
   boastful; the rhythm stops repeating.
6. **Tics cut by ~80%**, with a wider vocabulary for the interjections that remain, and the ones left
   carry meaning (`изгубването е за хора без карта`, `тестерите…`).
7. **Earned praise added** in chapters 5, 7, 13, 15, 17, 19, 21 and 23 — for the programmer (domain
   judgment from real defences, the decision to keep marking outside the pipeline, the discipline of
   the changelog) and for Claude (the stages written session after session, the narrow repair lever
   that makes short memory irrelevant, the CLI designed so an agent can drive it). No praise is
   free-floating: each sits next to the evidence it refers to.

## Preserved deliberately

Technical content, tables, code blocks, Mermaid diagrams, ADR references, numbers and chapter
structure are untouched. Verified: table-row counts and code-fence counts identical to `HEAD` for all
26 files; `git diff --stat` = 263 insertions / 202 deletions, i.e. prose level only.

## Verification

```
python3 scripts/build-handbook2.py --lang trump-bg   # 112 pages, no Missing character warnings
pdftotext … | grep -c "Тръмп\|Господине"            # 0, 0
```
Covers and body pages rendered to PNG and inspected (badge, tricolour band, 50-project paragraph in
place). No other edition touched.

## Addendum - typography and the name map

- **Em dashes removed** (458 of them, incl. en dashes) in favour of a plain hyphen, along with the
  four furniture strings that carry one (part titles, contents title, licence line). The built book
  now contains zero `—`; verified with `pdftotext`.
- **The acronym is counted correctly**: ДПС-НН is **five** letters and one hyphen, not four.
- **The name map is now the real one**, in the preface and in chapter 1's naming table: **ДПС-НН is
  the agent**; **Genesis** is one of the tools under it (the pipeline that builds the application);
  the other two tools are the LLMs **Sonnet** and **Haiku**, which write the input files (spec and
  seed). Previously the book said Genesis *was* the same product under an English name, which
  contradicted how the toolchain is actually split.
- Title page states the product's promise: it generates diploma projects, ASP.NET Core MVC
  applications; the original handbook-v2 mark is printed under the preface introduction.

Rebuilt: 112 pages, no missing glyphs, no em dash in the extracted text.
