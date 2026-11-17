# 116 — GUI: collapse toolbar to one right-hand column

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — pure layout restructure.

## Background

The 2-column toolbar split (changelog 114) covered Veselin Marinov's face on posters I/III with
the left-hand column. Stefan asked to collapse everything into a single right-hand column
instead — deliberately not symmetric — in a specific order: Language, Settings, Debug, Recent
projects, Last result, Wallpaper (theme picker), Music.

## What shipped

- `index.html`: both toolbar `.group` divs merged into one `.toolbar-right` column, in the
  requested order.
- `styles.css`: new `.toolbar-right` (`margin-left: auto; width: min(220px, 60vw)`) pushes the
  single column to the right edge, replacing the old two-group `justify-content: space-between`
  split. Removed the now-dead `.picker-row` (theme+language side-by-side row) and duplicate
  `.group-vertical` width rules from changelog 115/114 — `.toolbar-right` covers the same sizing
  for every control in the merged column.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure CSS/DOM layout change, no logic to test).

## Affected files

- `gui/src/renderer/index.html` (toolbar merged to one column)
- `gui/src/renderer/styles.css` (`.toolbar-right`, dead-CSS cleanup)
