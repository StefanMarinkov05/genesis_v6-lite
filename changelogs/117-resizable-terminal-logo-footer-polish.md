# 117 — GUI: resizable terminal, brighter/bigger logo, footer font + full-width polish

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — three independent UI polish requests.

## What shipped

- **Resizable debug terminal**: new drag handle (`.terminal-resize-handle`) above the xterm host.
  `wireTerminalResize()` in `main.ts` tracks mousedown/mousemove/mouseup, sets an explicit pixel
  height on `.terminal-wrap` (clamped 80px–`window.innerHeight - 220px`) and calls `refitTerminal()`
  live while dragging so the xterm content re-flows continuously, not just on release. `.resized`
  class switches `.terminal-wrap` from `flex: 1` to `flex: 0 0 auto` so the explicit height isn't
  fought by the flex layout.
- **Logo watermark**: size cap raised again (56%/420px → 68%/520px per changelog 115, now
  further), opacity back to 1, plus `filter: brightness(1.25) drop-shadow(...)` for a genuine
  glowing-centerpiece look rather than a subtle watermark.
- **Footer**: switched from the plain technical-mono body font to the ACTIVE THEME's own display
  font (`var(--font-display)` + its transform/tracking/weight), so it stays visually in-family
  across NOVA/VEIL/FORGE/ENCHANT instead of one fixed font clashing with every theme. Full-width
  (`width: 100%`), larger (`--scale-caption` → `--scale-body`), a soft accent glow, and
  `overflow-wrap: break-word` so long footer text wraps cleanly instead of overflowing.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure UI/CSS + one small drag-handler function; no new branching logic complex enough to warrant
a unit test — drag-resize is exercised the same way every other pointer-driven UI interaction in
this codebase is, via the standing manual `npm run dev` gate).

## Affected files

- `gui/src/renderer/index.html` (`.terminal-resize-handle` element)
- `gui/src/renderer/main.ts` (`wireTerminalResize`, wired into `wire()`)
- `gui/src/renderer/styles.css` (resize-handle CSS, logo size/brightness, footer font/layout)
