# 124 — GUI: stack Specification/Seed panels vertically instead of side by side

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — pure layout redesign.

## Background

Stefan's screenshot asked for the Specification and Seed input panels to be stacked (Spec on
top, Seed below) instead of the existing side-by-side 2-column layout.

## What shipped

`.panels` grid changed from `grid-template-columns: 1fr 1fr` to `grid-template-columns: 1fr` —
the two panels (already in Spec-then-Seed DOM order) now stack vertically. Removed the now-
redundant `@media (max-width: 680px) { .panels { grid-template-columns: 1fr; } }` override, since
the base rule already matches it at every width.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS grid change, no logic to test).

## Affected files

- `gui/src/renderer/styles.css` (`.panels` grid, redundant media-query rule removed)
