# 113 — GUI: toolbar reorder, panel/terminal swap, footer emptied on IMPERATOR

**Type:** Feature/bug fix (GUI)
**Model:** Sonnet, low effort, thinking off — three screenshot-confirmed layout changes.

## Background

Stefan's screenshots (confirming changelog 112's IMPERATOR fix worked — full VM art visible, no
logo double-branding) asked for three further layout adjustments:
1. Toolbar control order changed (theme picker moved to the far right).
2. Debug terminal and the spec/seed input panels swap vertical position.
3. Footer text emptied on IMPERATOR specifically, but the footer's space kept — so the bottom of
   the Veselin Marinov art stays visible, matching the "see the top AND bottom of the image" ask.

## What shipped

- **Toolbar**: `index.html`'s two `.group` divs reordered. Left group now holds Music → Flag →
  Last result → Recent projects → Debug → Settings, in that order; the theme picker moved into
  its own trailing group. The toolbar's existing `justify-content: space-between` (unchanged)
  pushes that lone group to the far right automatically — no new CSS needed.
- **Panel/terminal swap**: `.terminal-wrap` moved above `.panels` in the DOM (was below the run
  row/progress bar). Both elements' own CSS (`position`, `padding`, `flex`) already work
  independent of surrounding siblings, so this was a pure reorder.
- **Footer on IMPERATOR**: new `applyFooterVisibility()` in `main.ts` — the `.footer` element and
  its layout space are UNCHANGED on every theme (never removed/collapsed), but its `textContent`
  is cleared specifically when `currentWallpaper.theme === 'imperator'`, restored on any other
  theme. Hooked into `setWallpaper()` (fires on every wallpaper switch) and `boot()` (fires once
  at startup, after the real footer text is fetched via `footerGet()` and cached in
  `lastFooterText`).

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure layout/DOM-order change plus one small conditional-text function, no new logic branch
complex enough to warrant a dedicated unit test — `applyFooterVisibility` is a one-line ternary
exercised the same way every other theme-conditional CSS/DOM toggle in this codebase already is,
via the standing manual `npm run dev` gate). Not verified: on-screen re-render — Stefan's own
screenshots have been the source of truth for this whole layout thread.

## Affected files

- `gui/src/renderer/index.html` (toolbar group reorder, terminal/panels swap)
- `gui/src/renderer/main.ts` (`applyFooterVisibility`, `lastFooterText`, boot/setWallpaper hooks)
