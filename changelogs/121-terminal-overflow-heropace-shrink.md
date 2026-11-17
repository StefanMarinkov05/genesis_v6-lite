# 121 — GUI: fix Debug mode pushing the footer off-window; shrink dead hero-space

**Type:** Bug fix (GUI)
**Model:** Sonnet, low effort, thinking off — two screenshot-confirmed layout issues.

## Background

Stefan's screenshot showed two problems: leftover dead vertical space between the toolbar and
the input panels on the NOVA/German wallpaper, and toggling Debug mode pushing the footer
entirely below the visible window instead of the terminal compressing to fit.

## What shipped

- **Real bug: `.content` lacked `min-height: 0`.** A flex column's default `min-height: auto`
  refuses to shrink below its content's natural height — so when the `flex: 1` terminal grew
  (Debug toggled on, or manually resized per changelog 117), the whole `.content` column grew
  past `.window`'s own `overflow: hidden` boundary instead of the terminal being force-shrunk to
  fit. Added `min-height: 0` (lets flex-shrink actually engage) and `overflow: hidden` (belt and
  braces) to `.content`.
- Added `max-height: calc(100vh - 220px)` to `.terminal-wrap.resized` as a hard safety cap — even
  a large manual drag-resize can never itself exceed available window space.
- **Shrunk `.hero-space`** from `26vh` (`140–260px`) to `22vh` (`120–220px`) — the shared band was
  leaving visible dead space on wallpapers whose art doesn't need as much clearance as IMPERATOR's
  posters (which keep their own separate `40vh` override from changelog 119, untouched).

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS layout fix, no logic to test).

## Affected files

- `gui/src/renderer/styles.css` (`.content`, `.terminal-wrap.resized`, `.hero-space`)
