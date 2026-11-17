import { contextBridge, ipcRenderer } from 'electron';
import {
  CH,
  type BuildError,
  type BuildReport,
  type GuiApi,
  type ProgressEvent,
  type SettingsPatch,
} from '@shared/ipc-types';

// The preload is the ONLY bridge between the sandboxed renderer and the privileged main
// process (security model). It exposes exactly the typed GuiApi surface and nothing else:
// no raw ipcRenderer, no fs, no child_process, no process.env. Each push-event subscriber
// returns an unsubscribe function so screens can clean up their listeners.

function subscribe<T>(channel: string, cb: (payload: T) => void): () => void {
  const listener = (_e: unknown, payload: T): void => cb(payload);
  ipcRenderer.on(channel, listener);
  return () => ipcRenderer.removeListener(channel, listener);
}

const api: GuiApi = {
  specList: () => ipcRenderer.invoke(CH.specList),
  specUpload: (bytes, name) => ipcRenderer.invoke(CH.specUpload, bytes, name),
  seedList: () => ipcRenderer.invoke(CH.seedList),
  seedUpload: (bytes, name) => ipcRenderer.invoke(CH.seedUpload, bytes, name),
  seedGenerateWithClaude: (spec) => ipcRenderer.invoke(CH.seedGenerateWithClaude, spec),

  checkDuplicate: (spec, seed) => ipcRenderer.invoke(CH.checkDuplicate, spec, seed),
  buildStart: (spec, seed, opts) => ipcRenderer.invoke(CH.buildStart, spec, seed, opts),
  buildCreate: (description) => ipcRenderer.invoke(CH.buildCreate, description),
  buildPause: (runId) => ipcRenderer.invoke(CH.buildPause, runId),
  buildResume: (runId) => ipcRenderer.invoke(CH.buildResume, runId),
  buildStop: (runId) => ipcRenderer.invoke(CH.buildStop, runId),

  settingsGet: () => ipcRenderer.invoke(CH.settingsGet),
  settingsSet: (patch: SettingsPatch) => ipcRenderer.invoke(CH.settingsSet, patch),
  footerGet: () => ipcRenderer.invoke(CH.footerGet),
  reportGet: (runId) => ipcRenderer.invoke(CH.reportGet, runId),
  recentProjectsList: () => ipcRenderer.invoke(CH.recentProjectsList),
  musicRandomTrack: (category) => ipcRenderer.invoke(CH.musicRandomTrack, category),
  musicList: (category) => ipcRenderer.invoke(CH.musicList, category),

  windowMinimize: () => ipcRenderer.send(CH.windowMinimize),
  windowMaximize: () => ipcRenderer.send(CH.windowMaximize),
  windowClose: () => ipcRenderer.send(CH.windowClose),

  openExternal: (url) => ipcRenderer.invoke(CH.openExternal, url),
  openPath: (path) => ipcRenderer.invoke(CH.openPath, path),
  runApp: (projectDir) => ipcRenderer.invoke(CH.runApp, projectDir),
  killApp: (projectDir) => ipcRenderer.invoke(CH.killApp, projectDir),

  onProgress: (cb) => subscribe<ProgressEvent>(CH.onProgress, cb),
  onLog: (cb) => subscribe<string>(CH.onLog, cb),
  onAwaitingLlm: (cb) => subscribe<{ stage: string; requestPath: string }>(CH.onAwaitingLlm, cb),
  onDone: (cb) => subscribe<BuildReport>(CH.onDone, cb),
  onError: (cb) => subscribe<BuildError>(CH.onError, cb),
};

contextBridge.exposeInMainWorld('gui', api);
