# 086 — GUI polish batch (5 items) + website localization fixes (2 items)

**Type:** Bug fix / feature batch
**Model:** Sonnet, high effort, thinking on — a 7-item batch Stefan reported directly, split into
GUI (Electron shell) and website (generated-app template/spec) halves.

## GUI changes

1. **Start command display truncated.** The completion popup's "Start" row rendered the full
   `dotnet run --project "<absolute path>"` string, overflowing the box. The copy button still
   copies the real command; the visible text is now the literal `dotnet run`.
   (`gui/src/renderer/main.ts` — `copyField` gained an optional `displayText` param.)
2. **Only 1 admin + 1 non-admin credential shown.** Satisfied by the CLI-side fix (item 5 below) —
   the banner itself now only contains 2 credential rows, so no GUI-side filtering was needed;
   `report.ts`'s parser already renders exactly what it's given.
3. **"Last result" reopen control.** A new toolbar chip (disabled until a build completes, and
   re-disabled the moment a new build starts) reopens the just-closed report/error screen without
   re-running anything — restores access to the copy buttons after an accidental close.
   (`lastError` state added alongside the existing `lastReport`; `reopenLastResult()`.)
4. **"Kill app" button.** Clarified with Stefan first: "Stop" (top run-row) already cancels the
   CLI *pipeline* — this is a distinct action for the *generated app* process started by the
   "Run app" button (084), which was previously un-stoppable from the GUI (spawned detached +
   `unref()`'d with no tracked PID). New `killApp` IPC channel; `genesis-runner.ts` now tracks the
   PID `runApp` launches per project dir in a small side-map and `taskkill /T /F`s it (mirrors the
   existing pipeline-kill pattern). Lives next to "Run app" in the report popup only, disabled
   until "Run app" has been clicked.
5. **Credentials scoped, roles fixed.** Two real bugs, not just cosmetic: (a) the banner printed
   all N seed users (5-6 for FaceNote) with full passwords always on screen; (b)
   `VariationResolver`'s `userRoleName` axis had `"Potrebitel"` — an ASCII typo/transliteration,
   neither Cyrillic nor real English, literally what Stefan saw on screen. Fixed both in
   `Program.cs`/`VariationResolver.cs`: the banner now shows exactly one admin + one non-admin
   (first match per role, found via `Identity.Roles[].IsAdmin`), with the full N-user credential
   list written to `logs/<Project>/seed-credentials.txt` instead of the console; `userRoleName`'s
   options are now `["Потребител", "User", "Client"]` — Cyrillic or clean English, never
   transliterated.

## Website (generated-app) changes

6. **No Details page for single-property models.** Brand/Category (`Name` is their only non-key
   property) had a full scaffolded `Details` action + view that was already unlinked from the
   admin index (the existing `non_key_count > 1` gate in `admin_table.sbn` hides the button) but
   still directly reachable by URL — a dead, pointless page. Used the existing, already-proven
   `removedActions` deviation mechanism (brace-safe regex strip + view delete, same as changelog
   057's Admin Orders Create removal) rather than new engine code: both fixtures'
   `BrandsController`/`CategoriesController` entries gained `"removedActions": ["Details"]`.
7. **Admin/Products FK column headers were raw English.** `admin_table.sbn` rendered
   `<th>{{ rel.navigation }}</th>` — the literal C# navigation property name (`Brand`, `Category`),
   with no Bulgarian lookup, unlike every other column on the same table. Fixed to look up the
   relation's target model in `spec.models` and use its own `displayName` (`Марки`, `Категории`)
   when present, falling back to the raw navigation name otherwise. New regression test
   `Admin_table_fk_column_headers_use_target_model_display_name` renders the real Product model
   and asserts both Bulgarian headers appear and neither English one does.

## Verification

- `dotnet test`: 112/112 (was 111 — the new FK-header test).
- `gui`: `tsc --noEmit` clean, `electron-vite build` all 3 targets, `node --test` 3/3. Real
  `npm run dev` smoke launch confirmed a live Electron window with no console errors after the
  kill-app state refinement.
- Full clean `run --all` regenerate against the FaceNote fixture (cleared `out/FaceNote` +
  `.lock.json` first, so spec-expand re-derives `userRoleName` from source rather than reusing a
  stale lock) — verified directly against the real output:
  - Console banner: `Admin admin@facenote.bg / …` + `Потребител user@facenote.bg / …` only —
    real Cyrillic, exactly 2 rows, with `(4 more seed user(s) — full list: …)`.
  - `logs/FaceNote/seed-credentials.txt`: all 6 seed users present with correct roles.
  - `Areas/Admin/Controllers/BrandsController.cs`: no `Details` action.
  - `Areas/Admin/Views/Brands/` and `.../Categories/`: no `Details.cshtml` (Create/Edit/Delete/
    Index only).
  - `Areas/Admin/Views/Products/Index.cshtml`: `<th>Марки</th>` and `<th>Категории</th>`.
- `dotnet test` re-run after the real-run confirmation: still 112/112.

## Affected files

- `src/Genesis.Cli/Program.cs`, `src/Genesis.Core/Variation/VariationResolver.cs`
- `templates/views/admin_table.sbn`, `tests/Genesis.Tests/CoreTests.cs`
- `fixtures/project-spec.facenote.json`, `fixtures/project-spec.facenote-en.json`
- `gui/src/renderer/main.ts`, `index.html`, `i18n/en.ts`
- `gui/src/shared/ipc-types.ts`, `gui/src/main/ipc.ts`, `gui/src/main/genesis-runner.ts`,
  `gui/src/preload/index.ts`
- `docs/explanation/gui-architecture.md` (killApp channel documented in the same commit)

## Housekeeping

Found and fixed a filing bug from the prior session: changelog 084 had been written to
`gui/changelogs/084-gui-run-app-button.md` instead of the project-root `changelogs/` — breaking
the single numbering sequence (083 → 085 with 084 invisible). Moved it to
`changelogs/084-gui-run-app-button.md`; content untouched (paths inside it were already
repo-root-relative, no edits needed).
