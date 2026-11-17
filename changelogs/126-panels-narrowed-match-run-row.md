# 126 — GUI: narrow the Specification/Seed panels to match the run-controls block width

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — pure sizing change.

## Background

Changelog 124 stacked the Specification/Seed panels vertically but never constrained their
width, so they still spanned nearly the full window (unlike the run-controls block, narrowed to
`min(34%, 420px)` in changelog 125). Stefan sketched the target box size directly on a
screenshot; confirmed via a clarifying question that the ask was to shrink the panels to that
same narrow size, keeping their current position (not relocate them elsewhere).

## What shipped

`.panels` gained `width: min(34%, 420px)` — the exact same width constraint already used by
`.run-row-wrap` (changelog 125), so the Specification and Seed panel boxes now match the run
buttons' width. Position unchanged (still directly below `.hero-space`, left-aligned).

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS width change, no logic to test).

## Affected files

- `gui/src/renderer/styles.css` (`.panels` width)
