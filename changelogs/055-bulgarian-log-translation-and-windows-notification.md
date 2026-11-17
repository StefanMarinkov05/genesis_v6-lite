# Bulgarian pipeline-log translation + Windows notification hook

**Type:** Feature / change
**Model:** Sonnet — well-scoped generator-logic work following the exact pattern established for the Bulgarian worker names (issue #5) and the `meta.language` gate (changelog 054); no cross-cutting architecture decisions. The Windows-notification piece is a Claude Code harness/hook configuration, not project code, but recorded here since it was requested in the same turn. Confirmed with Stefan that Sonnet was fine for both before starting.

## Summary
Two independent requests from the same turn (recorded as one entry since both are small and from
the same session; changelog-discipline calls for separate entries only when they're substantial
enough to confuse together):

1. **Windows toast notification** whenever Claude Code needs a permission click or has a question
   waiting — a Claude Code `Notification` hook, not part of this repo's own code.
2. **dps-nn's own pipeline logs translated to Bulgarian** for a Bulgarian project — the same
   "Bulgarian everything" idea already applied to the parallel worker names (issue #5), now
   extended to the log narration itself (`[dps-nn] wrote Models/Product.cs` style lines).

## Part 1 — Windows notification hook (Claude Code config, not dps-nn code)
- New `C:\Users\AXN\.claude\hooks\windows-toast.ps1`: reads the hook's JSON stdin defensively
  (`.message` → `.title` → generic fallback), shows a `System.Windows.Forms.NotifyIcon` balloon
  tip (Windows 10/11 renders these as real toast notifications) titled "Claude Code".
  `NotifyIcon` was chosen over the WinRT `ToastNotificationManager` API because it doesn't need an
  AppUserModelID registered against a Start Menu shortcut to avoid an "element not found" error -
  more reliable for an ad-hoc script.
- Wired into `~/.claude/settings.json`'s new `Notification` hook (global, not project-scoped - this
  is a personal preference across all of Stefan's Claude Code projects, not specific to dps-nn).
- Verified live, twice: once calling the balloon-tip API directly, once piping a synthetic JSON
  payload through the actual script the way the hook invokes it - Stefan confirmed both notifications
  appeared before this was wired into settings.json.

## Part 2 — Bulgarian log translation
- New `templates/log-bg-map.json`: a flat map of STATIC English phrase fragments → Bulgarian,
  applied longest-fragment-first - the exact same technique already used for the generated app's
  own scaffolder-residue translation (`bg-term-map.json` / `identity-bg-map.json`), just aimed at
  dps-nn's OWN console/action-log output instead of the generated project's UI. Deliberately NOT a
  resource-key/format-string localization system: these ~51 log call sites are ordinary string
  interpolations (file paths, names, counts mixed with English prose), and translating the STATIC
  fragments around the dynamic parts needed ZERO changes to any call site.
- `StageContext.Info/Debug/Error` (Pipeline.cs) now run `TranslateLog(msg)` before the existing
  worker-name `Tag()` step, gated on the same `Spec.Meta.IsBulgarian` single source of truth from
  changelog 054. English projects are completely unaffected (map never applied). The map is loaded
  once per `GenesisRoot` and cached (a `Dictionary` keyed by path, guarded by a lock) rather than
  re-parsed on every one of the ~50+ log calls per run.
- Scope: this translates dps-nn's OWN narration, NOT the generated app's UI (already handled
  separately, see changelog 054's scope boundary) and NOT this repo's own source comments.

## Verification
- `dotnet build` clean; `dotnet test` **111/111** (106 baseline + 5 new `LogTranslationTests`:
  Bulgarian project translates, English project stays English, a longer fragment correctly wins
  over a shorter generic one it contains, the stage-timings summary line translates, `Error()` also
  translates).
- Ran the REAL tool (not just unit tests) against the actual FaceNote project: `emit-models` printed
  genuine Bulgarian narration (`[Кольо] записан файл: Models/Product.cs`, `моделите са записани,
  компилацията е успешна`) with file paths correctly left untranslated. A parallel run against a
  cloned English-language lock file confirmed the log stayed in English.
- Caught + immediately fixed a self-inflicted issue while testing: an ad-hoc EN-language check spec
  had no matching `.lock.json`, so the tool fell back to the RAW (unexpanded, 3-model) fixture and
  briefly overwrote `out/FaceNote`'s model files with an incomplete set, breaking its build. Restored
  by re-running `emit-models` against the real expanded fixture; confirmed `dotnet build` green again
  before moving on. Not a defect in the shipped feature - a test-methodology mistake on a spec file
  that never had a lock counterpart, caught and corrected immediately.

## Affected files
- `C:\Users\AXN\.claude\hooks\windows-toast.ps1` (new, outside this repo)
- `C:\Users\AXN\.claude\settings.json` (new `Notification` hook, outside this repo)
- `templates/log-bg-map.json` (new)
- `src/Genesis.Core/Pipeline/Pipeline.cs` (`TranslateLog` + wiring)
- `tests/Genesis.Tests/CoreTests.cs` (5 new `LogTranslationTests`)
