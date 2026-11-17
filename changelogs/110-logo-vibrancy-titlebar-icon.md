# 110 — GUI: real logo in the titlebar icon + more vivid watermark

**Type:** Bug fix (GUI)
**Model:** Sonnet, low effort, thinking off — two small visual corrections.

## Background

Stefan reported the watermark looked grayscale/faded and asked for it to pop more, and separately
asked to swap the small titlebar icon (next to "ДПС-...") for a real logo — it turned out that
icon was never the actual logo image at all, just a CSS conic-gradient placeholder blob left over
from before the real `design/logo.png` asset existed.

## What shipped

- **`.logo-mark`** (titlebar icon, `.titlebar .brand`): replaced the `conic-gradient` placeholder
  with the real `url("/logo/logo.png")` at `contain` sizing, plus a small accent-glow drop-shadow.
  Same asset the centered watermark already uses (changelog 106), just at icon scale.
- **`.logo-watermark`**: opacity raised `0.4` → `0.92`, and added a two-layer accent-tinted
  `drop-shadow` (22px + 46px blur) so the logo actually glows against the dark background instead
  of reading as a faded/grayscale overlay. `drop-shadow` (not `box-shadow`, which only affects the
  element's box, not the transparent PNG's own silhouette) blooms the logo's own edges — matches
  the "glow is the light source" invariant (design-specs.md §1 point 5) every other glowing
  element in the system already follows.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 45/45, unchanged
(pure CSS visual change, no logic to test). Not verified: on-screen re-render — Stefan's own
screenshots have been the source of truth for this layout/visual thread; a follow-up screenshot
would be the real confirmation.

## Affected files

- `gui/src/renderer/styles.css` (`.logo-mark`, `.logo-watermark`)
- `tasks.md`
