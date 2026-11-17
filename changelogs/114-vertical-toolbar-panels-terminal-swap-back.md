# 114 — GUI: vertical toolbar stacks, panels/terminal swapped back

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — pure layout restructure.

## Background

Stefan wanted a different toolbar layout than changelog 113's single-row order: two vertical
stacks instead. Left stack: theme picker on top, language picker below it, the (IMPERATOR-only)
"Play Veselin Marinov" button below both. Right stack: Settings, Debug, Recent projects, Last
result, top to bottom. Also asked to swap the debug terminal and the spec/seed input panels back
to their original order (panels above the terminal) — undoing changelog 113's swap.

## What shipped

- **`index.html`**: the toolbar's two `.group` divs restructured — left group now contains
  theme-picker → lang-picker → music-picker in that DOM order; right group contains
  settings → debug-toggle → recent-open → toolbar-reopen.
- **`styles.css`**: new `.group-vertical` modifier overriding `.toolbar .group`'s default
  `flex-direction: row` with `column` (`align-items: stretch`, `flex-wrap: nowrap`) so each
  control stacks full-width instead of wrapping as inline chips. Each picker/button inside gets
  `width: 100%` (and toggle buttons get `justify-content: center`) so the column reads as a clean
  stack rather than left-aligned chips of varying width.
- **`index.html`**: `.panels` moved back above `.terminal-wrap` (reverting changelog 113's swap).

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS/DOM-order change, no logic to test). Not verified: on-screen re-render — Stefan's own
screenshots have been the source of truth for this whole layout thread.

## Affected files

- `gui/src/renderer/index.html` (toolbar column restructure, panels/terminal order)
- `gui/src/renderer/styles.css` (`.group-vertical`)
