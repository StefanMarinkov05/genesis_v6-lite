# 107 — GUI: fix logo watermark overlapping the footer + wallpaper picker text wrapping

**Type:** Bug fix (GUI)
**Model:** Sonnet, low effort, thinking off — two real, screenshot-confirmed layout bugs.

## Background

Stefan screenshotted the running app after changelog 106 and found two real problems:
1. The new logo watermark (`.logo-watermark`, `inset: 0`) spans the FULL window, so its centered
   position landed right on top of the footer strip at the bottom — the footer text ("last-
   verified...", "Footer content...") and the logo were rendering on top of each other, both
   unreadable.
2. The wallpaper picker's dropdown wraps several labels to 2 lines ("ВЕСЕЛИН МАРИНОВ I", "DAS
   DEUTSCHE HERZ", "ИСЛЯМСКИ СУФИ ПЪТ") — the 260px max-width + `--scale-body` font from changelog
   105 was too narrow for the longer real-language strings.

## What shipped

- **`.logo-watermark`** repositioned from `inset: 0` to `top: 40px; bottom: 64px; left: 0; right:
  0` — anchored to the titlebar-to-footer span (the actual visible content area) instead of the
  whole window, so it centers there and never reaches down into the footer strip. Also reduced
  size (`min(46%, 360px)` → `min(40%, 300px)`) and opacity (0.5 → 0.4) slightly, since the tighter
  anchor box made the previous size feel oversized relative to it.
- **`.theme-menu`** widened (`max-width: min(260px, 90vw)` → `min(320px, 92vw)`).
  **`.theme-item`** font dropped from `--scale-body` to `--scale-mono` (smaller) plus `white-
  space: nowrap` + `text-overflow: ellipsis` — every wallpaper label now renders on exactly one
  line regardless of language or length; an unusually long future label would ellipsis rather
  than wrap and break the menu's fixed-height item rows.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 45/45, unchanged
(pure CSS layout fix, no logic to test). Not verified: on-screen re-render to confirm the fix
visually — Stefan's own screenshot was the bug report, so a follow-up screenshot would be the
real confirmation, not yet done in this session.

## Affected files

- `gui/src/renderer/styles.css` (`.logo-watermark` anchor/size, `.theme-menu`/`.theme-item` sizing)
- `tasks.md`
