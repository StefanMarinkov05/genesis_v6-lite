**Type:** Feature / change
**Model:** Sonnet — large batch of well-scoped GUI feature/polish work following established
patterns, plus one root-cause CSS debugging session with real ambiguity (routed to deeper analysis
inline rather than an Opus handoff, since context was already loaded).

## Summary

Large GUI session covering: a music player rework for "Play Veselin Marinov" (IMPERATOR theme),
a titlebar/branding pass, a boot splash screen, and an i18n audit.

## Music player

- Split `design/music/` into 3 category subfolders: `songs-audio/`, `concerts-audio/`,
  `concerts-video/` (was one flat folder). `MusicCategory` type threaded through
  `ipc-types.ts` → `main/paths.ts` → `main/music.ts` → `main/ipc.ts` → `preload` → renderer.
- Replaced the old toolbar dropdown menu (never wired a close-on-outside-click listener) with a
  proper closable/resizable/maximizable popup dialog (`#music-sheet`), same pattern as the debug
  terminal, with tabs per category, a track list, and a seek-bar player.
- Added `gui/src/main/media-protocol.ts` — a registered-privileged `app-media://` custom protocol
  with a hand-written HTTP Range handler. Two real bugs this fixes:
  - Raw `file://` media URLs only load in production (renderer served from `file://`,
    same-origin); in dev the renderer is `http://localhost:5173` and Chromium blocks `file://`
    media as cross-origin ("no supported source was found" regardless of path encoding).
  - Seeking mid-track was resetting to 0:00 — the original `net.fetch`-based handler didn't
    reliably serve `206 Partial Content` + `Content-Range`, so a seek re-fetched the whole file
    from byte 0. Range requests are now handled by hand via `fs.createReadStream(path, {start,
    end})`.
- Added Pause/Resume transport control (previously only Stop existed); rewired the seek-bar
  listeners to bind once at boot instead of once per `playLocalFile()` call — the old version
  stacked a fresh set of listeners on the persistent `<audio>`/`<video>` elements on every track
  change, which was a second, compounding cause of the "seek jumps back" symptom.

### The track-list text bug (root-cause debugging)

Symptom: Cyrillic filenames in the (dynamically rebuilt, ~80-item) track list rendered as
garbled "?" / half-visible text, while the exact same string in a native `title=""` tooltip
rendered perfectly.

Four theories tried and ruled out in sequence (each left in place only long enough to test, then
reverted once disproven):
1. `.music-item` inheriting a Cyrillic-hostile font stack — disproven; it didn't override
   `font-family` at all, same as the correctly-rendering tooltip.
2. GPU text-rasterization cache corruption — `app.disableHardwareAcceleration()` didn't fix it.
3. DirectComposition/GPU-compositing interaction — `--disable-direct-composition` /
   `--disable-gpu-compositing` didn't fix it either (and would have been actively harmful long
   -term for an app whose main feature is playing video).
4. DOM insertion race (80 individual `appendChild` calls each triggering a reflow) — batching
   into one `DocumentFragment` insert measurably improved the symptom but didn't fully clear it,
   which in hindsight was a real secondary factor, not the root cause.

Actual root cause: `.music-track-list` is `display:flex; flex-direction:column` and
`.music-item` has `overflow:hidden` (needed for `text-overflow:ellipsis`). Per the Flexbox spec,
a flex item with `overflow` other than `visible` gets an automatic minimum size of **0** instead
of min-content, and flex children default to `flex-shrink:1`. With ~80 rows whose combined
height dwarfs the container, the flex algorithm shrank every row's height below its own text
height, and `overflow:hidden` clipped the bottom of each line — read from a distance/screenshot,
this looked like character corruption. Fixed with one line: `.music-item { flex: none; }`.
This explains every piece of prior evidence at once: only the flex-column list was affected
(tabs are a flex *row*, no vertical shrink pressure), the tooltip was unaffected (no CSS box to
clip), and none of the font/GPU/DOM-timing fixes touched vertical flex-shrink.

## Other GUI changes this session

- Boot splash screen (`#boot-splash`, user-supplied `loading.png`) shown from page load until
  `boot()` fully completes.
- Titlebar: replaced the full app-title text with a short "HQ" badge (`t.hq`, localized
  including Cyrillic "ДПС-НН" for bg/ru), swapped the corner icon for a user-supplied
  `icon.png`/`icon.ico` (also wired as the Electron window/taskbar icon via
  `gui/resources/icon.ico`).
- App title/Settings label changed from "DPS-NN — Genesis" to "Control Center"/"Контролен
  център" (localized).
- FORGE-theme-specific watermark using a user-supplied `logo-hell.png`.
- ASCII-art terminal banner written on first mount (before Start is ever clicked), including
  app version, copyright line, and a Bulgarian log-style line.
- Removed the "Yoda" joke language entirely (`yoda.ts` + its flag + registry/test entries).
- Reworked "Sign language" (joke language) to pick a random 1-3 hand-gesture emoji, re-rolled on
  every `stringsFor('sign')` call instead of a single fixed glyph baked in at module load.
- i18n audit: found and fixed real hardcoded-English gaps ("Settings saved" toast, the settings
  icon's native tooltip, `hq` badge missing in de/tr/romani, German `toolbar.debug` was "Nerd
  mode" not "Nerd-Modus") across all 6 hand-written languages; correctly left proper
  nouns/brand names/acronyms untouched.
- `nova-code-corridor` wallpaper reordered to index 0 (new default on first launch).
- Version bump to 6.6.1.

## Repo/media handling

`design/music/` is user-supplied media (80 songs, 9 concert recordings, 8 concert videos —
~7.9GB total). Per-file sizes go up to ~2GB, over GitHub's hard 100MB-per-file limit, so this
requires Git LFS. Free GitHub LFS quota is only 1GB storage/bandwidth per repo per month, so only
a small curated subset is actually tracked in git (51 of the smallest songs + the smallest
concert video, ~957MB total): `.gitattributes` LFS-tracks `design/music/**/*.{mp3,mp4,webm,mov}`,
`.gitignore` excludes the rest of `design/music/{songs-audio,concerts-audio,concerts-video}/*`
(force-added the tracked subset with `git add -f`). The rest of Stefan's local music library
stays local-only, same as `out/`/`prompts/out/` — not a repo concern.

## Affected files

`gui/src/main/{index,ipc,music,music-parser,paths}.ts` (+new `media-protocol.ts`),
`gui/src/preload/index.ts`, `gui/src/shared/ipc-types.ts`,
`gui/src/renderer/{index.html,main.ts,styles.css,themes.ts}`,
`gui/src/renderer/i18n/*.ts` (yoda.ts deleted), `gui/test/*.test.ts`, `gui/package.json`,
`.gitignore`, `.gitattributes`, `design/app-icon/*`, `design/music/*` (LFS subset).
