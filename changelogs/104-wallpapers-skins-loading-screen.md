# 104 — GUI: real background wallpapers + skins + a full-bleed loading screen

**Type:** Feature (GUI)
**Model:** Sonnet, medium effort, thinking on — per the Phase 3 routing breakdown (the loading-
animation look was a real small design judgment call, not pure pattern-copy like prior groups).

## Background

Two remaining Phase 3 items, previously blocked/deferred:
- Per-theme background images (`design-specs.md` §8 decision 4 deferral) — no real art existed
  until Stefan supplied 10 images under a new `design/backgrounds/` folder this session.
- A dedicated full-bleed "generation/loading screen" (`design-specs.md` §5) — never built; progress
  only ever showed inline on the same setup screen.

Scope-confirmed with Stefan before building: (1) freedom to map the 10 supplied images to however
many theme/skin slots made sense, not forced into exactly 5; (2) per-project wallpapers (the
spec's literal wording — "image link from that project's spec") explicitly OUT of scope — no
`project-spec.json`/schema change; the loading screen shows the active GUI theme's wallpaper,
not something tied to the generated project.

## What shipped

### Background images
10 images reviewed and mapped to themes/skins per their actual content (matched against
`design-specs.md` §4's own descriptions, not guessed): 4 Veselin-Marinov branding posters →
IMPERATOR (one picked as default, comment flags the other 3 as available for a future "pick your
VM poster" refinement — not wired to a switch); ember/molten circuit → FORGE; Islamic calligraphy
→ VEIL's Islamic/Sufi skin; "Veil of Isis" occult art → VEIL's own default AND its Occult skin;
German culture (Bayern, football, cars) → a new NOVA skin (German/"Das Deutsche Herz" — Imperator
is ruled out for this per decision #1, VM-exclusive); blue astronomical HUD → NOVA's default;
blue phoenix/astral art → ENCHANT; a blue code-corridor image copied but not wired to a theme
(no clear additional slot, kept for a future refinement).

Vendored to `gui/src/renderer/public/backgrounds/*.png` (Electron's CSP + no-external-assets rule
means these must ship in the bundle, not be linked externally). Wired via a new `--canvas-image`
custom property, layered UNDER `.canvas-layer`'s existing gradient stack (not replacing it) — the
gradients now function as a per-theme color wash over the real photo instead of being the whole
background, so R8/§6 contrast rules apply exactly as before; `var(--canvas-image, none)` means any
theme/skin without an assigned image still falls back to gradient-only, unchanged from before this
session.

### Skins (design-specs.md §4 "Skins documented from the art")
`gui/src/renderer/themes.ts`'s `ThemeMeta` gained an optional `skins?: SkinMeta[]` — NOVA now
carries the German skin, VEIL carries Islamic/Sufi + Occult. Applied as a `data-skin` attribute
alongside `data-theme` (`[data-theme="veil"][data-skin="islamic"] { ... }`), inheriting the parent
theme's full token set and overriding only `--canvas-image` (+ accent for the German skin, matching
the black-red-gold flag colors) — exactly the "background+accent swap only" skin model the design
doc specifies. New `settings.skin` field (main + shared IPC types + renderer), persisted the same
way `settings.theme` already was. `normalizeSkin(themeId, skinId)` guards a stale/cross-theme skin
id (e.g. a persisted "islamic" skin surviving a switch to NOVA, which has no such skin) from
silently applying the wrong background.

The theme picker's dropdown now nests skin sub-items under their parent theme (indented, ↳ prefix)
rather than needing a second menu.

### Loading screen
New full-bleed `.loading-screen` (reuses the existing `.result-screen` scrim/centered-card
pattern already used by report/error) — a `.loading-card` with a 3-dot pulse spinner (respects
`prefers-reduced-motion`, same as every other animation in the system per R4), the current stage
name, `index/total` count, and a progress bar mirroring the setup screen's own `#progress-fill`
(one source of truth in `setProgress()`, painted to both places). Shown via `setLoadingScreen()`,
hooked into `setRunState()`: open only for `'running'`/`'awaiting-llm'`, closed for every other
state (idle/paused/done/error/stopped return to the setup screen or their own full-bleed screen).

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 47/47 (42 prior +
5 new — `themes.test.ts` gained skin-registry and `normalizeSkin` coverage: VEIL's exact 2 skins,
NOVA's German skin, FORGE/IMPERATOR/ENCHANT having none, cross-theme-skin rejection, empty/
undefined/null handling). Not verified: on-screen render of the new loading screen / skin picker
against a live pipeline run — the standing `npm run dev` gate covers the base render Stefan
already confirmed, not this specific new screen; a live click-through is a reasonable next manual
check.

## Follow-up: descriptive filenames (same session)

Renamed all 10 source images in `design/backgrounds/` (originally opaque auto-generated names —
`ChatGPT Image Jul 11, 2026, 10_23_32 PM.png`, `Gemini_Generated_Image_3v0t273v0t273v0t.png`,
etc.) and their vendored `gui/` copies to describe their actual theme/skin assignment (e.g.
`nova-astro-hud.png`, `veil-islamic-sufi-skin.png`, `imperator-vm-poster-1.png`). Updated every
`--canvas-image: url(...)` reference in `styles.css` to match. Rebuilt and re-tested clean
(tsc/build/47 tests) after the rename to confirm nothing broke.

## Follow-up: lightened the darkening (same session)

Stefan reported the wallpapers rendered too dark across every theme (including the 3 VM/IMPERATOR
posters). Two stacked darkening layers were the cause: the `.canvas-layer` gradient wash
(`color-mix` over `--canvas-image`) and the separate `.scrim-layer` on top of it. Lightened both,
app-wide, not just IMPERATOR (Stefan's explicit scope): the linear-gradient wash's `color-mix`
opacity dropped from 55%/40%/55% to 30%/18%/30%, and every theme's `--scrim` alpha dropped by
roughly a third (e.g. NOVA 0.62→0.42, IMPERATOR 0.68→0.46, FORGE 0.6→0.4). Text legibility (R8)
is unaffected by this change — body/heading text always sits on `surface.panel`/`panel-raised`,
which are separately near-opaque (`rgba(...0.86-0.93)`) regardless of scrim/wash values; only the
raw canvas visible in the gaps between panels got brighter. Rebuilt/re-tested clean.

## Known cost, not addressed this session

The 11 vendored PNGs total ~25MB unoptimized (source ChatGPT/Gemini-generated images, largest
~3MB each). A compression/resize/WebP-conversion pass would meaningfully shrink the Electron
bundle but wasn't requested and is scope creep beyond "wire the wallpapers" — flagged here as a
reasonable follow-up, not silently done or silently ignored.

## Affected files

- `gui/src/renderer/public/backgrounds/*.png` (new, 11 files)
- `gui/src/renderer/styles.css` (`--canvas-image` wiring, skin blocks, loading-screen CSS)
- `gui/src/renderer/themes.ts` (`SkinMeta`, `ThemeMeta.skins`, `normalizeSkin`)
- `gui/src/renderer/main.ts` (skin-aware theme picker, `setLoadingScreen`, progress mirroring)
- `gui/src/renderer/index.html` (loading-screen markup)
- `gui/src/shared/ipc-types.ts` (`Settings.skin`)
- `gui/src/main/settings.ts` (`skin` persistence)
- `gui/test/themes.test.ts` (5 new tests)
- `docs/explanation/gui-architecture.md`
- `tasks.md`
