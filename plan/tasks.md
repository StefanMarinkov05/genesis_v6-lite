# Tasks — DPS-NN

Live task queue. Full history of completed work lives in `changelogs/*` + `plan/state.md` — this
file only tracks what's still open.

---

## Now working

**Idle.** No task currently in progress. Last completed: "Play Veselin Marinov" button (changelog
111). See "Done" below for summary; full detail in changelog 111.

---

## Open — Category 3 backlog

**Infra (CI/CD)** · effort: low
- [x] GitHub Actions build+test gate on push/PR to `main` — DONE (changelog 099). `.NET`-only
      (120 tests); no GUI `node --test` job yet, no Claude step. Wrapper-script idea (reduce
      repeated regen/test invocations) explicitly declined — existing `.bat` scripts already cover it.
- [x] Add a GUI `node --test` job to `ci.yml` — **DONE** (changelog 103). New `gui-test` job:
      `typecheck` → `build` (electron-vite, all 3 targets) → `test:runner` (42 tests). Verified
      by running all 3 commands locally exactly as CI invokes them.
- [ ] Consider a low-risk Claude CI step (PR/commit summary) — not started, still on the table.

**Manual gate — CLEARED (2026-07-16).** Stefan confirmed: `npm run dev` renders correctly, themes
render properly, and the GUI is responsive across window sizes. A real end-to-end generation was
also run (project generated successfully). Nothing surfaced needing a fix. Phase 2 below is now
unblocked.

**Seeding workflow (changed — now manual, not a task, just context)**
Stefan generates seed data by pasting/attaching the spec into a Claude chat using the
`dps-nn-seed-authoring` skill, then uploads the result via the GUI's "Upload seed" control or
drops it under `seeds/` for the CLI picker. The in-app "Claude" seed button is deprioritized, not
cancelled.

**Phase 2 — DONE (changelog 102).**
- [x] Pause/Resume wired to the UI. New `'paused'` RunState distinct from `'stopped'` (Resume is
      legal only from paused, never after a plain Stop). i18n filled across all 7 hand-written
      tables. Found + fixed a pre-existing doc/code mismatch along the way: the arch doc claimed
      Pause waits for the current stage's checkpoint before killing; the real implementation
      always killed immediately (still correct, just undocumented accurately until now).
      tsc/build/42 tests green. NOT yet manually click-tested against a live pipeline run.

**Phase 3**
- [x] The other 4 themes (IMPERATOR/VEIL/FORGE/ENCHANT) as descriptor fills — **DONE** (changelog
      101). Ported from the vetted `design/preview.html` token reference; added a real theme
      picker (previously a static "NOVA" chip). Real background images/frame-ornament art per
      theme still deferred (design-specs.md §8 decision 4) — same boundary NOVA already had.
      Eyeballed and confirmed rendering correctly + responsive (2026-07-16).
- [x] The 5 additional languages (BG/DE/RU/Romani/SGA) + flag picker — **DONE** (changelog 093,
      Opus). GUI-chrome only; SGA is a font-swap seam; flags are inline SVG. Romani is best-effort,
      flagged for a native-speaker review. Eyeballed and confirmed (2026-07-16).
- [x] "Play Veselin Marinov" button — **DONE** (changelog 111). Toolbar button shown only on
      IMPERATOR; opens a placeholder YouTube link (swap the real URL in `main.ts` when ready) or
      plays a local MP3 from `design/music/` (random-shuffle or pick-specific). tsc/build/49 tests
      green. `design/music/` is empty — drop MP3 files there to actually hear anything. NOT yet
      eyeballed on screen.
- [x] Logo vibrancy + real titlebar icon — **DONE** (changelog 110). Titlebar icon was a CSS
      gradient placeholder, never the real logo — swapped for `design/logo.png`. Watermark
      opacity 0.4→0.92 + accent drop-shadow glow so it pops instead of looking faded/grayscale.
      tsc/build/45 tests green. NOT yet eyeballed on screen.
- [x] Reserved hero-space band (logo/face clearance) — **DONE** (changelog 108). Replaced the
      IMPERATOR-only 48px margin hack with a real, same-height-on-every-theme reserved band
      between the toolbar and input panels, so buttons/panels never sit over the logo or
      Veselin Marinov's face on ANY theme. tsc/build/45 tests green. NOT yet eyeballed on screen.
- [x] Logo watermark position + picker text-wrap fix — **DONE** (changelog 107). Watermark
      re-anchored to the titlebar-footer span instead of the full window (was overlapping the
      footer text). Wallpaper picker menu widened + smaller font so every label fits one line.
      tsc/build/45 tests green. NOT yet eyeballed on screen.
- [x] Logo watermark + IMPERATOR face fix — **DONE** (changelog 106). `design/logo.png` centered
      over every wallpaper except IMPERATOR (whose VM art self-brands); run controls pushed down
      48px on IMPERATOR only so Veselin Marinov's face stays clear of the panels. tsc/build/45
      tests green. NOT yet eyeballed on screen.
- [x] Wallpapers + loading animation — **DONE** (changelogs 104-105). Real background art (Stefan
      supplied `design/backgrounds/`, descriptively renamed) mapped to a flat 10-option wallpaper
      picker (each option = its own theme colors/fonts + background, no nested skins), wired via
      `--canvas-image`. Picker labels translated across all 7 hand-written languages, no
      descriptions (per Stefan's request). New full-bleed loading screen (spinner + progress)
      shown while a build runs. Per-project wallpapers explicitly OUT of scope (confirmed with
      Stefan) — loading screen uses the active picker selection, not a project-spec field.
      Lightened the scrim/gradient darkening app-wide after Stefan reported it too dark. Known
      cost: ~25MB unoptimized image assets, flagged not fixed. tsc/build/45 tests green. NOT yet
      eyeballed on screen.
- [x] Recent-projects sheet off `logs/registry.json` — **DONE** (changelog 100). New toolbar
      button + sheet, read-only `recentProjects:list` IPC channel, i18n filled in all 7
      hand-written tables (joke tier auto-generated). Eyeballed and confirmed (2026-07-16).

---

## Done (see changelogs/ + plan/state.md for full detail)

- Outgoing Identity confirmation emails (Email.cshtml.cs English subject/body) — closed as a
  NON-ISSUE, no code change. `RequireConfirmedAccount = false` (templates/project/program.sbn:35)
  already means generated apps never gate sign-in on email confirmation, and `IEmailSender` is
  never registered in program.sbn — ASP.NET Identity's built-in default is a no-op, so
  `_emailSender.SendEmailAsync` never actually sends mail. The English text sits in dead-letter
  code paths with no user-visible or functional effect. Stefan confirmed intent was "disable email
  confirmation entirely," which was already true; nothing to translate or wire up.
- Category 3 batch: `images.mode:"single"` UI gap + Group A2 (Identity/Manage English leaks) —
  changelog 097. Also fixed a real pre-existing bug found along the way: a too-narrow lookahead
  window in `InjectBgValidationMessages` produced a blank display name in `Register.cshtml.cs`
  (shipping bug, not introduced this session) and would have in `ChangePassword.cshtml.cs` too.
- Category 1 short-task batch (5 items) — changelog 089
- Group A4 — image pipeline implementation — changelog 087
- Group A5 — Спецификации consolidation implementation — changelog 088
- product_details.sbn zero-star row fix — changelog 091
- Card grid bulk average rating — changelog 092
- 2026-07-12 evening batch (GUI credential scoping, Kill-app, Cyrillic role fix, etc.) — changelog 086
