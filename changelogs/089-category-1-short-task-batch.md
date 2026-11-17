# 089 — Category 1 short-task batch (5 items, 2 real bugs found)

**Type:** Bug fix batch
**Model:** Sonnet, low effort, thinking off — a batch of small, mechanical, narrow-blast-radius
tasks queued from the 2026-07-12/13 evening session's own follow-up audit (tasks.md Category 1).
Item 4 (characteristics_index.sbn FK-header sweep) was dropped before starting — that file was
deleted when A5 retired Характеристики (changelog 088), making the item moot.

## Summary

Two of the five items were genuinely "confirm or shrug" as sized. The other three surfaced real,
previously-shipped bugs — small tasks are not always low-value.

1. **GUI: guard "Run app" against a deleted project folder.** `runGeneratedApp` now checks
   `existsSync(projectDir)` and throws a clear error instead of letting `spawn` fail with a raw
   ENOENT. The renderer's click handler was also fixed to only flip `appLaunchedForLastReport`
   (which enables "Kill app") on confirmed success, and toasts the error otherwise — previously it
   optimistically enabled Kill app before the spawn even resolved.
2. **CLI: `logs/<Project>/` was never gitignored.** Real gap, not a no-op: `logs/*.jsonl` /
   `logs/*.log` only match files directly in `logs/`, not inside per-project subfolders — so
   `logs/FaceNote/seed-credentials.txt` (**plaintext seed-user passwords**), `actions.log.jsonl`,
   and `db.json` were all untracked-but-stageable, one `git add -A` away from landing in history.
   Added `logs/*/` to `.gitignore` (verified via `git check-ignore`) — only `logs/README.md` stays
   tracked, matching intent.
3. **Website: single-property-model "no Details" rule was Brand/Category-only.** Auditing the
   fixture found `CatalogueItem` (`isLookup`, only "Position" as a real property) in the same
   boat — but it's auto-injected by `SpecStages.InjectAdminSupervisorSurface` at spec-expand
   time, not hand-authored, so the fixture-level fix from changelog 086 didn't cover it. Fixed at
   the generator level instead: the injector now computes each model's non-key/non-FK/non-computed
   property count itself and adds `Details` to `removedActions` whenever it's ≤1 — a permanent,
   generic rule (not a one-off patch) that covers every future single-property model, hand-authored
   or auto-injected, without another audit ever being needed. New test
   `Admin_supervisor_removes_details_for_single_property_models_and_keeps_order_rule` (also proves
   it coexists correctly with the pre-existing Order→no-Create rule in the same `removedActions`
   array).
4. **Website: a real dangling link, found by the audit this item asked for.** `admin_table.sbn`'s
   FK-relation column always linked to the target controller's `Details` action unconditionally.
   Since Brand/Category no longer have one (changelog 086), the Admin Products table's Brand/
   Category link cells pointed at a **404**. Fixed: the template now checks the target
   controller's `removedActions` and falls back to `Edit` (which always exists) when `Details`
   was removed. New test `Admin_table_fk_link_falls_back_to_edit_when_target_details_is_removed`.
5. **GUI: "Kill app" PID tracking didn't survive a GUI restart.** The in-memory `Map` backing
   `runGeneratedApp`/`killGeneratedApp` died with the main process. Replaced with a small
   file-backed store at `logs/launched-apps.json` (read/write around each call) — a generated
   app launched in one GUI session can now be killed after the GUI itself is closed and reopened,
   not just within the same session. Added to `.gitignore`. Verified with a Node smoke test
   simulating a process restart (fresh `readFileSync` in place of the in-memory Map). Noted
   limitation: this fixes the underlying PID *tracking*; full renderer-side UI-state restoration
   (reopening a *report screen* from a prior session) is a separate, larger feature, out of scope
   here — there is currently no mechanism to reopen any report after a restart at all, so the
   scenario this closes is "same GUI session, later report for the same project dir".

## Verification

- `dotnet build` + `dotnet test`: 116/116 (was 114 after A5 — 2 new tests this batch).
- `gui`: `tsc --noEmit` clean, `electron-vite build` all 3 targets, `node --test` 3/3.
- Full clean `run --all` regenerate — **verify PASSED** (build/db/boot/http/manifest/language).
  Confirmed directly in real generated output: `Products/Index.cshtml`'s Brand/Category links now
  read `asp-action="Edit"` (was the dead `"Details"`); `CatalogueItemsController.cs` has no
  `Details` action and `Areas/Admin/Views/CatalogueItems/` has no `Details.cshtml`; A5's
  Brand/Category→Specifications redirects from changelog 088 remain intact (3 each, unaffected).
- `git check-ignore -v logs/FaceNote/seed-credentials.txt` — confirmed ignored, `git status`
  clean on `logs/`.

## Affected files

- `gui/src/main/genesis-runner.ts` (existsSync guard, file-backed launched-apps store)
- `gui/src/renderer/main.ts` (Run-app click handler: success/failure branches)
- `src/Genesis.Core/Stages/SpecStages.cs` (`InjectAdminSupervisorSurface` generic Details rule)
- `templates/views/admin_table.sbn` (FK link Details→Edit fallback)
- `tests/Genesis.Tests/CoreTests.cs` (2 new tests)
- `.gitignore` (`logs/*/`, `logs/launched-apps.json`)
