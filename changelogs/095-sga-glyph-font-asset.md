# 095 — SGA glyph font asset lands the deferred seam from 093

**Type:** Feature
**Model:** Sonnet, low effort — single asset drop + one `@font-face` rule into an already-complete
seam (see 093), no architecture decisions.

## Summary

Adds the actual Standard Galactic Alphabet font file, closing the gap changelog 093 flagged as
deferred: `"SGA"` was referenced by `--font-display`/`--font-body` but had no `@font-face` backing
it, so selecting the SGA language silently fell back to the normal faces.

`Sga-Regular.ttf` (public-domain "Standard Galactic Alphabet" font, maps Latin A-Z to
Minecraft-enchanting-table-style runes) is now vendored at
`gui/src/renderer/public/fonts/Sga-Regular.ttf`. Files under `<rendererRoot>/public/` are Vite's
static-asset convention — copied byte-for-byte to the build root, referenced with an absolute
`/fonts/...` URL, no bundler config changes needed.

## Fix

`.window.lang-sga` was missing `--font-technical` in its override list, so monospace/technical-styled
text (native `<select>` dropdowns' surrounding labels, the seed/spec "no selection" placeholders,
toasts) stayed in the fallback font even after the glyph font landed. Added the missing variable
alongside `--font-display`/`--font-body`.

## Verification

Built (`electron-vite build`), confirmed `Sga-Regular.ttf` copied to `out/renderer/fonts/`. Launched
the real built app (`electron.exe out/main/index.js --remote-debugging-port`), drove it via a raw
CDP WebSocket client (no Playwright/puppeteer in this repo's deps — hand-rolled the minimal
handshake+frame client in Node's builtin `net`/`crypto`, throwaway, not committed), clicked the SGA
picker entry, screenshotted: headings, buttons, toasts, and placeholder text all render as galactic
glyphs. Two things correctly do **not** remap: native `<select>` option text (Windows/Chromium
renders `<option>` content with the OS combobox font regardless of CSS `font-family` on some
builds — a platform limitation, not a wiring bug) and the Cyrillic "ДПС-НН" wordmark (SGA only maps
Latin A-Z, expected — no Cyrillic glyphs exist in the font).

## Affected files

- `gui/src/renderer/public/fonts/Sga-Regular.ttf` (new, vendored binary asset)
- `gui/src/renderer/styles.css` (`@font-face` rule + `--font-technical` added to `.lang-sga`)
