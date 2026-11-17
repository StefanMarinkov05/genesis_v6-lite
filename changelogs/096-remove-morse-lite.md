# 096 — Remove Morse (lite) entirely

**Type:** Cleanup
**Model:** Sonnet, low effort — deleting an already-disabled feature and its residue.

## Summary

`morseLite` (the "buttons only" Morse variant that kept long sentences readable in English) was
already commented out of `LANGUAGES`/`CATALOG` in `i18n/index.ts` — not user-reachable — but its
source file, flag SVG, and test coverage were still present. Removed all of it per request: it's
gone from the codebase, not just the picker.

## Changes

- Deleted `gui/src/renderer/i18n/morse-lite.ts`.
- `gui/src/renderer/i18n/index.ts`: dropped the commented-out import/type-union entry/`LANGUAGES`
  entry/`CATALOG` entry, and the doc-comment references.
- `gui/src/renderer/i18n/flags.ts`: removed the `morseLite` SVG emblem and its `FLAGS` entry.
- `gui/src/renderer/i18n/{hex,morse-full}.ts`: dropped now-dangling comment references to
  `morse-lite.ts`.
- `gui/test/i18n.test.ts`: `LANGUAGES` count 15 → 14; removed `morseLite` from the joke-tier list
  and the dedicated lite-vs-full comparison test, replaced with a `morseFull`-only equivalent.

## Verification

`tsc --noEmit` clean, `node --test` — 31/31 pass, `electron-vite build` — all 3 targets green.

## Affected files

- `gui/src/renderer/i18n/morse-lite.ts` (deleted)
- `gui/src/renderer/i18n/index.ts`
- `gui/src/renderer/i18n/flags.ts`
- `gui/src/renderer/i18n/hex.ts`
- `gui/src/renderer/i18n/morse-full.ts`
- `gui/test/i18n.test.ts`
