# 093 — GUI phase 3b: six languages + flag picker

**Type:** Feature
**Model:** Opus, high effort, thinking on — cross-cutting renderer work with a real ambiguity
(GUI-chrome language vs. generated-app language) that had to be resolved correctly against the
project's hard "Bulgarian-only generated UI" requirement, not pattern-copied. Routed to Opus
deliberately (the other phase-2/3 GUI items are Sonnet-tier and were left for a Sonnet session).

## Summary

Added the five non-English GUI languages (BG, DE, RU, Romani, SGA) and a flag/language picker to
the Electron GUI, completing phase 3b of the 6.5.0 GUI plan. The MVP shipped English-only with the
i18n *seam* already in place (`i18n/en.ts` + `applyStrings()`); this fills the seam.

**The load-bearing decision — a boundary, not a feature.** These six languages control **only the
GUI's own chrome** (its buttons, headings, toasts). They are renderer-local (the main process is
language-agnostic, per the arch doc) and have **zero** effect on the language a *generated app* is
produced in — generated apps stay Bulgarian-only (CLAUDE.md hard requirement) unless the *spec's*
`meta.language` says otherwise, which is a completely separate axis (changelog 054). Conflating the
two is exactly the "two paths, one concept, drift" bug class this project has been bitten by before,
so the boundary is documented loudly in `i18n/en.ts`, the arch doc, and here. Because language is
renderer-local, it persists through the **existing** `settings.language` field — **no new IPC
channel**, so the IPC contract (and its same-commit doc) is untouched.

## Techniques

- **Widened `Strings` type for compile-time shape enforcement.** `en.ts` is authored `as const`
  (narrow literal types). A `Widen<T>` conditional type widens the string literals to `string`
  while preserving nested objects and function signatures (`progress.of`, `error.title`,
  `toast.stageDone`). Every translation table is typed `Strings`, so a missing/renamed/mistyped key
  is a **tsc error**, not a runtime blank label — the "table fill, not a re-wire" guarantee the
  original `en.ts` comment promised, now enforced by the compiler.
- **SGA = font swap, not translation.** Per the arch doc, the Standard Galactic Alphabet "language"
  is the English strings with a font swap. `sga.ts` re-exports `en` verbatim; selecting it toggles
  a `.lang-sga` class that swaps `--font-display`/`--font-body`. The actual enchanting glyph font is
  owned by the ENCHANT theme (design-specs §176), a separate phase-3 theme task — until it lands the
  seam falls back to the normal faces (SGA renders as English). Honest boundary: the seam is complete
  and correct; only the glyph asset is deferred to the task that owns it.
- **Flags as inline SVG, not PNG.** The design doc said "flag PNGs from `images/`", but no flag
  binaries exist and the renderer's CSP forbids external assets. Inline SVG (BG/DE/RU tricolors, a
  simplified Union Jack for EN, the blue/green + red-chakra Romani flag, a runic tile for SGA) is
  CSP-safe, self-contained, and needs no binary assets to verify blind. Updated the arch-doc i18n
  bullet to say SVG (same-commit doc rule).
- **Live `retranslate()` without losing state.** Switching language re-applies every already-painted
  string in place: static labels via `applyStrings()`, then restores the selection-driven wells, any
  live progress (from a `lastProgress` snapshot, guarded to non-idle states), the select
  placeholders (preserving the current selection), and re-renders an open report/error screen. No
  reload, no lost selection or run state.
- **Data-driven picker.** The picker (collapsed flag button + menu) is built from the `LANGUAGES`
  metadata list, so adding a language is a table fill + one registry entry — no picker rewiring.

## Romani caveat (flagged, not hidden)

Romani has no single standardized orthography, many dialects, and no established native software
vocabulary. `romani.ts` is **best-effort, not native-verified**: it uses well-attested common words
where a natural equivalent exists (phandav, putar, ačh, alosar, garav, mudar, čhavi, gata…) and
keeps technical nouns as recognizable loanwords (specifikacia, seme, parola, baza). The file header
says this explicitly and asks for a native-speaker review before it's treated as final. Shipping it
still fully exercises the switch + picker machinery, which is the point.

## Verification

- `tsc --noEmit` clean (the widened `Strings` type would have caught any incomplete table).
- `electron-vite build` — all 3 targets green; confirmed the built bundle contains all six
  languages' strings and the `lang-picker` container.
- `node --test` — **14/14** (3 existing report-parser + 11 new i18n): every table matches the EN
  shape exactly, SGA is EN by identity, unknown codes fall back to EN, function-valued strings
  render, and every language has a non-empty inline SVG flag.
- **NOT verified** (needs a display — this is the manual `npm run dev` gate that is Stefan's, same
  boundary as the phase-1 MVP): the on-screen render of the picker and a live language switch. The
  logic is build/test-verified; the pixels are the eyeball gate.

## Affected files

- `gui/src/renderer/i18n/en.ts` (widened `Strings` type + scope-boundary note)
- `gui/src/renderer/i18n/{bg,de,ru,romani,sga}.ts` (new string tables)
- `gui/src/renderer/i18n/index.ts` (new — language registry, `stringsFor`, `langMeta`, `normalizeLang`)
- `gui/src/renderer/i18n/flags.ts` (new — inline SVG flags)
- `gui/src/renderer/main.ts` (live language state, picker build, `setLanguage`, `retranslate`)
- `gui/src/renderer/index.html` (`#lang-picker` container in the toolbar)
- `gui/src/renderer/styles.css` (picker + menu styles, `.lang-sga` font-swap seam)
- `gui/test/i18n.test.ts` (new — 6 tests / 11 assertions)
- `docs/explanation/gui-architecture.md` (i18n bullet: phase-3b done, SVG-not-PNG, no new channel, boundary)
