# 118 — GUI: fix dropdown-overlap bug, logo clipping, bigger chips, move logo asset

**Type:** Bug fix + polish (GUI)
**Model:** Sonnet, low effort, thinking off — 4 screenshot-confirmed issues.

## Background

Stefan's screenshot showed a real bug (theme picker's dropdown stayed open after clicking the
music button — two menus visible simultaneously) plus three polish asks: move the source logo
file into `design/app-icon/` alongside the app icon assets, make the toolbar buttons bigger, and
fix the logo watermark appearing cut off / behind a box.

## What shipped

- **Real bug: dropdown-overlap fixed.** Each picker (language/theme/music) independently called
  `e.stopPropagation()` inside its own toggle button's click handler — so clicking picker B's
  toggle never bubbled up to picker A's `document.addEventListener('click', closeA)`, meaning A
  stayed open when B opened. New `closeAllPickerMenus(except?)` helper closes all three menus
  before any one of them opens; every `toggleXMenu()` function now calls it first.
- **Logo asset moved**: `design/logo.png` → `design/app-icon/logo.png` (git-tracked as a rename).
  The vendored runtime copy the app actually loads (`gui/src/renderer/public/logo/logo.png`) was
  already separate and untouched — this only reorganizes the source file.
- **Real bug: logo clipping fixed.** `.logo-watermark`'s `background-size` was a fixed width value
  (`min(68%, 520px) auto`) — the logo PNG's own aspect ratio meant its auto-computed height could
  exceed the watermark box's own `26vh` height, so the bottom of the logo rendered underneath
  `.panels`' opaque background right where the reserved band ends (Stefan: "appears cut / behind
  some box"). Switched to `background-size: contain`, which guarantees the whole image always
  fits inside the box regardless of aspect ratio.
- **Bigger toolbar chips**: `.chip` padding `4px 10px` → `8px 16px`, font-size `10px` → `12px`.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(the dropdown fix is new branching logic in existing toggle functions, not a new module — no
dedicated unit test added; picker open/close state is exercised the same way every other
pointer-driven UI interaction in this codebase is, via the standing manual `npm run dev` gate).

## Affected files

- `design/app-icon/logo.png` (renamed from `design/logo.png`)
- `gui/src/renderer/main.ts` (`closeAllPickerMenus`, updated toggle functions)
- `gui/src/renderer/styles.css` (`.logo-watermark` background-size, `.chip` sizing)
