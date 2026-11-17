# 125 — GUI: run-control buttons stacked 2x2, block narrowed to ~1/3 width

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — pure layout redesign, same pattern as changelog 124.

## Background

Following the panels stacking redesign (changelog 124), Stefan asked for the same treatment on
the run-control buttons: Start/Pause stacked above Resume/Stop (2x2 grid instead of one row of
4), and the whole block narrowed to about a third of the window width instead of spanning it.

## What shipped

- `.run-row` grid changed from `repeat(4, 1fr)` to `repeat(2, 1fr)` — the existing 4 buttons
  (Start, Pause, Resume, Stop, in that DOM order) now wrap into 2 rows of 2 automatically.
- `.run-row-wrap` gained `width: min(34%, 420px)` (was full-width/no explicit width).
- Removed the now-empty `@media (max-width: 680px)` block, since its one remaining rule
  (`.run-row { grid-template-columns: repeat(2, 1fr) }`) duplicated the new base rule at every
  width.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS grid/width change, no logic to test).

## Affected files

- `gui/src/renderer/styles.css` (`.run-row-wrap` width, `.run-row` grid, redundant media query
  removed)
