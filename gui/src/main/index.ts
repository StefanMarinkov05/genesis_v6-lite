import { join } from 'node:path';
import { BrowserWindow, app } from 'electron';
import { registerIpc } from './ipc';
import { handleMediaProtocol, registerMediaProtocol } from './media-protocol';

// Electron main entry. The window is FRAMELESS (frame: false) — the carved frame and the
// min/max/close controls are real renderer DOM per design-specs.md R6 and the arch doc.
// The renderer runs fully sandboxed: contextIsolation on, nodeIntegration off, sandbox on;
// its only path to anything privileged is the typed preload surface (security model).

// Must be registered before app is ready (Electron requirement for privileged schemes).
registerMediaProtocol();

function createWindow(): void {
  const win = new BrowserWindow({
    width: 1080,
    height: 720,
    minWidth: 900,
    minHeight: 620,
    frame: false,
    backgroundColor: '#050608', // matches the NOVA canvas so there's no white flash
    // Window/taskbar icon (Stefan's own icon.ico, copied into gui/resources/) - replaces the
    // default Electron icon that showed up in the OS taskbar/alt-tab (this option does NOT
    // affect the in-app .logo-mark titlebar icon, which is a renderer-side <img>/background-image
    // pointed at logo.png separately).
    icon: join(__dirname, '../../resources/icon.ico'),
    show: false,
    webPreferences: {
      preload: join(__dirname, '../preload/index.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });

  registerIpc(win);

  win.once('ready-to-show', () => win.show());

  // electron-vite injects ELECTRON_RENDERER_URL in dev; load the built file in production.
  const devUrl = process.env.ELECTRON_RENDERER_URL;
  if (devUrl) {
    void win.loadURL(devUrl);
  } else {
    void win.loadFile(join(__dirname, '../renderer/index.html'));
  }
}

app.whenReady().then(() => {
  handleMediaProtocol();
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
