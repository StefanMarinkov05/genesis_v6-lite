# 127 — GUI: real CSS Grid layout (two-column: terminal left, controls right)

**Type:** Feature (GUI)
**Model:** Sonnet, medium effort, thinking off — structural port of a layout confirmed via an
interactive HTML/CSS simulation before touching the real app.

## Background

Changelogs 106-126 iterated on this screen's layout through many rounds of hand-guessed
`position: absolute` percentage offsets — logo watermark, hero-space band, toolbar order,
panel/terminal swaps — each round fixing one visual bug while risking a new one at a different
window size (the dropdown-overlap bug in changelog 118, the "1337" gap in changelog 123, etc.).
Stefan asked for a live simulation to confirm the target layout before another blind port; that
simulation (built and iterated in a separate Claude Artifact, not in this repo) converged on a
two-column CSS Grid design, confirmed against his own hand-drawn box diagram overlaid on a real
screenshot.

## What shipped

`.content` is now a real CSS Grid (not a flex column with hand-placed absolute-positioned
children):

```
grid-template-columns: 1fr min(34%, 420px);
grid-template-rows: auto 1fr auto;
grid-template-areas: "titlebar titlebar" "leftcol rightcol" "footer footer";
```

- **`.leftcol`** (new wrapper): holds the debug terminal. This structurally replaces the old
  `.hero-space` band (a guessed vh value) — the grid area itself reserves the same visual space,
  at any window size, with no separate empty div needed. When Debug is off, `.leftcol` is simply
  an empty grid cell and the background wallpaper shows through unobstructed.
- **`.rightcol`** (new wrapper): holds the toolbar, input panels, run controls, and progress bar,
  in that order — a flex column with `overflow: hidden` (confirmed intent: this column must
  always fit in view; if it doesn't, that's a sizing bug to fix, never something to hide behind a
  scrollbar). Folds in what `.toolbar-right` used to do (single-column stacking, chip/picker
  full-width rules).
- Removed entirely: `.hero-space` (both the base rule and the IMPERATOR-specific taller override
  from changelog 119/123) — the grid area supersedes it. Removed the now-redundant
  `position/z-index/width` declarations from `.run-row-wrap`, `.progress-wrap`, `.panels`,
  `.toolbar` — their parent `.rightcol` now owns positioning and width for all of them.
- `main.ts` needed **no changes** — every element ID stayed the same, only their DOM nesting
  (which wrapper div they sit inside) and CSS changed.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49, unchanged
(pure layout/CSS restructuring, no logic touched). Not verified: on-screen re-render — the
standing `npm run dev` gate; Stefan is opening a new chat session next specifically to do this
eyeball check and report back anything the grid port missed.

## Affected files

- `gui/src/renderer/index.html` (`.leftcol`/`.rightcol` wrappers, `.hero-space` div removed)
- `gui/src/renderer/styles.css` (`.content` grid, `.leftcol`/`.rightcol` rules, `.hero-space`
  removed, redundant positioning stripped from child rules)
