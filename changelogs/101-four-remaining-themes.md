# 101 — GUI: the 4 remaining themes (IMPERATOR/VEIL/FORGE/ENCHANT) + a real theme picker

**Type:** Feature (GUI)
**Model:** Sonnet, medium effort, thinking off — per the Phase 3 routing breakdown. Standard
implementation following an already-vetted token reference, no new design decisions to make.

## Background

`design/design-specs.md` §4 fixed the 5-theme lineup back in the Task-2 design phase (Opus), and
`design/preview.html` already carried a fully tuned, vetted CSS reference for all 5 themes' token
values (surfaces, text, accent/glow, display fonts, radius) — this was the working reference the
design doc itself points implementers at. The MVP (changelog 073) shipped only NOVA; the other 4
were the last "descriptor fill" item left in Phase 3 (tasks.md).

## What shipped

- **4 new `[data-theme="..."]` token blocks** in `gui/src/renderer/styles.css`, ported directly
  from `design/preview.html`'s vetted values — IMPERATOR (gold-baroque, italic serif display),
  VEIL (violet arcane, Palatino-family serif), FORGE (ember industrial mono, sharp radius), ENCHANT
  (cyan-magenta aurora, wide-tracked sans). Each declares exactly the same token set NOVA's own
  block does (`--canvas-*`, `--scrim`, `--panel*`, `--inset`, `--border-hairline`, `--text-*`,
  `--accent*`, `--glow`, `--font-display`, `--display-*`, `--radius-mul`) — no new tokens invented,
  per design-specs.md's own component-spec rule that components reference only semantic tokens.
- **IMPERATOR logo suppression**: `[data-theme="imperator"] .logo-mark { display: none; }` — the
  one theme where `structure.showLogo` is permanently false (design-specs.md §8 decision 2, VM art
  self-brands). Ported from preview.html's identical rule, not reinvented.
- **ENCHANT + SGA**: confirmed the existing global `.window.lang-sga` font-swap rule (changelog
  093/095) already applies across every theme including ENCHANT — added a comment instead of a
  redundant theme-scoped override, after checking CSS cascade order would have made a naive
  `[data-theme="enchant"].lang-sga` override lose to the existing global rule anyway (same
  specificity, declared later in the file).
- **A real theme picker** (previously: the toolbar just showed a static "NOVA" chip with no way to
  switch). New `gui/src/renderer/themes.ts` (THEMES registry, `normalizeTheme`/`themeMeta` — same
  shape as `i18n/index.ts`'s LANGUAGES pattern), wired into `main.ts` mirroring the language
  picker's build/open/close/select functions exactly. Selecting a theme sets `.window`'s
  `data-theme` attribute directly; every component already reads only CSS custom properties off
  that attribute, so switching required zero other renderer changes. Persists through the
  **already-existing** `settings.theme` field (`gui/src/main/settings.ts` had a `theme` field
  wired end-to-end since the MVP; the renderer had just never consumed it) — no new IPC channel.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 37/37 (34 prior +
3 new — `THEMES` registry shape, unknown-id fallback to NOVA, `themeMeta` resolution). Not
verified: on-screen render/switch (the standing `npm run dev` manual gate, same boundary as every
other GUI session).

## Not yet done (deferred, per design-specs.md §8 decision 4)

Real background images (`images/bg/*.jpg` per theme) and theme-specific frame-ornament art/loading
animation remain deferred — all 5 themes currently render the same CSS-gradient canvas
approximation (`.canvas-layer`'s radial-gradient stack), just with theme-specific color tokens.
This was already the documented boundary for NOVA in the MVP; nothing new was deferred here.

## Affected files

- `gui/src/renderer/styles.css` (4 new theme blocks, IMPERATOR logo-suppress rule, theme-picker CSS)
- `gui/src/renderer/themes.ts` (new)
- `gui/src/renderer/main.ts`, `index.html` (theme-picker wiring, boot-time restore)
- `gui/test/themes.test.ts` (new)
- `docs/explanation/gui-architecture.md` (theming section update)
- `tasks.md`
