#!/usr/bin/env node
// Launches electron-vite with ELECTRON_RUN_AS_NODE fully REMOVED from the child env.
//
// Why this file exists: `cross-env VAR=` / most cross-platform env tools only set a var to an
// EMPTY STRING - they can't unset it. Electron's own native check for this var is a presence
// check (`HasVar`), not a truthy check, so a present-but-empty value still forces Electron to
// run as a plain Node process instead of opening a window (require('electron') then returns a
// path string, not the app API - the classic "Cannot read properties of undefined (reading
// 'whenReady')" crash). This var commonly leaks into a terminal's env when it's a child of
// another Electron app (e.g. an Electron-based editor's integrated terminal), so relying on the
// caller's shell not having it set is not safe - delete it here, unconditionally.
const { spawn } = require('node:child_process');
const path = require('node:path');

const env = { ...process.env };
delete env.ELECTRON_RUN_AS_NODE;

// Spawn electron-vite's bin script directly with `node`, via a plain path join - NOT
// require.resolve('electron-vite/bin/electron-vite.js') (blocked by the package's "exports"
// map under Node's strict subpath resolution) and NOT the node_modules/.bin/*.cmd shim (Windows
// requires shell:true to exec a .cmd, which reopens quoting/escaping risk for no benefit here).
const root = path.resolve(__dirname, '..');
const bin = path.join(root, 'node_modules', 'electron-vite', 'bin', 'electron-vite.js');
const args = process.argv.slice(2);

const child = spawn(process.execPath, [bin, ...args], { stdio: 'inherit', env, cwd: root });
child.on('exit', (code) => process.exit(code ?? 0));
