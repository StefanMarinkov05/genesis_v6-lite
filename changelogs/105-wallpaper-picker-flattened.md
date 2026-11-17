# 105 — GUI: wallpaper picker flattened to 10 clickable images, i18n labels, descriptions removed

**Type:** Feature (GUI)
**Model:** Sonnet, low effort, thinking off — restructuring an already-working picker to a flatter
shape, no new design decisions.

## Background

Changelog 104's theme+skin picker nested skins under their parent theme (5 top-level themes, 3
with sub-items). Stefan asked for a flatter model: 10 clickable options, one per background image,
each carrying its own theme colors/fonts — no separate "pick a theme, then a skin" step, and no
in-menu description text. Confirmed scope before building: picking a wallpaper sets BOTH the
background AND its associated theme's palette in one click (not two independent pickers).

## What shipped

- **`themes.ts` rewritten**: `THEMES`/`SkinMeta`/`normalizeTheme`/`normalizeSkin` replaced with a
  single flat `WALLPAPERS: WallpaperMeta[]` (10 entries — `id`, `theme`, `skin`, `labelKey`) and
  `normalizeWallpaper(id)`. The unwired 10th image from changelog 104 (`nova-code-corridor.png`)
  is now a real picker option (a second NOVA look, not one of the design doc's documented skins —
  flagged as such in the CSS comment); the 2 previously-unused VM poster images
  (`imperator-vm-poster-2/3.png`) are now their own clickable entries too.
- **`styles.css`**: added `[data-theme="nova"][data-skin="code"]` and
  `[data-theme="imperator"][data-skin="poster2"/"poster3"]` blocks for the 3 newly-wired images.
  Simplified `.theme-item` (removed `.theme-desc`/`.theme-skin-item` nesting/indent styling — no
  longer needed for a flat list).
- **i18n**: new `wallpaper` string table (`title` + 10 label keys) filled across all 7
  hand-written language tables (en/bg/de/ru/tr/romani/yoda) with short original names (e.g.
  "Astral Gateway," "Soul Forge," "Code Corridor") — not descriptions, just names, per the
  "remove descriptions" request. Joke tier + SGA auto-inherited via `fromEn`/re-export as usual.
- **`main.ts`**: `buildThemePicker`/`updateThemePicker`/`setWallpaper` (renamed from `setTheme`)
  rewritten for the flat list — each menu item is `t.wallpaper[w.labelKey]`, no description
  sub-line. `retranslate()` now calls `updateThemePicker()` (previously missing — a language
  switch wasn't re-painting the picker's now-translatable wallpaper names).
- **Settings**: `Settings.theme`/`Settings.skin` collapsed into a single `Settings.wallpaper`
  field (shared IPC types + main `settings.ts` + renderer) — one persisted value instead of two,
  since a wallpaper id already fully determines both.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 45/45 (39 prior +
6 rewritten `themes.test.ts` cases for the flat `WALLPAPERS`/`normalizeWallpaper` API: 10 unique
ids, the default's theme/skin, IMPERATOR's 3 posters never borrowing another theme's skin id,
VEIL's exact 2 skin-backed entries, known-id resolution, unknown/missing-id fallback). `i18n.test.ts`
re-run in isolation to confirm the new `wallpaper` key's shape matches across all 14 language
tables. Not verified: on-screen render of the flattened picker — same standing `npm run dev` gate.

## Affected files

- `gui/src/renderer/themes.ts` (rewritten: `WALLPAPERS`, `normalizeWallpaper`)
- `gui/src/renderer/styles.css` (3 new skin blocks, simplified `.theme-item`)
- `gui/src/renderer/main.ts` (picker rewrite, `setWallpaper`, `retranslate()` fix)
- `gui/src/renderer/i18n/{en,bg,de,ru,tr,romani,yoda}.ts` (`wallpaper` table)
- `gui/src/shared/ipc-types.ts` (`Settings.theme`/`skin` → `Settings.wallpaper`)
- `gui/src/main/settings.ts` (persistence)
- `gui/test/themes.test.ts` (rewritten)
- `tasks.md`
