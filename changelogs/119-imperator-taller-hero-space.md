# 119 — GUI: taller reserved hero-space band on IMPERATOR

**Type:** Bug fix (GUI)
**Model:** Sonnet, low effort, thinking off — screenshot-confirmed layout fix.

## Background

Stefan's screenshot showed the spec/seed panels and run-control row sitting directly on top of
the "1337" mark in the VM poster art — the shared `26vh` hero-space band (changelog 108) wasn't
tall enough for this specific wallpaper, since its content sits lower in the frame than the other
9 wallpapers' art.

## What shipped

`[data-theme="imperator"] .hero-space { height: 40vh; min-height: 220px; max-height: 380px; }` —
a taller reserved band specifically for IMPERATOR (all 3 poster variants), pushing the panels/run
row further down so the "1337" mark stays clear. Every other theme keeps the original shared
`26vh` band (R1 — one layout; this is a theme-scoped exception for one wallpaper's specific art,
not a restructuring of the shared layout itself).

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS height value, no logic to test).

## Affected files

- `gui/src/renderer/styles.css` (`.hero-space` IMPERATOR override)
