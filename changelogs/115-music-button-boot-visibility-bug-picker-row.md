# 115 — GUI: fix "Play Veselin Marinov" not showing on restored IMPERATOR wallpaper + picker layout

**Type:** Bug fix + feature (GUI)
**Model:** Sonnet, low effort, thinking off — one boot-ordering bug, one layout change.

## Background

Stefan reported the "Play Veselin Marinov" button never showed when the app opened directly on
an IMPERATOR wallpaper (persisted from a previous session) — only appeared after manually
re-picking the wallpaper. Separately asked for the language picker to sit next to the theme
picker (same row), with the music button in its own row below that pair.

## What shipped

- **Real bug found and fixed**: `boot()` called `buildMusicMenu()` (which ends with
  `updateMusicPickerVisibility()`) BEFORE restoring `currentWallpaper` from persisted settings —
  so visibility was always evaluated against the module-level default (NOVA), hiding the button
  even when the actual persisted wallpaper was IMPERATOR. Reordered `boot()` so the wallpaper
  (and its `data-theme`/`data-skin` attributes) are restored first, and `buildMusicMenu()` runs
  after.
- **Layout**: new `.picker-row` wrapper places the theme picker and language picker side by side
  (each taking half the row, `flex: 1`); the music button (still IMPERATOR-only, still hidden on
  every other wallpaper) now sits in its own full-width row underneath that pair, inside the same
  left-hand vertical toolbar stack from changelog 114.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(the fix is a call-order change in `boot()`, not new branching logic — no new unit test
warranted; visibility correctness for a given wallpaper is already exercised manually via the
standing `npm run dev` gate). Not verified: on-screen re-render confirming the button now shows
on a restored IMPERATOR session — Stefan's own screenshots have been the source of truth for
this whole thread.

## Affected files

- `gui/src/renderer/main.ts` (`boot()` reorder)
- `gui/src/renderer/index.html` (`.picker-row` wrapper)
- `gui/src/renderer/styles.css` (`.picker-row` layout)
