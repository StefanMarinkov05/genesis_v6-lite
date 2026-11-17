# 094 — GUI toolbar/seed-row overflow fixes + four more joke languages

**Type:** Bug fix + feature
**Model:** Sonnet, low effort, thinking off — the CSS fixes are mechanical (same class as
changelog 093's toolbar wrap fix); the four new languages follow the exact generator pattern
already established in 093, no new architecture.

## Summary

Two follow-ups from Stefan's screenshot review of changelog 093's language picker.

### 1. Overflow fixes

- **Language dropdown rendered behind the seed panel.** `.toolbar` and `.panels` were sibling
  elements at the *same* `z-index: 4` — equal-z-index siblings stack by DOM order, so `.panels`
  (later in the HTML) painted over `.toolbar`, regardless of the dropdown's own `z-index: 20`
  (a descendant's z-index can never escape its ancestor's stacking context). Bumped `.toolbar` to
  `z-index: 5` (still under modal overlays at 6/7/8).
- **Toolbar's rightmost button clipped at narrow window widths.** No wrap behavior on the
  right-side button group. Added `flex-wrap: wrap` to `.toolbar` and `.toolbar .group`.
- **Claude button in the seed panel still cut off** (same class of bug, different container):
  `.select-row` (select + Upload + Claude) had no wrap either. Added `flex-wrap: wrap` on the row
  and `flex-shrink: 0` on both buttons, so they drop to their own line instead of clipping.

### 2. Four more joke languages: Turkish (real) + Pirate, l33t, upside-down, Yoda

Stefan asked for Turkish as a real language, plus my own follow-up suggestions from 093 (all
four): Pirate speak, l33tspeak, mirrored/upside-down text, and Yoda-speak.

- **Turkish (tr)** — real hand-written translation, same personality tone as the existing tables.
- **Pirate** — whole-word dictionary substitution (hello→ahoy, stop→belay, settings→ship's log…),
  case-preserving, with a stock "Arrr," prefix on longer sentence-shaped strings.
- **l33tspeak** — the classic character table (A→4, E→3, I→1, O→0, S→5, T→7, B→8, G→6, Z→2, L→1).
- **Upside down / mirrored** — a Unicode flip-table (each letter maps to a lookalike glyph) plus
  a full string reversal, since flipping text also reverses reading order.
- **Yoda** — the one **hand-written** table in the joke tier (see below).

**New shared machinery** (`encoders.ts`): a `fromEn(leafTransform, wrapFn)` helper that deep-walks
the real EN table and applies a transform to every string leaf, optionally wrapping function
leaves so their *output* gets the same transform. This is what let Morse/hex/sign (093) and now
pirate/leet/flip be **generated, not hand-transcribed** — zero risk of a mistyped dot-dash, hex
pair, or leet substitution, and the `Strings` type still enforces the shape at compile time.

**Yoda is hand-written, not generated — flagged honestly, same as Romani (093).** Natural Yoda
word order ("Started, the build has") requires real per-sentence linguistic judgment a leaf
transform can't safely make without butchering random sentences. Each of the ~45 strings was
individually reordered by hand; the header comment says so explicitly.

## A real bug caught during review (not shipped)

Spot-checking the actual encoded output (not just "does it typecheck") caught a genuine
correctness bug: `pirate.ts` originally wrapped function-valued entries like every other
generated language. But `error.title(stage)` is called with a **caller-supplied value** — the
real pipeline stage name, e.g. `"seed"` — and `PIRATE_WORDS` happens to map `seed → "buried
treasure"`. Wrapping the function meant the whole-word dictionary swap ran on the *composed*
string, silently turning `Build failed at stage "seed"` into `Build failed at stage "buried
treasure"` — not a joke anymore, a **wrong error message** that would mislead whoever's debugging
a real failed generation. This is the identical failure class to changelog 070's BG residue-map
bug (`"Email" → "Имейл"` corrupting `EmailModel`): a blind text transform colliding with a value
that must survive verbatim. Fixed by setting `wrapFn: false` for pirate — function outputs stay
plain English, matching the precedent `morseLite` already established for "don't touch dynamic
content." Character ciphers (hex/morse/flip/leet) don't share this failure mode — they have no
word-boundary semantics to collide with a real identifier — so they safely keep wrapping function
outputs. A regression test (`pirate NEVER dictionary-swaps a caller-supplied value inside a
function output`) pins this.

## Verification

- `tsc --noEmit` clean.
- `electron-vite build` — all 3 targets green.
- `node --test` — **32/32** (11 new: shape-match ×4 languages, joke-tier grouping widened to 8
  entries, pirate word-swap + capitalization, pirate's function-output regression guard, leet
  digit substitution, flip reversal, yoda reordering + interpolation survival).
- Manually rendered every new language's actual strings (not just shape-checked) via a Node
  one-liner importing the real registry — this is what caught the pirate bug above; a
  shape-only test would have shipped it.

## Affected files

- `gui/src/renderer/styles.css` (toolbar/panels z-index fix, toolbar + select-row flex-wrap)
- `gui/src/renderer/i18n/encoders.ts` (pirate/leet/flip encoders added)
- `gui/src/renderer/i18n/{tr,pirate,leet,flip,yoda}.ts` (new — 4 generated + 1 hand-written table)
- `gui/src/renderer/i18n/index.ts` (registry: +5 LangCode entries, joke-tier metadata)
- `gui/src/renderer/i18n/flags.ts` (5 new inline-SVG emblems: TR flag, pirate skull, l33t tile,
  upside-down "flip" tile, Yoda lightsaber-glow tile)
- `gui/test/i18n.test.ts` (11 new tests, incl. the pirate regression guard)
