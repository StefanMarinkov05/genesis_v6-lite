# Handbook — the Trump edition (a parody voice over the audited English text)

**Type:** Docs
**Model:** Opus — 26 chapters retold in a new voice with every technical fact held fixed, then
re-synced against the generator fixes of changelog 141, which landed during the rewrite.

## Summary

The author asked for a new, separate edition: the English handbook retold in Donald Trump's speaking
style. That means superlatives, repetition, asides and "believe me". The book praises Genesis, the
AI agent and the developer. It insults only bugs, boring docs and lesser tools. Facts and
explanations stay unchanged. Later the author added one rule: **no references to people, parties or
ideologies**. Catchphrases that name nobody ("witch hunt", "strong borders") are allowed. The clean
English v2 is untouched.

## What was made

- **`handbook2/trump/00–25`**, all 26 chapters.
  - Tables, code blocks and Mermaid diagrams are copied verbatim from `handbook2/`. The voice lives
    only in the prose and the titles.
  - Chapters 17–21 are condensed retellings of `docs/reference/cli.md`,
    `docs/explanation/{gui-architecture, image-pipeline, specifications-consolidation, techniques}.md`.
    They are written in the voice, like the bg/de editions, and the full docs stay in `docs/`.
  - The preface carries a "Status of this edition" disclaimer. It is a parody of a speaking style,
    with no endorsement and no politics, and when a superlative and a table disagree, the table
    wins.
  - Running nicknames for real defects:
    - "Low-Energy `GetHashCode()`" (the old preset pick, now retired);
    - "Sleepy No-Op" (`--from spec-build` / `--from spec-expand` run nothing; still open).
- **`scripts/build-handbook2.py`**: a new `--lang trump` variant (`dir="trump"`), which writes
  `dist/dps-nn-handbook-trump.pdf`.
  - Palette: navy, flag red and gold.
  - Fonts: Playfair Display Black for display text (titles and headings) and Montserrat Light for
    the cover subtitle, both from TeX Live. Body, tables and code stay IBM Plex for full glyph
    coverage.
  - Cover:
    - 13 red/white stripes under a gold band with white stars;
    - a "THE TRUMP EDITION ★★★" badge;
    - top label "MAKE DIPLOMA PROJECTS GREAT AGAIN" — this reading of "MDGA" is an **assumption**
      from the file name, because the images are not opened;
    - the `gui/src/renderer/public/logo/MDGA.png` mark, bounded to 0.62 page width × 0.30 page
      height with its aspect ratio kept.
  - Footer: three gold stars instead of the logo, drawn with TikZ.
  - Part titles and book labels are in the voice.
  - The term map (`TEXT_TRANSFORMS`) is off for this edition, as it is for bg/de.

## Fact sync with changelog 141

Changelog 141 fixed the preset hash, made `verify` strict with an anonymous-refusal check (and
stamped the missing `[Authorize]`), and fixed `cart` and `personalization`. The Trump chapters
written before it were corrected to match:

- ch01, 02, 03, 08, 09, 10, 12, 15 and 21;
- 22, 23, 24 and 25 were written against the new text.

The `GetHashCode()` passages are now a "comeback" story, and the old lenient-gate paragraph became
the story of the strict gate finding the missing `[Authorize]`.

**Stale lines left in the English source** (owned by the changelog-141 work, not edited here):

- `handbook2/01-what-it-is.md:127`: the signature-choices row still says an unpinned preset is
  "picked by a process-randomised hash";
- `handbook2/15-how-the-project-is-built.md:103`: "pin the design preset";
- `handbook2/24-glossary.md`:
  - the Design preset row: "not yet reproducibly";
  - the Verify row: "(non-200 is logged, not fatal)";
- `handbook2/25-decisions.md:16`: the ADR-004 cost "the preset pick still deviates".

The de edition likely mirrors some of these (`de/24`, `de/25:11`).

## Verification

- Build: `PUPPETEER_EXECUTABLE_PATH=… python3 scripts/build-handbook2.py --lang trump`. It exits 0
  with 26 docs, 9 diagrams, **98 pages** and **0 "Missing character" warnings**. Playfair's coverage
  of — and ↔ was checked with `fc-query`.
- Pages inspected as PNG:
  - the part page, a chapter opener with its diagram, the CLI tables and the footer stars;
  - the cover's top and bottom bands.

  The cover's middle, where the MDGA logo sits, was **not** viewed (project rule: images are judged
  by file name only). Its size is bounded so it cannot reach the title or the top rule.
- Every chapter was grepped for references to people, parties and ideologies, and none were found.
