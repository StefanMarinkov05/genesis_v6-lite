import { cpSync, existsSync, mkdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { app } from 'electron';

// Two roots (ADR-014). The CLI reads its templates/schema relative to its own GenesisRoot and
// writes logs/, prompts/, out/ and the image cache relative to its cwd, so the GUI must keep the
// two apart:
//   installRoot()   - READ-ONLY: templates/, spec.schema.json, feature-templates/, design/ (footer,
//                     music), the sample spec + seed. Packaged: <install>/resources/genesis.
//   workspaceRoot() - WRITABLE, the CLI child's cwd: logs/, prompts/, out/, fixtures/, seeds/,
//                     uploads. Packaged: Documents\DPS-NN.
// In a repo checkout (npm run dev) both are the repo root, exactly as before the split.
// Never guess a path; verify the marker files exist (same discipline as the CLI's GenesisRoot()).

let cachedInstall: string | null = null;
let cachedWorkspace: string | null = null;

function looksLikeGenesisRoot(dir: string): boolean {
  return existsSync(join(dir, 'templates')) && existsSync(join(dir, 'spec.schema.json'));
}

/** Where the read-only Genesis files live, or throw with a clear message (fail loud, not blind). */
export function installRoot(): string {
  if (cachedInstall) return cachedInstall;

  if (app.isPackaged) {
    const dir = join(process.resourcesPath, 'genesis'); // electron-builder extraResources
    if (looksLikeGenesisRoot(dir)) return (cachedInstall = dir);
    throw new Error(`The installation is incomplete: ${dir} has no templates/ + spec.schema.json. Reinstall DPS-NN.`);
  }

  // Dev: app.getAppPath() → gui/. Walk up from it (and the exe dir, cwd) to the repo root.
  const starts = [app.getAppPath(), dirname(app.getPath('exe')), process.cwd()];
  for (const start of starts) {
    let dir = resolve(start);
    for (;;) {
      if (looksLikeGenesisRoot(dir)) return (cachedInstall = dir);
      const parent = dirname(dir);
      if (parent === dir) break;
      dir = parent;
    }
  }
  throw new Error(
    'Could not locate the Genesis root (a directory containing templates/ and spec.schema.json) ' +
      'above the GUI app. The GUI must live as gui/ inside the Genesis repo.',
  );
}

/**
 * The CLI's cwd and every GUI-written file. Packaged, it is created on first use and the bundled
 * sample spec + seed are copied in (never overwriting the user's copies), because the CLI writes
 * project-spec.lock.json next to the spec it is given - that must not be the read-only install.
 */
export function workspaceRoot(): string {
  if (cachedWorkspace) return cachedWorkspace;
  if (!app.isPackaged) return (cachedWorkspace = installRoot());

  const ws = join(app.getPath('documents'), 'DPS-NN');
  mkdirSync(ws, { recursive: true });
  for (const sub of ['fixtures', 'seeds']) {
    const from = join(installRoot(), sub);
    if (existsSync(from)) cpSync(from, join(ws, sub), { recursive: true, force: false });
  }
  return (cachedWorkspace = ws);
}

/** The bundled self-contained CLI in a packaged build; null in a repo checkout (dev uses `dotnet run`). */
export function bundledCli(): string | null {
  if (!app.isPackaged) return null;
  return join(installRoot(), process.platform === 'win32' ? 'dps-nn.exe' : 'dps-nn');
}

/** Directories the spec list-select scans, in display order (matches the CLI pickers). */
export function specSearchDirs(): { dir: string; source: 'cwd' | 'fixtures' | 'archive' }[] {
  const root = workspaceRoot();
  return [
    { dir: root, source: 'cwd' },
    { dir: join(root, 'fixtures'), source: 'fixtures' },
    { dir: join(root, 'prompts', 'out', 'schemas'), source: 'archive' },
  ];
}

/** Directories the seed list-select scans (matches ResolveSeedInteractively). */
export function seedSearchDirs(): { dir: string; source: 'seeds' | 'archive' }[] {
  const root = workspaceRoot();
  return [
    { dir: join(root, 'seeds'), source: 'seeds' },
    { dir: join(root, 'prompts', 'out', 'seeds'), source: 'archive' },
  ];
}

/** Where uploaded files are staged before being handed to the CLI. */
export function uploadsDir(): string {
  return app.isPackaged ? join(workspaceRoot(), 'uploads') : join(workspaceRoot(), 'gui', '.uploads');
}

/** Where the IMPERATOR theme's "Play Veselin Marinov" media lives (Stefan drops files in here),
 *  split into 3 category subfolders instead of one flat directory. */
export function musicDir(): string {
  return join(installRoot(), 'design', 'music');
}
export type MusicCategory = 'songsAudio' | 'concertsAudio' | 'concertsVideo';
const MUSIC_SUBDIRS: Record<MusicCategory, string> = {
  songsAudio: 'songs-audio',
  concertsAudio: 'concerts-audio',
  concertsVideo: 'concerts-video',
};
export function musicCategoryDir(category: MusicCategory): string {
  return join(musicDir(), MUSIC_SUBDIRS[category]);
}
