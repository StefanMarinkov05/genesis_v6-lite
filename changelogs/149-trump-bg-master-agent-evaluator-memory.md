# Trump-bg handbook — master agent, evaluator, and the agents' memory

**Type:** Docs (`docs/handbook/trump-bg/`, 6 chapters)
**Model:** Opus — the author corrected the architecture: everything is part of the project.

## The correction

The previous pass described the defence chain as seller-side tooling around the generator. Wrong, and now
fixed: the agent layer **is part of the project**, and it has a shape.

| Fact | Where it now lives |
|---|---|
| one **master agent** orchestrates the whole workflow and controls the others | ch. 23 new section, ch. 1 cycle table |
| an **evaluator agent** scans every document for low-quality AI output | ch. 23 new section, ch. 14 fifth gate |
| agents run **in parallel** and are **not created per product** | ch. 23 memory section, ch. 1 |
| they keep memory as **RAG** (facts) plus **fine-tuning** (habits), globally and **per school** | ch. 23, ch. 12, ch. 6, ch. 1 |
| their configuration is **private because it handles personal data** | ch. 23, ch. 6, ch. 12 |

The old wording ("продавачът държи локална база", "списъкът живее при продавача") was replaced: the
memory belongs to the system, and only its configuration is private.

## Quality gate

Chapter 14 went from four gates to five: schema, compilation, migration, verify — and the **evaluator**,
which judges the four text artefacts by a rule stated in one line: if a sentence could sit in another
project unchanged, it says nothing about this one.

## Fresh comparisons

Nine, each used exactly once, all new to the book: "по-стегнато от операционна зала" (ch. 3), "приятел,
който отваря капака преди дълъг път" (ch. 4), "не е ракета, е трактор: тежък, предвидим, оре прав"
(ch. 12), "по-подозрителен от митничар на летище и по-строг от държавен изпит" (ch. 23), "по-дълга памет
от слон, и то с папки" (ch. 23), plus the earlier seven devices from changelog 148. No line was taken
verbatim from a source; the register's **patterns** are reused, not its quotations.

## Verification

`--lang trump-bg` → **118 pages**, no missing glyphs, no em dashes; the new pages (11, 68, 107-108)
rendered and read. No other edition touched.
