# 076 — GUI `npm run dev` failed to launch (two real bugs, not one)

**Type:** Error fix
**Model:** Sonnet — real-bug diagnosis under an existing architecture, not a design decision.

## Where

`gui/` — `node_modules/electron`'s binary install, and `electron-vite`'s dev-server process spawn.

## How it happened

Stefan ran `npm run dev` in `gui/` and got `Error: Electron uninstall` from
`electron-vite`'s `getElectronPath()`.

## Why (bug 1 — the reported one)

`node_modules/electron/dist/` only ever contained `LICENSES.chromium.html`; `path.txt` was never
written. `electron`'s own postinstall (`node_modules/electron/install.js`) downloads a zip via
`@electron/get` (confirmed cached correctly at 115MB in `%LOCALAPPDATA%\electron\Cache\...\
electron-v33.4.11-win32-x64.zip` — not a corrupt/truncated download) and extracts it with
`extract-zip`. Re-running `install.js` directly reproduced the failure silently: exit code 0, no
console output, `path.txt` still missing. Isolated it by extracting the same cached zip with
PowerShell's native `Expand-Archive` instead — that succeeded completely (73 files, `electron.exe`
present and correctly sized at 188MB) to both an ASCII path and to the actual (Cyrillic)
`D:\Projects\ДПС-НН\gui\node_modules\electron\dist`. So the zip was never the problem: `extract-zip`
(Node, via `yauzl`) specifically failed partway through extracting `electron.exe` — most likely an
antivirus real-time-scan lock racing the write of a freshly-downloaded `.exe`, silently truncating
the extraction without the promise chain's `.catch` ever firing (the process exited clean before the
rejection would have surfaced). Not fully root-caused (an intermittent AV/IO race, not deterministic
to reproduce on demand) — worked around by finishing the extraction via a tool that isn't
susceptible to the same race.

## Why (bug 2 — found only by actually running the fix, not assumed)

Fixing bug 1 alone did **not** produce a window. The app launched and immediately crashed:
`TypeError: Cannot read properties of undefined (reading 'whenReady')` at `electron.app.whenReady()`
in the compiled main process. This is the exact signature of Electron running in **Node-emulation
mode**: `ELECTRON_RUN_AS_NODE` was set to `1` in the shell (confirmed present in both the bash tool's
environment and a fresh PowerShell process — almost certainly inherited from an Electron-based
parent process, e.g. an editor's integrated terminal). When that variable is set, `require('electron')`
inside the main process returns a plain path string instead of the Electron API object, so `.app` is
`undefined`. `electron-vite`'s own `startElectron()` spawns the Electron binary with
`{ stdio: 'inherit' }` and no `env` override, so it inherits whatever the parent shell has — this bug
is **not specific to Stefan's machine's state right now**; it will recur in any terminal that happens
to be a descendant of an Electron app, which is common for IDE-integrated terminals.

Tried `cross-env ELECTRON_RUN_AS_NODE=` (the standard idiom) first — verified it does **not** work
here: Electron's native check is a variable-*presence* check (`HasVar`), not a truthy check, and
`cross-env`'s empty-value assignment leaves the key present with an empty string
(confirmed directly: `'ELECTRON_RUN_AS_NODE' in process.env` stayed `true`). The key must be fully
absent, not just falsy.

## What it affects

Every `npm run dev` / `npm run start` invocation from a terminal that inherits
`ELECTRON_RUN_AS_NODE` - silently, with no window and a crash that only appears in the background
process's own stdout (easy to miss if not watching the terminal).

## How to reproduce

Bug 1: was already environment-state-dependent (a prior failed extraction); not independently
reproducible on demand. Bug 2: run `npm run dev` from any shell with `ELECTRON_RUN_AS_NODE=1` set
(e.g. `$env:ELECTRON_RUN_AS_NODE=1; npm run dev` in PowerShell) — reproduces the `whenReady` crash
immediately, unrelated to bug 1.

## How to fix

- **Bug 1**: extracted the verified-good zip via PowerShell's `Expand-Archive` to a scratch
  directory, then copied the result directly into `node_modules/electron/dist` and hand-wrote
  `path.txt` — bypassing the Node extractor entirely for this one-time repair. Not a code change;
  a local `node_modules` state repair. If it recurs on a clean install, re-running `npm install`
  a second time (or `node node_modules/electron/install.js` again) may simply succeed since the
  underlying cause is an intermittent I/O race, not a deterministic bug.
- **Bug 2**: added `gui/scripts/dev.js`, a launcher that `delete`s `ELECTRON_RUN_AS_NODE` from a
  copied `process.env` before spawning `electron-vite`'s bin script directly via
  `process.execPath` (not `require.resolve()` into the package — blocked by its `exports` map
  under strict subpath resolution; not the `node_modules/.bin/*.cmd` shim — needs `shell: true`
  on Windows, which reopens quoting risk for no benefit). `package.json`'s `dev`/`start` scripts
  now call this wrapper instead of `electron-vite` directly, so the fix holds regardless of what
  terminal a future `npm run dev` is invoked from.
- Verified for real: launched `npm run dev` in the background, confirmed via
  `Get-Process -Name electron` that a genuine window process was running with
  `MainWindowTitle: DPS-NN — Genesis` (not just "no crash printed") before tearing it down.

## Affected files

- `gui/scripts/dev.js` (new)
- `gui/package.json` (`dev`/`start` scripts point at the wrapper)
- `gui/node_modules/electron/dist/*`, `gui/node_modules/electron/path.txt` (local install repair,
  not tracked by git — `node_modules/` is gitignored)
