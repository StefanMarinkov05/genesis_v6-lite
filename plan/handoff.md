# DPS-NN — new-session handoff

Paste the fenced block below into a fresh Claude Code chat (this repo as the working directory).
Written per the `context-handoff` skill's 6-section discipline (Goal / State / Decisions /
Assumptions / Gotchas / Next actions) — not a narrative retelling of the conversation.

**Recommended for the new session:** model **Sonnet 5**, effort **medium**, thinking **standard**
(not extended) — this project is almost entirely standard multi-file feature work following
established patterns. Bump to **Opus** only if the next task is architectural/cross-cutting
(touches the generator's core emission logic — e.g. how the pipeline emits admin surfaces for
arbitrary models) or a root-cause debugging session with real ambiguity (see
`changelogs/README.md`'s model-routing rubric for the exact criteria). Drop to **Haiku** only for
a mechanical, single-file, pattern-copy task.

---

## Paste-in prompt

```
Read CLAUDE.md first (router, not a container — follow its table to whatever doc the actual task
needs). Read plan/state.md + plan/decisions.md per CLAUDE.md's "read first" rule, but trust
plan/tasks.md over state.md for "what's open" — state.md's own header says how current it is.

## Goal
No task is currently in progress — the last unit of work (multi-image seed data + a GUI session)
is complete, tested, committed, and pushed. Your first job is to ask what to work on next, not to
resume something. If the user says "continue" without specifics, check plan/tasks.md's "Now working"
section first — it should say "Idle" if nothing was left mid-flight.

## State (done + VERIFIED, not just claimed)
- `master` @ c59da14. Working tree clean at handoff time.
- Genesis-side: products with `images.mode:"multiple"` now seed 2-4 real images each (was
  hardcoded to 1). VERIFIED by querying the regenerated app's DB directly (48 ProductImage rows /
  24 products / exactly 2 each), confirming real downloaded JPEGs on disk, `dotnet build` (0
  errors) + full test suite (121/121) green. Changelog 129.
- GUI-side: full "Play Veselin Marinov" music player rework (category popup, Range-aware
  app-media:// protocol, pause/resume, seek bar), titlebar/branding pass, boot splash, i18n audit,
  a joke "performance notice" popup on Start click. VERIFIED via extensive real-app screenshot
  testing across many rounds (not just tsc/build — actual on-screen behavior, including a genuine
  multi-round root-cause debugging session for a text-rendering bug). Changelogs 128-129.
- `plan/archive/v6-task-breakdown.md` (Packages A-K): all items done as of changelog 101 — do not
  re-propose any of those as "missing," they're shipped.
- `plan/tasks.md`'s only unchecked line: an optional "Claude CI step" idea, explicitly never committed
  to. Not a blocker for anything.

## Decisions made (with rationale — check plan/decisions.md for the full log)
- Seed images: schema field renamed `imageKeyword` (string) → `imageKeywords` (array), sized 1..1
  for `mode:"single"` and 2..4 for `mode:"multiple"` via JSON Schema min/maxItems — keeps both
  modes correct from one field instead of branching per-mode in two places.
- Seed image filenames: `{model}-{row}.jpg` → `{model}-{row}-{img}.jpg`. `SeedStage.cs`'s download
  loop and `seed_data.sbn`'s runtime insert loop each independently recompute this same pattern
  from the same seed-data.json — no state passed between them, so if you touch one you MUST check
  the other stays in sync.
- Local media (`design/music/`, ~7.9GB total) is NOT fully version-controlled: only a curated
  ~957MB subset (51 smallest songs + 1 smallest concert video) is tracked via Git LFS, chosen to
  fit GitHub's free 1GB/month LFS quota. The rest is gitignored, same treatment as `out/`.
- Tried and REVERTED: `app.disableHardwareAcceleration()` + `--disable-direct-composition` +
  `--disable-gpu-compositing` as a fix for the text-rendering bug — didn't work, and would have
  been actively harmful long-term (this app's main feature is playing video). Removed once the
  real cause was found. Don't re-add these without new evidence.
- Prank popup: text-only content, no fake "slowdown" logic ever implemented — it's purely
  cosmetic/joke, by explicit request. Don't build real throttling behind it.

## Assumptions & open questions (unconfirmed — check before acting on them)
- `plan/tasks.md` has NOT been updated to reflect changelogs 112-129 (everything past the "Play
  Veselin Marinov button" entry) — it's stale but was left that way pending confirmation from the
  user on whether they want it refreshed. Ask, don't assume either way.
- Whether `plan/handoff.md` (this file, formerly root `context.md`) should be committed to git, vs. staying a local-only scratch
  file — was raised, never answered.
- `gui/resources/icon.ico` vs `gui/src/renderer/public/logo/icon.ico` — earlier in the prior
  session the user updated the renderer-side icon directly; whether the OS-window/taskbar icon
  copy (`gui/resources/icon.ico`) was ever synced to match is unconfirmed. Check both files match
  before assuming the taskbar icon is current.
- Git LFS is at ~957MB of the 1GB free quota — essentially no headroom. Any request to add more
  tracked media needs an explicit quota conversation first (paid data pack vs. dropping something
  already tracked), don't just add-and-push.

## Gotchas (the expensive-to-rediscover stuff)
- **Flexbox + `overflow:hidden` auto-min-size-0 trap**: a flex item with `overflow` != `visible`
  gets an automatic minimum size of 0 (not min-content) per the Flexbox spec, and flex children
  default to `flex-shrink:1`. In a `display:flex; flex-direction:column` list with many items and
  `overflow:hidden` (needed for `text-overflow:ellipsis`), the flex algorithm can shrink every
  row's HEIGHT below its own text height, silently clipping the bottom of each line — looked like
  character corruption in Cyrillic text, cost 4 wrong theories (font, GPU cache, DirectComposition,
  DOM-insert-race) before this was found. Fix is one line: `flex: none` on the row. If any other
  flex-column list in this GUI ever shows partial/clipped text, check this FIRST.
- **`file://` media is blocked cross-origin in Electron dev mode**: `npm run dev` serves the
  renderer from `http://localhost:5173`, not `file://` — Chromium refuses `file://` media loads
  from a non-file:// origin ("no supported source was found"), even though the exact same
  file:// URL works fine in a packaged build (renderer served from file://, same-origin there).
  Fixed via a registered-privileged custom `app-media://` protocol (`src/main/media-protocol.ts`),
  Electron's documented pattern for this. Don't "fix" this again with webSecurity:false.
- **Seeking needs a hand-rolled Range handler**: `net.fetch()` on a `file://` target inside
  `protocol.handle()` did NOT reliably serve `206 Partial Content` — seeking reset to 0:00. Fixed
  with `fs.createReadStream(path, {start, end})` and manual `Content-Range` headers.
- **Smart-resume can silently reuse a stale database**: deleting `out/` alone does NOT force a
  fresh migration+seed if `logs/<ProjectName>/db.json` still has a `dbName` that happens to match
  what the new run derives — migrations get skipped ("вече приложени — пропускане") and
  `EnsureSeededAsync`'s `AnyAsync()` early-return then skips seeding too, against the OLD data.
  Caught by directly querying the DB after a "successful" regenerate and finding stale rows. If
  you ever regenerate FaceNote (or any project) after a schema/seed-logic change, verify the
  actual DB content, don't trust the pipeline's own "success" log alone — and clear
  `logs/<Project>/db.json` (or drop the DB) first if you want a guaranteed-fresh run.
- **Electron stale-window false negatives**: after any renderer/main change, a full
  `Get-Process electron | Stop-Process -Force`, confirm zero remain, THEN relaunch — otherwise
  "nothing changed" reports are often just a window that never actually reloaded.
- **`git add` on files inside a gitignored directory**: negation patterns (`!path`) inside an
  ignored parent directory don't reliably re-include individual files; use `git add -f <exact
  path>` per file instead when you need to track a curated subset of an otherwise-ignored folder.

## Next actions
1. Ask the user what they want to work on — there is no queued task.
2. If they want housekeeping first: offer to update `plan/tasks.md` to reflect changelogs 112-129 (see
   Assumptions above — don't do it unprompted, ask first).
3. If they mention "the music popup" or "the track list" again: read the Gotchas section above
   before touching CSS there — the flexbox trap is the single most expensive lesson from the
   prior session and is easy to reintroduce by "simplifying" the layout.
```
