# 111 — GUI: "Play Veselin Marinov" button (IMPERATOR only)

**Type:** Feature (GUI)
**Model:** Sonnet, medium effort, thinking off — standard IPC + renderer wiring, no design
ambiguity once scope was confirmed.

## Background

Stefan asked for a play button on the IMPERATOR/VM wallpapers with two source options: an
external YouTube link, or a local MP3. Confirmed scope before building: both options via one
button/dropdown (not two separate controls); local files come from a new `design/music/` folder
Stefan populates himself, picked via a random-shuffle mechanism (not a fixed single file) plus a
"choose a specific track" list; the YouTube URL is a placeholder until Stefan supplies the real
one.

## What shipped

- **`design/music/`** (new, empty) — Stefan drops his own MP3s here. Nothing is bundled or
  fetched; this ships zero audio content of its own.
- **`gui/src/main/music-parser.ts`** (new): pure logic (`filterMp3Files`, `pickRandom`) — no
  Electron/filesystem import, directly unit-testable (same split as `registry-parser.ts`).
- **`gui/src/main/music.ts`** (new): the filesystem wrapper (`listMusicFiles`, `pickRandomTrack`)
  reading `design/music/`. Read-only, best-effort — a missing/empty folder means "no track" not
  a thrown error.
- **2 new IPC channels**: `music:randomTrack` (path to one random local file) and `music:list`
  (every filename, for the "choose a track" list) — both read-only, no new attack surface beyond
  what `recentProjects:list` already established.
- **Toolbar button** ("♪ Play Veselin Marinov"), shown only when the active wallpaper's theme is
  `imperator` (`updateMusicPickerVisibility`, hooked into `setWallpaper`) — hidden on every other
  theme. Dropdown menu: **Open on YouTube** (a placeholder URL — `VESELIN_MARINOV_YOUTUBE_URL` in
  `main.ts`, one constant to swap once Stefan has the real link), **Play a random local track**,
  **Choose a track…** (lists every file in `design/music/`, replaces the menu temporarily), and
  **Stop** (shown only once something is playing).
- A hidden `<audio id="music-player">` element in the renderer plays local files via a `file://`
  URI. **CSP fix**: the existing policy had no `media-src` directive, so `default-src 'self'`
  would have silently blocked the `<audio>` element from loading a local file — added `media-src
  'self' file:`.
- **i18n**: new `music` string table (`button`/`youtube`/`random`/`pick`/`stop`) filled across all
  7 hand-written language tables. Caught and fixed a real structural bug while writing the
  `yoda.ts` entry — the block landed nested inside `wallpaper` instead of as a sibling key
  (indentation slip), confirmed by the resulting `tsc` syntax error and fixed before it could ship.

## Verified

`tsc --noEmit` clean. `electron-vite build` all 3 targets green. `node --test`: 49/49 (45 prior +
4 new `music-parser.test.ts` cases: mp3-only case-insensitive filtering + sort, empty-list
handling, `pickRandom` always returns a list member across repeated calls, throws on empty input
rather than returning `undefined`). Not verified: on-screen render/click-through, or actual audio
playback (no MP3s exist in `design/music/` yet — Stefan populates that folder himself) — the
standing `npm run dev` gate, same as every other GUI feature in this batch.

## Known follow-ups (not blockers)

- `VESELIN_MARINOV_YOUTUBE_URL` in `main.ts` is a placeholder search-results link — swap for the
  real URL whenever Stefan has it (one-line change).
- `design/music/` is empty — the random/choose-track paths will correctly toast "no local tracks
  found" until Stefan drops MP3 files in.

## Affected files

- `design/music/` (new, empty folder)
- `gui/src/main/music-parser.ts`, `gui/src/main/music.ts` (new)
- `gui/src/main/paths.ts` (`musicDir()`), `gui/src/main/ipc.ts` (2 new handlers)
- `gui/src/shared/ipc-types.ts` (`musicRandomTrack`/`musicList`)
- `gui/src/preload/index.ts` (bridge methods)
- `gui/src/renderer/index.html` (button/menu markup, `<audio>` element, CSP `media-src` fix)
- `gui/src/renderer/styles.css` (`.music-picker`/`.music-menu`/`.music-item`)
- `gui/src/renderer/main.ts` (picker build/visibility/playback logic)
- `gui/src/renderer/i18n/{en,bg,de,ru,tr,romani,yoda}.ts` (`music` table)
- `gui/test/music-parser.test.ts` (new)
- `tasks.md`
