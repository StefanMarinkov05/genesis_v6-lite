import { type ChildProcess, spawn } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readFileSync, statSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';
import type {
  BuildError,
  BuildReport,
  FileHandle,
  ProgressEvent,
  RunId,
  StageName,
} from '@shared/ipc-types';
import { workspaceRoot } from './paths';
import { effectiveApiKey, effectiveGenesisCommand, effectiveOutDir } from './settings';
import { recordBuild } from './dedup';
import { parseCompletionBanner } from './report';
import { agentStopLine, tryReadProjectName } from './agent-helpers';

// This module owns the CLI child-process lifecycle. It embodies the inviolable rule from
// docs/explanation/gui-architecture.md: it spawns ONE `run --all` and controls its
// lifecycle; it never drives stages one-by-one or reimplements pipeline logic in Node.
//
// Progress comes from tailing {out}/{Project}/dps-nn.log.jsonl (PipelineRunner.AppendLog),
// reconciled with the state.json baseline so a resume that skips half the pipeline still
// shows an accurate bar. The debug terminal is fed separately from raw stdout/stderr.

/** The seven pipeline stages that `run --all` executes and that emit to dps-nn.log.jsonl. */
const PIPELINE_STAGES: StageName[] = [
  'scaffold',
  'emit-models',
  'identity',
  'emit-controllers-views',
  'seed',
  'verify',
  'report',
];

/** Exit code the CLI uses for "waiting for a manual LLM response" (Program.cs). */
const EXIT_AWAITING_LLM = 3;

export interface RunnerEvents {
  progress(e: ProgressEvent): void;
  log(line: string): void;
  awaitingLlm(e: { stage: string; requestPath: string }): void;
  done(e: BuildReport): void;
  error(e: BuildError): void;
}

interface RunState {
  runId: RunId;
  /** 'run' = `run --all` on a chosen spec; 'create' = agent mode, spec drafted by the CLI. */
  mode: 'run' | 'create';
  /** create mode only: the description file handed to `create --describe-file`. */
  descriptionPath: string | null;
  spec: FileHandle;
  seed: FileHandle | null;
  child: ChildProcess | null;
  projectName: string;
  outDirAbs: string;
  projectDir: string;
  logPath: string;
  /** Byte offset in the jsonl at spawn time — only bytes past this are new. */
  logOffset: number;
  /** Completed pipeline stages carried from a prior run (state.json baseline). */
  baselineDone: number;
  index: number;
  /** Accumulated stdout for banner parsing at exit. */
  stdout: string;
  /** Tail of stderr for error reporting. */
  stderrTail: string[];
  lastStartedStage: string | null;
  tailTimer: NodeJS.Timeout | null;
  /** Set when the user explicitly Stopped — suppresses the error path on the resulting kill. */
  stopping: boolean;
  report: BuildReport | null;
}

export class GenesisRunner {
  private readonly runs = new Map<RunId, RunState>();
  /** Resolves when the startup `doctor` has exited; runs wait for it (see doctor()). */
  private ready: Promise<void> = Promise.resolve();

  constructor(private readonly ev: RunnerEvents) {}

  /** Spawn `run --all` for a (spec, seed) pair. Returns a runId addressing its lifecycle. */
  start(spec: FileHandle, seed: FileHandle | null): RunId {
    const root = workspaceRoot();
    const outDirAbs = effectiveOutDir();
    const projectName = readProjectName(spec.path);
    const projectDir = join(outDirAbs, projectName);
    const logPath = join(projectDir, 'dps-nn.log.jsonl');

    // Pre-place the chosen seed where the CLI reads it. Under --no-interactive the CLI's
    // own seed picker is skipped, so the GUI must stage the file itself (arch doc).
    if (seed) placeSeed(root, seed.path, projectDir);

    const st: RunState = {
      runId: randomUUID(),
      mode: 'run',
      descriptionPath: null,
      spec,
      seed,
      child: null,
      projectName,
      outDirAbs,
      projectDir,
      logPath,
      logOffset: existsSync(logPath) ? statSync(logPath).size : 0,
      baselineDone: countBaselineDone(projectDir),
      index: 0,
      stdout: '',
      stderrTail: [],
      lastStartedStage: null,
      tailTimer: null,
      stopping: false,
      report: null,
    };
    st.index = st.baselineDone;
    this.runs.set(st.runId, st);

    this.spawnChild(st);
    return st.runId;
  }

  /**
   * Agent mode: `create --describe-file <file> --out <out>/agent_<stamp>`. The project name (and
   * so the log to tail) is unknown until the CLI has drafted the spec, and a repair round may
   * rename it — readTail re-syncs from the spec file on every poll (syncAgentProject).
   */
  create(description: string): RunId {
    const text = description.trim();
    if (!text) throw new Error('The project description is empty.');
    if (!effectiveApiKey()) throw new Error('Agent mode needs an Anthropic API key (Settings).');
    const stamp = new Date().toISOString().replace(/[-:]/g, '').replace('T', '_').slice(0, 15);
    const outDirAbs = join(effectiveOutDir(), `agent_${stamp}`);
    mkdirSync(outDirAbs, { recursive: true });
    const descriptionPath = join(outDirAbs, 'description.txt');
    writeFileSync(descriptionPath, text, 'utf8');

    const st: RunState = {
      runId: randomUUID(),
      mode: 'create',
      descriptionPath,
      spec: { path: join(outDirAbs, 'project-spec.json'), name: 'project-spec.json', source: 'uploaded', size: 0 },
      seed: null,
      child: null,
      projectName: '',
      outDirAbs,
      projectDir: outDirAbs,
      logPath: join(outDirAbs, 'dps-nn.log.jsonl'), // placeholder until the spec names the project
      logOffset: 0,
      baselineDone: 0,
      index: 0,
      stdout: '',
      stderrTail: [],
      lastStartedStage: null,
      tailTimer: null,
      stopping: false,
      report: null,
    };
    this.runs.set(st.runId, st);
    this.spawnChild(st);
    return st.runId;
  }

  /** Stop: kill the child, keep state.json (a later run --all can resume). Never destroys work. */
  stop(runId: RunId): void {
    const st = this.runs.get(runId);
    if (!st) return;
    st.stopping = true;
    this.killChild(st);
  }

  /**
   * Pause: kills the child immediately (same as Stop) — NOT checkpoint-aware, despite an
   * earlier doc draft claiming otherwise (corrected in changelog 102's doc fix). Still correct:
   * every stage is idempotent and MarkDone only fires on completion, so an immediate kill
   * mid-stage just discards that one stage's partial work; Resume re-runs it from scratch.
   */
  pause(runId: RunId): void {
    const st = this.runs.get(runId);
    if (!st) return;
    st.stopping = true; // treat the kill as intentional, not an error
    this.killChild(st);
  }

  /** Resume: re-spawn run --all; done stages are skipped via state.json. */
  resume(runId: RunId): void {
    const st = this.runs.get(runId);
    if (!st) return;
    st.stopping = false;
    st.logOffset = existsSync(st.logPath) ? statSync(st.logPath).size : 0;
    st.baselineDone = countBaselineDone(st.projectDir);
    st.index = st.baselineDone;
    this.spawnChild(st);
  }

  /**
   * `dps-nn doctor` at GUI startup, streamed into the debug terminal like any CLI output: the
   * prerequisite report (SDK, dotnet-ef, scaffolder, LocalDB, API key) with the fix for each gap.
   * Informational only - a failing check never blocks Start. Runs spawned meanwhile wait for it:
   * in dev both would `dotnet run` the CLI project, and two concurrent builds of it collide on
   * Windows file locks.
   */
  doctor(): void {
    this.ready = new Promise((done) => {
      const { program, baseArgs } = effectiveGenesisCommand();
      let child: ChildProcess;
      try {
        child = spawn(program, [...baseArgs, 'doctor'], { cwd: workspaceRoot(), env: childEnv(), shell: false });
      } catch (e) {
        this.ev.log(`dps-nn doctor could not start: ${(e as Error).message}`);
        done();
        return;
      }
      const forward = (buf: Buffer): void => {
        for (const line of buf.toString().split(/\r?\n/)) if (line) this.ev.log(line);
      };
      child.stdout?.on('data', forward);
      child.stderr?.on('data', forward);
      child.on('error', (e) => {
        this.ev.log(`dps-nn doctor could not start: ${e.message}`);
        done();
      });
      child.on('exit', () => done());
    });
  }

  getReport(runId: RunId): BuildReport | null {
    return this.runs.get(runId)?.report ?? null;
  }

  // ---- internals ------------------------------------------------------------

  private spawnChild(st: RunState): void {
    // A Stop/Pause that lands while waiting for the doctor simply means "never spawn".
    void this.ready.then(() => {
      if (!st.stopping) this.spawnNow(st);
    });
  }

  private spawnNow(st: RunState): void {
    const root = workspaceRoot();
    const { program, baseArgs } = effectiveGenesisCommand();
    const args =
      st.mode === 'create'
        ? [...baseArgs, 'create', '--describe-file', st.descriptionPath!, '--out', st.outDirAbs, '--no-interactive']
        : [...baseArgs, 'run', '--all', '--spec', st.spec.path, '--out', st.outDirAbs, '--no-interactive'];

    const child = spawn(program, args, { cwd: root, env: childEnv(), shell: false });
    st.child = child;
    st.stopping = false;

    child.stdout?.on('data', (buf: Buffer) => this.onStdout(st, buf.toString()));
    child.stderr?.on('data', (buf: Buffer) => this.onStderr(st, buf.toString()));
    child.on('error', (e) => {
      this.stopTail(st);
      this.ev.error({ stage: null, message: `Failed to launch Genesis CLI: ${e.message}` });
    });
    child.on('exit', (code) => this.onExit(st, code));

    this.startTail(st);
  }

  private onStdout(st: RunState, chunk: string): void {
    st.stdout += chunk;
    for (const line of chunk.split(/\r?\n/)) if (line) this.ev.log(line);
  }

  private onStderr(st: RunState, chunk: string): void {
    for (const line of chunk.split(/\r?\n/)) {
      if (!line) continue;
      this.ev.log(line);
      st.stderrTail.push(line);
      if (st.stderrTail.length > 40) st.stderrTail.shift();
    }
  }

  private startTail(st: RunState): void {
    // Poll the jsonl for appended stage-boundary lines. Polling (not fs.watch) is the
    // robust cross-platform choice and the file may not exist until the first stage runs.
    st.tailTimer = setInterval(() => this.readTail(st), 250);
  }

  private stopTail(st: RunState): void {
    if (st.tailTimer) {
      clearInterval(st.tailTimer);
      st.tailTimer = null;
    }
  }

  /** create mode: point the tail at the project the CLI's spec currently names. */
  private syncAgentProject(st: RunState): void {
    const name = tryReadProjectName(st.spec.path);
    if (!name || name === st.projectName) return;
    st.projectName = name;
    st.projectDir = join(st.outDirAbs, name);
    st.logPath = join(st.projectDir, 'dps-nn.log.jsonl');
    st.logOffset = 0;
    st.index = 0;
  }

  private readTail(st: RunState): void {
    if (st.mode === 'create') this.syncAgentProject(st);
    if (!existsSync(st.logPath)) return;
    const size = statSync(st.logPath).size;
    if (size < st.logOffset) {
      // The log was recreated (agent repair round wipes and regenerates the project): start over.
      st.logOffset = 0;
      st.index = 0;
    }
    if (size <= st.logOffset) return;
    const buf = readFileSync(st.logPath);
    const fresh = buf.subarray(st.logOffset, size).toString('utf8');
    st.logOffset = size;
    for (const line of fresh.split(/\r?\n/)) {
      const trimmed = line.trim();
      if (!trimmed) continue;
      let entry: { stage?: string; phase?: string };
      try {
        entry = JSON.parse(trimmed);
      } catch {
        continue; // a partially-flushed line; the next poll re-reads from the same offset boundary
      }
      if (!entry.stage || !entry.phase) continue;
      if (entry.phase === 'start') {
        st.lastStartedStage = entry.stage;
      } else if (entry.phase === 'end') {
        st.index = Math.min(st.index + 1, PIPELINE_STAGES.length);
      }
      this.emitProgress(st, entry.stage, entry.phase as ProgressEvent['phase']);
    }
  }

  private emitProgress(st: RunState, stage: string, phase: ProgressEvent['phase']): void {
    this.ev.progress({ stage, phase, index: st.index, total: PIPELINE_STAGES.length });
  }

  private killChild(st: RunState): void {
    if (!st.child) return;
    try {
      // Kill the whole tree — `dotnet run` spawns the actual app as a grandchild.
      if (process.platform === 'win32') {
        spawn('taskkill', ['/pid', String(st.child.pid), '/T', '/F']);
      } else {
        st.child.kill('SIGTERM');
      }
    } catch {
      /* already gone */
    }
  }

  private onExit(st: RunState, code: number | null): void {
    this.stopTail(st);
    this.readTail(st); // drain any final lines
    st.child = null;

    if (st.stopping) return; // intentional Stop/Pause — no done/error event

    if (code === 0) {
      const report = parseCompletionBanner(st.stdout, st.projectDir);
      report.summaryPath = summaryPathIfExists(st.projectDir);
      st.report = report;
      if (existsSync(st.spec.path)) recordBuild(st.spec, st.seed, {
        project: st.projectName,
        outDir: st.projectDir,
        ts: new Date().toISOString(),
        status: 'success',
      });
      this.ev.done(report);
      return;
    }

    if (code === EXIT_AWAITING_LLM) {
      this.ev.awaitingLlm({
        stage: st.lastStartedStage ?? 'seed',
        requestPath: join(workspaceRoot(), 'prompts', 'in'),
      });
      return;
    }

    // Any other non-zero (or null on kill we didn't initiate) is a genuine failure.
    const stage = st.lastStartedStage ?? null;
    const message =
      (st.mode === 'create' ? agentStopLine(st.stdout) : null) ||
      st.stderrTail.slice(-6).join('\n') ||
      `Genesis exited with code ${code}`;
    if (st.mode === 'run') recordBuild(st.spec, st.seed, {
      project: st.projectName,
      outDir: st.projectDir,
      ts: new Date().toISOString(),
      status: 'failed',
    });
    this.ev.error({ stage, message });
  }
}

// ---- helpers ----------------------------------------------------------------

/** The CLI child's env: the GUI's own, plus the API key injected into the child only (security model). */
function childEnv(): NodeJS.ProcessEnv {
  const env = { ...process.env };
  const key = effectiveApiKey();
  if (key) env.ANTHROPIC_API_KEY = key;
  return env;
}

/** Read meta.projectName from a spec JSON; the log/state paths are keyed by it. */
function readProjectName(specPath: string): string {
  const name = tryReadProjectName(specPath);
  if (name) return name;
  throw new Error(`Spec at ${specPath} has no meta.projectName — cannot locate its output.`);
}


/** Copy the chosen seed to prompts/in/seed-response.json and clear any stale seed-data.json. */
function placeSeed(root: string, seedPath: string, projectDir: string): void {
  const promptsIn = join(root, 'prompts', 'in');
  if (!existsSync(promptsIn)) mkdirSync(promptsIn, { recursive: true });
  copyFileSync(seedPath, join(promptsIn, 'seed-response.json'));
  // Mirror ResolveSeedInteractively: a stale Data/seed-data.json would let SeedStage reuse
  // old data and silently ignore the fresh choice.
  const stale = join(projectDir, 'Data', 'seed-data.json');
  if (existsSync(stale)) {
    try {
      rmSync(stale);
    } catch {
      /* best-effort */
    }
  }
}

/** Count completed pipeline stages from state.json (the resume baseline). */
function countBaselineDone(projectDir: string): number {
  const path = join(projectDir, 'plan', 'state.json');
  if (!existsSync(path)) return 0;
  try {
    const doc = JSON.parse(readFileSync(path, 'utf8'));
    const done: string[] = doc?.completedStages ?? [];
    return PIPELINE_STAGES.filter((s) => done.includes(s)).length;
  } catch {
    return 0;
  }
}

function summaryPathIfExists(projectDir: string): string | null {
  const p = join(projectDir, 'wwwroot', 'output', 'project-summary.json');
  return existsSync(p) ? p : null;
}

/**
 * Launch the generated app: `dotnet run --project <dir>`, spawned as argv (shell: false) —
 * never a shell command string. See changelog 083: the CLI's own banner text used to read
 * `cd {dir} && dotnet run`, which hard-fails to parse in Windows PowerShell 5.1 (`&&` is a
 * PS7-only operator). Spawning argv directly sidesteps that whole class of bug regardless of
 * the user's shell. Detached + unref'd so the dev server outlives the GUI if the GUI closes.
 */
// Tracks the most recently launched generated-app process per project dir, so killGeneratedApp
// can find it later. runApp/killApp are independent, ad-hoc actions from the report screen (not
// part of the tracked pipeline run in `runs`). Persisted to logs/launched-apps.json (not just an
// in-memory Map) so Kill app still works after a GUI restart while the generated app is still
// running — an in-memory-only map would silently strand the process with no GUI-side kill path
// short of re-launching a second instance via Run app.
function launchedAppsPath(): string {
  return join(workspaceRoot(), 'logs', 'launched-apps.json');
}

function readLaunchedApps(): Record<string, number> {
  try {
    return JSON.parse(readFileSync(launchedAppsPath(), 'utf8'));
  } catch {
    return {};
  }
}

function writeLaunchedApps(apps: Record<string, number>): void {
  try {
    mkdirSync(join(workspaceRoot(), 'logs'), { recursive: true });
    writeFileSync(launchedAppsPath(), JSON.stringify(apps));
  } catch {
    /* best-effort — worst case Kill app just won't find the PID */
  }
}

export function runGeneratedApp(projectDir: string): void {
  if (!existsSync(projectDir)) {
    throw new Error(`Project folder no longer exists: ${projectDir}`);
  }
  const child = spawn('dotnet', ['run', '--project', projectDir], {
    cwd: projectDir,
    shell: false,
    detached: true,
    stdio: 'ignore',
  });
  if (child.pid) {
    const apps = readLaunchedApps();
    apps[projectDir] = child.pid;
    writeLaunchedApps(apps);
  }
  child.unref();
}

/** Kill the app process runApp() most recently launched for this project dir, if still tracked. */
export function killGeneratedApp(projectDir: string): void {
  const apps = readLaunchedApps();
  const pid = apps[projectDir];
  if (!pid) return;
  delete apps[projectDir];
  writeLaunchedApps(apps);
  try {
    // Kill the whole tree — `dotnet run` spawns the actual ASP.NET app as a grandchild.
    if (process.platform === 'win32') {
      spawn('taskkill', ['/pid', String(pid), '/T', '/F']);
    } else {
      process.kill(pid, 'SIGTERM');
    }
  } catch {
    /* already gone */
  }
}
