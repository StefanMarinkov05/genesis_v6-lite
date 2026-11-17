// The typed GUI↔CLI IPC contract. Single source of truth for both the preload
// (which exposes it) and the renderer (which consumes it). Channel names and shapes
// are fixed by docs/explanation/gui-architecture.md ("IPC channel surface"); do not
// add a channel here without updating that doc in the same commit (same-commit rule).
//
// MVP scope (spec B.0 phase 1): spec/seed input, dedup, Start/Stop, progress, debug
// terminal, generation screen. Pause/Resume and the Claude-seed button are phase 2 —
// their channels are declared so the surface is stable, but the renderer disables
// their controls until then.

/** "Play Veselin Marinov" media category (design/music/{songs-audio,concerts-audio,concerts-video}/). */
export type MusicCategory = 'songsAudio' | 'concertsAudio' | 'concertsVideo';

/** A resolved spec or seed file the main process has validated and can hand to the CLI. */
export interface FileHandle {
  /** Absolute path on disk (main-process only ever acts on this). */
  path: string;
  /** Basename shown in the UI. */
  name: string;
  /** Where it was found — drives grouping in the list-select. */
  source: 'fixtures' | 'archive' | 'cwd' | 'seeds' | 'uploaded';
  /** Exact size in bytes — shown everywhere a file is listed, so identical/edited copies are visible at a glance. */
  size: number;
}

/** Result of a duplicate check: a prior completed build for this (spec+seed) hash, or null. */
export interface DuplicateMatch {
  project: string;
  /** ISO timestamp of the last successful build. */
  ts: string;
  outDir: string;
}

/** One entry from the CLI's logs/registry.json (read-only — the GUI never writes this file). */
export interface RecentProject {
  project: string;
  /** ISO timestamp of that project's last run. */
  ts: string;
  specPath: string;
  outDir: string;
}

/** The canonical stage order the progress feed emits (see PipelineRunner.CanonicalOrder). */
export type StageName =
  | 'spec-build'
  | 'spec-expand'
  | 'scaffold'
  | 'emit-models'
  | 'identity'
  | 'emit-controllers-views'
  | 'seed'
  | 'verify'
  | 'report';

/** Push event: one stage boundary from dps-nn.log.jsonl, reconciled with state.json baseline. */
export interface ProgressEvent {
  stage: StageName | string;
  phase: 'start' | 'end' | 'error';
  /** Completed-stage count for this invocation. */
  index: number;
  /** Stages that will run for this invocation (known up front). */
  total: number;
}

/** Parsed completion banner + project-summary.json (report:get / build:done). */
export interface BuildReport {
  url: string | null;
  credentials: { role: string; email: string; password: string }[];
  dbName: string | null;
  startCommand: string | null;
  /** The generated project's output directory (for "open folder"). */
  outDir: string | null;
  summaryPath: string | null;
}

export interface BuildError {
  stage: StageName | string | null;
  message: string;
}

/** Renderer-visible settings. The API key is WRITE-ONLY: settings:get never returns it. */
export interface Settings {
  /** Selected wallpaper id (themes.ts WALLPAPERS) — carries its own theme colors + fonts. */
  wallpaper: string;
  language: string;
  /** CLI invocation override (ADR-010 "CLI stays first-class"). */
  genesisCommand: string;
  outDir: string;
  /** True iff an Anthropic key is configured — the value itself never crosses to the renderer. */
  apiKeySet: boolean;
}

export type SettingsPatch = Partial<Omit<Settings, 'apiKeySet'>> & {
  /** Write-only; accepted here, never returned by settings:get. */
  apiKey?: string;
};

/** A run identifier handed back by build:start, used to address pause/resume/stop. */
export type RunId = string;

/** The entire preload surface exposed on window.gui. Keep minimal and typed (security model). */
export interface GuiApi {
  // ---- spec / seed input (renderer → main, invoke) ----
  specList(): Promise<FileHandle[]>;
  specUpload(bytes: Uint8Array, name: string): Promise<FileHandle>;
  seedList(): Promise<FileHandle[]>;
  seedUpload(bytes: Uint8Array, name: string): Promise<FileHandle>;
  /** Phase 2 — main-process Anthropic call; declared now, disabled in the MVP UI. */
  seedGenerateWithClaude(spec: FileHandle): Promise<FileHandle>;

  // ---- dedup + run lifecycle ----
  checkDuplicate(spec: FileHandle, seed: FileHandle | null): Promise<DuplicateMatch | null>;
  buildStart(spec: FileHandle, seed: FileHandle | null, opts?: { force?: boolean }): Promise<RunId>;
  /**
   * Agent mode (ADR-013): spawns `create --describe-file` — the CLI drafts the spec from the
   * description, runs the pipeline and repairs the spec on failure. Needs the API key in settings.
   * Same lifecycle/push events as buildStart; the project name is learned once the spec exists.
   */
  buildCreate(description: string): Promise<RunId>;
  buildPause(runId: RunId): Promise<void>; // phase 2
  buildResume(runId: RunId): Promise<void>; // phase 2
  buildStop(runId: RunId): Promise<void>;

  // ---- settings / footer / report ----
  settingsGet(): Promise<Settings>;
  settingsSet(patch: SettingsPatch): Promise<Settings>;
  footerGet(): Promise<string>;
  reportGet(runId: RunId): Promise<BuildReport | null>;

  /** Every project the CLI has ever recorded a completed run for (logs/registry.json), newest first. */
  recentProjectsList(): Promise<RecentProject[]>;

  // ---- IMPERATOR "Play Veselin Marinov" (design/music/{songs-audio,concerts-audio,
  // concerts-video}/) ----
  /** Absolute path to a randomly-picked file in the given category, or null if none exist. */
  musicRandomTrack(category: MusicCategory): Promise<string | null>;
  /** Every filename in the given category's subfolder (for a "pick a specific one" list), sorted. */
  musicList(category: MusicCategory): Promise<string[]>;

  // ---- window chrome ----
  windowMinimize(): void;
  windowMaximize(): void;
  windowClose(): void;

  // ---- shell (main-process only; open URLs / folders in the OS) ----
  openExternal(url: string): Promise<void>;
  openPath(path: string): Promise<void>;

  /**
   * Launch the generated app: spawns `dotnet run --project <dir>` directly (argv, no shell) —
   * see changelog 083. Never builds a shell command string, so it's immune to the `&&`/quoting
   * differences across PowerShell 5.1 / 7 / cmd.exe that broke the CLI's own banner text.
   */
  runApp(projectDir: string): Promise<void>;

  /**
   * Kill the app process most recently launched via runApp() for this project dir. Distinct
   * from build:stop (which cancels the CLI PIPELINE) — this targets the generated app's own
   * `dotnet run`, which runApp starts detached and would otherwise have no GUI-side stop control.
   */
  killApp(projectDir: string): Promise<void>;

  // ---- main → renderer (push events); each returns an unsubscribe fn ----
  onProgress(cb: (e: ProgressEvent) => void): () => void;
  onLog(cb: (line: string) => void): () => void;
  onAwaitingLlm(cb: (e: { stage: string; requestPath: string }) => void): () => void;
  onDone(cb: (e: BuildReport) => void): () => void;
  onError(cb: (e: BuildError) => void): () => void;
}

/** Channel-name constants shared by preload + main so the two never drift. */
export const CH = {
  specList: 'spec:list',
  specUpload: 'spec:upload',
  seedList: 'seed:list',
  seedUpload: 'seed:upload',
  seedGenerateWithClaude: 'seed:generateWithClaude',
  checkDuplicate: 'build:checkDuplicate',
  buildStart: 'build:start',
  buildCreate: 'build:create',
  buildPause: 'build:pause',
  buildResume: 'build:resume',
  buildStop: 'build:stop',
  settingsGet: 'settings:get',
  settingsSet: 'settings:set',
  footerGet: 'footer:get',
  reportGet: 'report:get',
  recentProjectsList: 'recentProjects:list',
  musicRandomTrack: 'music:randomTrack',
  musicList: 'music:list',
  windowMinimize: 'window:minimize',
  windowMaximize: 'window:maximize',
  windowClose: 'window:close',
  openExternal: 'shell:openExternal',
  openPath: 'shell:openPath',
  runApp: 'shell:runApp',
  killApp: 'shell:killApp',
  // push events (main → renderer)
  onProgress: 'build:progress',
  onLog: 'build:log',
  onAwaitingLlm: 'build:awaiting-llm',
  onDone: 'build:done',
  onError: 'build:error',
} as const;
