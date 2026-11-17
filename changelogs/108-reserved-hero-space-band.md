# 108 — GUI: real reserved logo/face band, replacing the IMPERATOR-only margin hack

**Type:** Bug fix (GUI)
**Model:** Sonnet, low effort, thinking off — layout correction on Stefan's explicit direction.

## Background

Changelogs 106/107 pushed the run-control row down 48px on IMPERATOR only, as a per-theme margin
hack. Stefan clarified the actual intent: the whole area between the toolbar and the input
panels is reserved space for the logo watermark (and, on IMPERATOR, Veselin Marinov's face) — not
just enough clearance for the run row, and not IMPERATOR-specific. The buttons (and panels) should
never sit in that band on ANY theme.

## What shipped

- Removed the `[data-theme="imperator"] .run-row-wrap { margin-top: 48px; }` hack entirely.
- New `.hero-space` element in `index.html`, between the toolbar and the input panels — an empty,
  `aria-hidden` block, same height on every theme (R1 — one layout, themes recolor, never
  restructure). Height is `26vh` (clamped `140px`–`260px`) so it scales with the window instead of
  a fixed guess.
- `.logo-watermark` repositioned to align with this same reserved band (`top: calc(76px + 2vh)`,
  matching height `26vh`/140–260px) instead of the previous `top: 40px; bottom: 64px` guess from
  changelog 107 — both now describe the SAME conceptual space, so the logo and the reserved gap
  move together if either is tuned later.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 45/45, unchanged
(pure layout change, no logic to test). Not verified: on-screen re-render — Stefan's own
screenshots have been the source of truth for this whole layout thread; a follow-up screenshot
would be the real confirmation, not yet done in this session.

## Affected files

- `gui/src/renderer/index.html` (`.hero-space` element)
- `gui/src/renderer/styles.css` (`.hero-space` rule, `.logo-watermark` realignment, IMPERATOR
  margin hack removed)
- `tasks.md`
