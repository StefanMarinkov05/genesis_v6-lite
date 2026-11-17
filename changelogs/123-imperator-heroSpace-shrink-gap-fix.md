# 123 — GUI: shrink IMPERATOR's hero-space to close the dead gap above the panels

**Type:** Bug fix (GUI)
**Model:** Sonnet, low effort, thinking off — screenshot-confirmed layout fix.

## Background

Stefan's screenshot showed a large empty gap between the bottom of the VM poster art (below
Veselin Marinov's outstretched hands) and the input panels — the `40vh` IMPERATOR-specific
hero-space band from changelog 119 turned out to be too generous once the "1337" mark was
actually confirmed visible near the panels' own top edge; the extra reserved height was just
empty wallpaper, not covering anything.

## What shipped

`[data-theme="imperator"] .hero-space` reduced from `40vh` (`220–380px`) to `28vh`
(`160–260px`) — enough to keep the "1337" mark clear (the original goal of changelog 119)
without leaving a large dead zone above the panels.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS height value, no logic to test).

## Affected files

- `gui/src/renderer/styles.css` (`.hero-space` IMPERATOR override)
