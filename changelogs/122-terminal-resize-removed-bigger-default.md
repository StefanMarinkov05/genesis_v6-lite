# 122 — GUI: remove terminal drag-resize, bigger default terminal size

**Type:** Feature/cleanup (GUI)
**Model:** Sonnet, low effort, thinking off.

## Background

Stefan asked to remove the manual drag-resize handle added in changelog 117 (keeping xterm's own
scrollback/scrollbar, which was never affected), and to make the terminal's default size bigger.

## What shipped

- Removed `.terminal-resize-handle` (HTML element, CSS, and `wireTerminalResize()` in `main.ts`
  along with its call site in `wire()`) entirely — the terminal is no longer manually resizable.
- `.terminal-wrap` given a bigger starting size: `flex: 1 1 320px; min-height: 220px` (was
  `flex: 1` with no basis/minimum, which let it end up cramped depending on whatever space was
  left over after the other fixed-size rows). It still grows with the window via flex-grow, just
  from a larger starting point.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure UI removal + CSS sizing, no logic to test).

## Affected files

- `gui/src/renderer/index.html` (resize-handle element removed)
- `gui/src/renderer/main.ts` (`wireTerminalResize` + call site removed)
- `gui/src/renderer/styles.css` (`.terminal-wrap` sizing, resize-handle rules removed)
