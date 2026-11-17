import { type BrowserWindow, ipcMain, shell } from 'electron';
import { CH, type FileHandle, type MusicCategory, type SettingsPatch } from '@shared/ipc-types';
import { listSeeds, listSpecs, uploadSeed, uploadSpec } from './files';
import { checkDuplicate } from './dedup';
import { getSettings, setSettings } from './settings';
import { getFooter } from './footer';
import { GenesisRunner, killGeneratedApp, runGeneratedApp } from './genesis-runner';
import { listRecentProjects } from './recent-projects';
import { listMusicFiles, pickRandomTrack } from './music';

// Registers the entire IPC surface named in docs/explanation/gui-architecture.md. The
// preload exposes exactly these channels and nothing else — this is the whole attack
// surface, so every handler is explicit and typed. Errors thrown here surface to the
// renderer as rejected promises (never a silent failure).

export function registerIpc(win: BrowserWindow): void {
  const runner = new GenesisRunner({
    progress: (e) => win.webContents.send(CH.onProgress, e),
    log: (line) => win.webContents.send(CH.onLog, line),
    awaitingLlm: (e) => win.webContents.send(CH.onAwaitingLlm, e),
    done: (e) => win.webContents.send(CH.onDone, e),
    error: (e) => win.webContents.send(CH.onError, e),
  });

  // Prerequisite report into the debug terminal once the renderer is listening (onLog is
  // subscribed during renderer startup, before did-finish-load).
  win.webContents.once('did-finish-load', () => runner.doctor());

  // ---- spec / seed input ----
  ipcMain.handle(CH.specList, () => listSpecs());
  ipcMain.handle(CH.specUpload, (_e, bytes: Uint8Array, name: string) => uploadSpec(bytes, name));
  ipcMain.handle(CH.seedList, () => listSeeds());
  ipcMain.handle(CH.seedUpload, (_e, bytes: Uint8Array, name: string) => uploadSeed(bytes, name));
  ipcMain.handle(CH.seedGenerateWithClaude, () => {
    // Phase 2 (Opus Task 3): a main-process Anthropic call writing prompts/in/seed-response.json.
    // Declared so the surface is stable; the MVP UI keeps this control disabled.
    throw new Error('Claude seed generation is a phase-2 feature and is not enabled in the MVP.');
  });

  // ---- dedup + run lifecycle ----
  ipcMain.handle(CH.checkDuplicate, (_e, spec: FileHandle, seed: FileHandle | null) =>
    checkDuplicate(spec, seed),
  );
  ipcMain.handle(CH.buildStart, (_e, spec: FileHandle, seed: FileHandle | null) =>
    runner.start(spec, seed),
  );
  ipcMain.handle(CH.buildCreate, (_e, description: string) => runner.create(description));
  ipcMain.handle(CH.buildPause, (_e, runId: string) => runner.pause(runId));
  ipcMain.handle(CH.buildResume, (_e, runId: string) => runner.resume(runId));
  ipcMain.handle(CH.buildStop, (_e, runId: string) => runner.stop(runId));

  // ---- settings / footer / report ----
  ipcMain.handle(CH.settingsGet, () => getSettings());
  ipcMain.handle(CH.settingsSet, (_e, patch: SettingsPatch) => setSettings(patch));
  ipcMain.handle(CH.footerGet, () => getFooter());
  ipcMain.handle(CH.reportGet, (_e, runId: string) => runner.getReport(runId));
  ipcMain.handle(CH.recentProjectsList, () => listRecentProjects());
  ipcMain.handle(CH.musicRandomTrack, (_e, category: MusicCategory) => pickRandomTrack(category));
  ipcMain.handle(CH.musicList, (_e, category: MusicCategory) => listMusicFiles(category));

  // ---- window chrome (frameless window; controls are real DOM per R6) ----
  ipcMain.on(CH.windowMinimize, () => win.minimize());
  ipcMain.on(CH.windowMaximize, () => (win.isMaximized() ? win.unmaximize() : win.maximize()));
  ipcMain.on(CH.windowClose, () => win.close());

  // ---- shell: open the generated app's URL / output folder in the OS ----
  ipcMain.handle(CH.openExternal, (_e, url: string) => shell.openExternal(url));
  ipcMain.handle(CH.openPath, (_e, path: string) => shell.openPath(path));
  ipcMain.handle(CH.runApp, (_e, projectDir: string) => runGeneratedApp(projectDir));
  ipcMain.handle(CH.killApp, (_e, projectDir: string) => killGeneratedApp(projectDir));
}
