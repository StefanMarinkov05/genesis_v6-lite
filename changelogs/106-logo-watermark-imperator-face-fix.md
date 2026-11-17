# 106 — GUI: centered logo watermark (all wallpapers except IMPERATOR) + VM face visibility fix

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — asset wiring + one layout nudge, no design ambiguity.

## Background

Stefan added a transparent-background project logo (ДПС-НН / "Нова надежда" wordmark with a
laurel wreath and C# emblem) to `design/`. Asked for it centered over every wallpaper except the
3 Veselin Marinov/IMPERATOR posters (that art already self-brands — same reasoning as the existing
`.logo-mark` suppression, design-specs.md §8 decision 2), and for Veselin Marinov's face to stay
visible on IMPERATOR by moving the run-control buttons lower.

## What shipped

- Renamed the source file (`Gemini_Generated_Image_q4l7rj...-removebg-preview.png`) to
  `design/logo.png`; vendored a copy to `gui/src/renderer/public/logo/logo.png` (same pattern as
  the background wallpapers and the SGA font — Electron's CSP forbids external assets, so
  anything the renderer displays has to ship in the bundle).
- New `.logo-watermark` layer in `index.html`, sitting between `.scrim-layer` (z-index 1) and
  `.content` (z-index 2) at z-index 1.5 — above the scrim so it reads over any background, below
  every real UI element so it never competes with panels/controls, `pointer-events: none` so it's
  purely decorative (never intercepts clicks). Centered, capped at `min(46%, 360px)` width so it
  scales with the window without ever dominating a small one. `[data-theme="imperator"]
  .logo-watermark { display: none; }` suppresses it on all 3 VM wallpapers.
- `[data-theme="imperator"] .run-row-wrap { margin-top: 48px; }` — pushes the run-control row (and
  everything below it) down specifically on IMPERATOR, since the VM poster art's face sits in the
  upper-middle of the frame where the spec/seed panels and run row would otherwise sit directly
  over it. No other theme needed this (their art is abstract or already clear at the top).

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green (confirmed `logo.png` present in
the built `out/renderer/logo/` output). `node --test`: 45/45, unchanged (this is pure CSS/asset
wiring, no logic to unit-test). Not verified: on-screen render — same standing `npm run dev` gate.

## Affected files

- `design/logo.png` (renamed from opaque auto-generated filename)
- `gui/src/renderer/public/logo/logo.png` (new, vendored copy)
- `gui/src/renderer/index.html` (`.logo-watermark` div)
- `gui/src/renderer/styles.css` (`.logo-watermark` rule, IMPERATOR suppression, IMPERATOR run-row
  margin)
- `tasks.md`
