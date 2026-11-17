# 120 — GUI: fix theme/music dropdown menus floating away from their buttons

**Type:** Bug fix (GUI)
**Model:** Sonnet, low effort, thinking off — screenshot-confirmed layout bug.

## Background

Stefan's screenshot showed the theme picker's dropdown rendering far from its own toggle button —
near the top-right corner of the window instead of directly below the button. The language
picker's dropdown was unaffected.

## What shipped

Root cause: `.theme-menu` and `.music-menu` both used `left: 0`, anchoring the menu box's LEFT
edge to their parent picker. Both pickers live inside `.toolbar-right`, a narrow column
(`min(220px, 60vw)`) pinned to the far right of the window — a `min-width: 220-280px` menu
anchored by its left edge, inside a column that narrow at the window's right edge, overflows past
the visible window, and Chromium's own overflow-avoidance repositioning shoves it away from the
button entirely (exactly what the screenshot showed). `.lang-menu` was already correctly anchored
`right: 0` (from an earlier session) and never had this problem — that was the tell.

Changed both `.theme-menu` and `.music-menu` from `left: 0` to `right: 0`, matching `.lang-menu`'s
already-correct approach — the menu's right edge aligns with the picker's right edge, so it always
opens leftward/downward and stays fully inside the visible window regardless of how narrow
`.toolbar-right` is.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS positioning fix, no logic to test).

## Affected files

- `gui/src/renderer/styles.css` (`.theme-menu`, `.music-menu` anchor point)
