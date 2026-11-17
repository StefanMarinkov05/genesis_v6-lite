# 112 — GUI: hide logo watermark on IMPERATOR only; bigger + lighter elsewhere

**Type:** Bug fix (GUI)
**Model:** Sonnet, low effort, thinking off — screenshot-confirmed CSS adjustment.

## Background

Stefan screenshotted the running app (logo watermark clearly visible and readable over the NOVA
wallpaper — confirming changelog 110/111's fixes worked) and asked for two changes: don't show
the DPS-НН logo watermark over the Veselin Marinov/IMPERATOR wallpaper specifically (that art
already self-brands with his own name/face), and make the logo bigger and lighter everywhere else.

## What shipped

- `[data-theme="imperator"] .logo-watermark { display: none; }` restored — this suppression had
  been removed in changelog 107 in response to an earlier "do not hide it" instruction, but that
  instruction was about not hiding the logo in general (it had been wrongly hidden site-wide at
  the time); it wasn't a standing exception for the VM wallpaper specifically. Every other theme
  keeps showing the watermark at full visibility.
- Size cap raised `min(42%, 320px)` → `min(56%, 420px)`; opacity dialed back `1` → `0.8` so the
  larger mark still reads as a watermark rather than a flat sticker over a busy background.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS change, no logic to test).

## Affected files

- `gui/src/renderer/styles.css` (`.logo-watermark` size/opacity, IMPERATOR suppression restored)
