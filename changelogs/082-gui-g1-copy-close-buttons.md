# 082 — GUI Group G1: split copy buttons, close (✕) on every popup

**Type:** Feature / change
**Model:** Sonnet, low effort, thinking off — exactly the Group G1 scope from the prior
grouping/model-assignment pass (mechanical, well-specified, no dependencies).

## Summary

Two small GUI polish items from Stefan's task batch:

1. **Split the combined email/password copy field into two.** The completion readout previously
   rendered one `copyField("{email} / {password}")` per credential row — copying it gave you both
   values glued together with a slash, needing manual trimming before either was usable. Now each
   credential row has two `<dt>/<dd>` pairs (`{Role} — Email`, `{Role} — Password`), each with its
   own copy button.
2. **Close (✕) button on every popup.** `dup-dialog`, `report-screen`, and `error-screen` had no
   dedicated close control — only their footer action buttons (`Open existing`/`Cancel`,
   `Open in browser`/`New build`, `Retry`/`New build`) could dismiss them. `settings-sheet` already
   had one; added the matching `✕` to the other three, wired to each screen's existing close logic
   (`closeDuplicateDialog` / `closeResultScreens` + `setRunState('idle')`).

**Also fixed the flagged "New build" ambiguity while touching this area**: that button only ever
closed the readout and reset to idle — it never started configuring anything new, so the label read
as "this discards my current build and begins another" when it actually just meant "done, dismiss
this." Renamed to `Close` (new `t.report.close` i18n key, replacing `newBuild`) on both the report
and error screens, now that the dedicated `✕` covers the "clearly just close" gesture and the text
button can say plainly what it does.

## Verified

`tsc --noEmit` clean, `electron-vite build` all 3 targets, `node --test` 3/3. Launched the app for
real (`npm run dev`) and confirmed via `Get-Process` a genuine window opened with no console/runtime
errors, rather than just trusting a clean build. Full interactive click-through of each popup is
left to Stefan's own manual pass, per usual for GUI/UI work — this only confirms it renders and
doesn't crash, not that every click path looks right.

## Affected files

- `gui/src/renderer/index.html` (✕ buttons on the three popups, "Close" label)
- `gui/src/renderer/main.ts` (split copy fields, ✕ handlers, `t.report.close` wiring)
- `gui/src/renderer/i18n/en.ts` (`email`, `password`, `close` keys; `newBuild` removed)
