# Title page carries the spelled-out name, and the book gets a closing plate

**Type:** Docs / tooling (`scripts/build-handbook.py`, `scripts/build-handbook2.py`, trump-bg output)
**Model:** Opus — two small requests: the full name under the abbreviation on the title page, and a big
final page.

## Title page

A `BOOK_COVER_NAME` hook now prints a line directly under the title, before the handbook name. Only
editions that set it get the line — it is substituted as a whole `@COVERNAMELINE@` block, so an edition
without a name produces no stray `\\` (the first attempt broke the English build with *"There's no line
here to end"*).

The Bulgarian retelling sets it to **Дипломни проекти за серсеми: Нова надежда**, in white under
`ДПС-НН`.

## Closing plate

A `CLOSING` template plus `CLOSING_PAGE` switch: a full-bleed final page with the cover gradient, the
edition's national band, the logo, the abbreviation in display type, the spelled-out name, one line of
record, a rule, a sign-off and the version stamp. Skipped entirely when an edition does not ask for it.

For the Bulgarian retelling:

> **ДПС-НН** · Дипломни проекти за серсеми: Нова надежда
> Над петдесет защитени проекта от лятото на 2024 година насам. Нито един провал, за който да се знае.
> *Край. Сега идете да си защитите проекта.*

## Defects hit and fixed on the way

| # | Symptom | Cause | Fix |
|---|---|---|---|
| 1 | `Undefined color '1A3166'` | the closing plate was handed a hex string where the cover takes a *palette name* | the plate defines `endtop`/`endbottom` with `\definecolor[HTML]` |
| 2 | `Extra }, or forgotten $` | a `\\[4pt]` line break inside the sign-off became display math after the escape collapsed | the line is one paragraph, no forced break |
| 3 | the sign-off rendered off the page | `yshift` from `current page.south` moves **down** for negative values | positive offsets (4.2 cm / 4.6 cm) |
| 4 | English build failed after the first attempt at the name line | empty `\\` after an empty group | the whole line is substituted conditionally |

## Verification

- trump-bg: **119 pages**, cover and page 119 rendered and inspected; sign-off and version stamp land
  above the tricolour band.
- English edition rebuilt as a regression check: 98 pages, text diff is only the version hash and one
  table reflow — no content change.

## Scope note

Only the Bulgarian retelling was meant to change. The English edition was rebuilt once as a regression
check, which replaced its committed PDF; that file was restored byte-for-byte to its previous state
(`md5` identical to the build before this work) and the restore is its own commit. `bg`, `de` and
`galactic` were never rebuilt.
