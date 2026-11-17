import '@xterm/xterm/css/xterm.css';
import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';
import type { BuildReport, DuplicateMatch, FileHandle, MusicCategory, RecentProject } from '@shared/ipc-types';
import { LANGUAGES, langMeta, normalizeLang, stringsFor, type LangCode, type Strings } from './i18n';
import { flagSvg } from './i18n/flags';
import { activeButton, enablement, type RunState } from './state';
import { WALLPAPERS, normalizeWallpaper, type WallpaperMeta } from './themes';
import './styles.css';

// Renderer bootstrap (NOVA theme). It owns screen state and talks to the privileged main
// process only through window.gui (the typed preload surface). No fs, no child_process, no
// key here — the renderer is fully sandboxed (security model).
//
// Language (phase 3b) is renderer-local: `t` is the active string table, swapped live by the
// flag picker and persisted through the existing settings.language field. This is the GUI's
// OWN chrome language only — it never changes the language a generated app is produced in
// (that's the spec's meta.language, a separate axis). See the scope note in i18n/en.ts.

const gui = window.gui;
let currentLang: LangCode = 'en';
let t: Strings = stringsFor(currentLang);
let currentWallpaper: WallpaperMeta = WALLPAPERS[0]!;

function $<T extends HTMLElement = HTMLElement>(id: string): T {
  const el = document.getElementById(id);
  if (!el) throw new Error(`missing #${id}`);
  return el as T;
}

// ---- run/selection state ----------------------------------------------------
let specs: FileHandle[] = [];
let seeds: FileHandle[] = [];
let selectedSpec: FileHandle | null = null;
let selectedSeed: FileHandle | null = null;
let runId: string | null = null;
let runState: RunState = 'idle';
let pendingDup: DuplicateMatch | null = null;

const TOTAL_STAGES = 7; // the pipeline stages that emit progress (genesis-runner PIPELINE_STAGES)
const APP_VERSION = '6.6.6'; // mirrors gui/package.json "version" - no IPC channel to read it live yet

// ---- debug terminal ---------------------------------------------------------
const term = new Terminal({
  convertEol: true,
  fontFamily: '"Cascadia Code", "Consolas", ui-monospace, monospace',
  fontSize: 12,
  theme: { background: '#03070e', foreground: '#a9c3e6', cursor: '#3fd0ff' },
  scrollback: 5000,
  disableStdin: true, // display-only: we pipe the CLI's output, we never send input to it
});
const fit = new FitAddon();
term.loadAddon(fit);

// DPS-NN ASCII banner, written once the terminal is first mounted - visible immediately when
// Debug is opened, even before Start is ever clicked (Stefan). ANSI colors: cyan for the glyphs
// (matches the accent most themes share a family with), muted gray for status lines, dim yellow
// for the warning-style log line. A function (not a static array) because it reads `t` - the
// tagline and the flagged-project line both need to reflect the CURRENT language, including
// Cyrillic "ДПС-НН" for BG/RU rather than a hardcoded "DPS-NN" (Stefan: "translate the name to
// дпс-нн as well").
function buildBanner(): string[] {
  return [
    '\x1b[36m██████╗ ██████╗ ███████╗    ███╗   ██╗███╗   ██╗\x1b[0m',
    '\x1b[36m██╔══██╗██╔══██╗██╔════╝    ████╗  ██║████╗  ██║\x1b[0m',
    '\x1b[36m██║  ██║██████╔╝███████╗    ██╔██╗ ██║██╔██╗ ██║\x1b[0m',
    '\x1b[36m██║  ██║██╔═══╝ ╚════██║    ██║╚██╗██║██║╚██╗██║\x1b[0m',
    '\x1b[36m██████╔╝██║     ███████║    ██║ ╚████║██║ ╚████║\x1b[0m',
    '\x1b[36m╚═════╝ ╚═╝     ╚══════╝    ╚═╝  ╚═══╝╚═╝  ╚═══╝\x1b[0m',
    '',
    `\x1b[90m${t.hq} — ${t.title} — v${APP_VERSION}\x1b[0m`,
    '\x1b[90m© 2026 DPS-NN (ДПС-НН). All rights reserved.\x1b[0m',
    '\x1b[90mDebug terminal — pipeline output streams here once a build starts.\x1b[0m',
    '',
    '\x1b[33m[dps-nn]  проектът е белязан\x1b[0m',
    '',
    '\x1b[90mIdle. Waiting for Start.\x1b[0m',
    '',
  ];
}
let bannerWritten = false;
function ensureTerminalMounted(): void {
  if (term.element) return;
  term.open($('terminal'));
  if (!bannerWritten) {
    for (const line of buildBanner()) term.writeln(line);
    bannerWritten = true;
  }
}
function refitTerminal(): void {
  if (!term.element || $('terminal-sheet').getAttribute('data-open') !== 'true') return;
  try {
    fit.fit();
  } catch {
    /* terminal not laid out yet */
  }
}

// ---- toasts -----------------------------------------------------------------
type ToastKind = 'info' | 'success' | 'warning' | 'danger';
function toast(kind: ToastKind, msg: string, sub = ''): void {
  const el = document.createElement('div');
  el.className = `toast ${kind}`;
  el.setAttribute('role', 'status');
  el.innerHTML =
    `<span class="tdot"></span><div><div class="tmsg"></div>` +
    (sub ? `<div class="tsub"></div>` : '') +
    `</div><button type="button" class="tclose" aria-label="close">✕</button><span class="trail"></span>`;
  (el.querySelector('.tmsg') as HTMLElement).textContent = msg;
  if (sub) (el.querySelector('.tsub') as HTMLElement).textContent = sub;
  el.querySelector('.tclose')!.addEventListener('click', () => el.remove());
  $('toast-stack').appendChild(el);
  let timer = window.setTimeout(() => el.remove(), 5000);
  el.addEventListener('mouseenter', () => clearTimeout(timer));
  el.addEventListener('mouseleave', () => (timer = window.setTimeout(() => el.remove(), 2500)));
}

// ---- run-control state ------------------------------------------------------
function setRunState(s: RunState): void {
  runState = s;
  const en2 = enablement(s);
  ($('run-start') as HTMLButtonElement).disabled = !en2.start;
  ($('run-pause') as HTMLButtonElement).disabled = !en2.pause;
  ($('run-resume') as HTMLButtonElement).disabled = !en2.resume;
  ($('run-stop') as HTMLButtonElement).disabled = !en2.stop;
  const active = activeButton(s);
  for (const key of ['start', 'pause', 'resume', 'stop'] as const) {
    $(`run-${key}`).classList.toggle('active', active === key);
  }
  // A run can't start without a spec.
  if ((s === 'idle' || s === 'done' || s === 'error' || s === 'stopped') && !selectedSpec) {
    ($('run-start') as HTMLButtonElement).disabled = true;
  }
  const generating = s === 'running' || s === 'awaiting-llm' || s === 'paused';
  // Spec/Seed panels are only actionable before a run starts or after it ends - hide them while
  // one is actively in flight (running/paused/awaiting-llm) so the terminal/progress get the
  // space instead. They come back on idle/done/error/stopped.
  $('panels').hidden = generating;
  // Minimalism pass (Stefan: "too many buttons") - back to hiding what's not relevant, not just
  // disabling it. Progress bar + Pause/Resume/Stop only appear once something is actually
  // generating; Start is the mirror image, only shown before/after a run (there's nothing to
  // "start" mid-run - that's what the other three act on).
  $('progress-wrap').hidden = !generating;
  ($('run-start') as HTMLButtonElement).hidden = generating;
  ($('run-pause') as HTMLButtonElement).hidden = !generating;
  ($('run-resume') as HTMLButtonElement).hidden = !generating;
  ($('run-stop') as HTMLButtonElement).hidden = !generating;
  // Explicit "only one button visible" flag (was relying on CSS grid auto-fit/flex-grow alone
  // to fill the row when there's a single item - reported still narrower than the toolbar chips
  // above it FIVE times over, despite that being textbook-correct CSS; switching to a JS-driven
  // class removes all ambiguity about what's actually happening in the cascade). .run-row.single
  // (below) forces that one button to a flat, unambiguous width:100% block instead of trusting
  // grid/flex distribution math.
  $('run-row').classList.toggle('single', !generating);
}

type ProgressCls = '' | 'running' | 'success' | 'error';
// Last non-idle progress paint, so a mid-run language switch can restore the bar/count after
// retranslate() re-applies the idle defaults. Stage names come from the CLI (not translated).
let lastProgress: { index: number; total: number; stage: string; cls: ProgressCls } | null = null;

function setProgress(index: number, total: number, stage: string, cls: ProgressCls): void {
  $('progress-stage').textContent = stage;
  $('progress-count').textContent = t.progress.of(index, total);
  const fillEl = $('progress-fill');
  fillEl.style.width = `${Math.round((index / total) * 100)}%`;
  fillEl.classList.remove('running', 'success', 'error');
  if (cls) fillEl.classList.add(cls);
  lastProgress = { index, total, stage, cls };
}

// ---- selection UI -----------------------------------------------------------
// Exact byte count (never rounded to KB/MB) shown everywhere a file is listed, so two
// otherwise-identical-looking entries (e.g. a fixture vs its stale archived copy) are
// distinguishable at a glance without opening either file.
function formatBytes(n: number): string {
  return `${n.toLocaleString('en-US')} B`;
}

function optionLabel(f: FileHandle): string {
  const size = formatBytes(f.size);
  return f.source === 'uploaded' ? `${f.name} (uploaded, ${size})` : `${f.name} · ${f.source} · ${size}`;
}

function populateSelect(select: HTMLSelectElement, list: FileHandle[], placeholder: string): void {
  select.innerHTML = '';
  const ph = document.createElement('option');
  ph.value = '';
  ph.textContent = placeholder;
  select.appendChild(ph);
  list.forEach((f, i) => {
    const opt = document.createElement('option');
    opt.value = String(i);
    opt.textContent = optionLabel(f);
    select.appendChild(opt);
  });
}

async function reloadLists(): Promise<void> {
  [specs, seeds] = await Promise.all([gui.specList(), gui.seedList()]);
  populateSelect($('spec-select') as HTMLSelectElement, specs, t.spec.pick);
  populateSelect($('seed-select') as HTMLSelectElement, seeds, t.seed.pick);
}

// A fresh install opens ready to Start with no API key: when nothing is chosen yet, preselect the
// bundled FaceNote sample spec + its seed (the seed is what makes the seed stage key-free). Only
// those exact files; anything else is left to the user. (ADR-014, changelog 134)
function preselectSample(): void {
  if (selectedSpec) return;
  const spec = specs.findIndex((f) => f.name === 'project-spec.facenote.json' && f.source === 'fixtures');
  if (spec < 0) return;
  ($('spec-select') as HTMLSelectElement).value = String(spec);
  onSpecChosen(specs[spec]);
  const seed = seeds.findIndex((f) => f.name === 'facenote-rich-seed.json' && f.source === 'seeds');
  if (selectedSeed || seed < 0) return;
  ($('seed-select') as HTMLSelectElement).value = String(seed);
  onSeedChosen(seeds[seed]);
}

function onSpecChosen(f: FileHandle | null): void {
  selectedSpec = f;
  $('spec-well').textContent = f ? `${f.name} (${formatBytes(f.size)})` : t.spec.none;
  setRunState(runState); // re-evaluate Start enablement
}
function onSeedChosen(f: FileHandle | null): void {
  selectedSeed = f;
  $('seed-well').textContent = f ? `${f.name} (${formatBytes(f.size)})` : t.seed.none;
}

// ---- build lifecycle --------------------------------------------------------
async function attemptStart(): Promise<void> {
  openPrankDialog();
  if (!selectedSpec) {
    toast('warning', t.toast.selectSpecFirst);
    return;
  }
  const dup = await gui.checkDuplicate(selectedSpec, selectedSeed);
  if (dup) {
    pendingDup = dup;
    openDuplicateDialog(dup);
    return;
  }
  await doStart();
}

async function doStart(): Promise<void> {
  if (!selectedSpec) return;
  term.clear();
  setProgress(0, TOTAL_STAGES, '…', 'running');
  runId = await gui.buildStart(selectedSpec, selectedSeed);
  setRunState('running');
  const specPart = `${selectedSpec.name} (${formatBytes(selectedSpec.size)})`;
  const seedPart = selectedSeed ? ` · ${selectedSeed.name} (${formatBytes(selectedSeed.size)})` : '';
  toast('info', t.toast.started, specPart + seedPart);
  // A fresh run makes the previous result stale - hide the reopen control until this run finishes.
  ($('toolbar-reopen') as HTMLButtonElement).disabled = true;
}

/**
 * Agent mode (ADR-013): hand the description to `create`; the CLI drafts the spec, runs the
 * pipeline and repairs the spec on failure. Same progress/done/error events as a normal run.
 */
async function doCreate(): Promise<void> {
  if (runState === 'running' || runState === 'paused' || runState === 'awaiting-llm') return;
  const text = ($('spec-describe') as HTMLTextAreaElement).value.trim();
  if (!text) {
    toast('warning', t.toast.describeFirst);
    return;
  }
  if (!(await gui.settingsGet()).apiKeySet) {
    toast('warning', t.toast.noKey);
    return;
  }
  term.clear();
  setProgress(0, TOTAL_STAGES, '…', 'running');
  try {
    runId = await gui.buildCreate(text);
  } catch (e) {
    toast('danger', t.toast.failed, (e as Error).message);
    return;
  }
  setRunState('running');
  $('settings-sheet').setAttribute('data-open', 'false'); // the terminal is where the agent narrates
  toast('info', t.toast.agentStarted);
  ($('toolbar-reopen') as HTMLButtonElement).disabled = true;
}

async function doStop(): Promise<void> {
  if (!runId) return;
  await gui.buildStop(runId);
  setRunState('stopped');
  setProgress(Number($('progress-count').textContent!.split('/')[0]) || 0, TOTAL_STAGES, 'stopped', '');
  toast('warning', t.toast.stopped);
}

// Pause/resume (phase 2 — unblocked now that the manual npm-run-dev gate is cleared, changelog
// 102). Main-process pause() kills after the current stage's checkpoint (genesis-runner.ts); the
// same runId addresses resume(), which re-spawns run --all and skips completed stages via
// state.json. 'paused' is its own RunState (see state.ts) so Resume never appears after a plain Stop.
async function doPause(): Promise<void> {
  if (!runId) return;
  await gui.buildPause(runId);
  setRunState('paused');
  setProgress(Number($('progress-count').textContent!.split('/')[0]) || 0, TOTAL_STAGES, 'paused', '');
  toast('warning', t.toast.paused);
}

async function doResume(): Promise<void> {
  if (!runId) return;
  await gui.buildResume(runId);
  setRunState('running');
  toast('info', t.toast.resumed);
}

// ---- duplicate dialog -------------------------------------------------------
function openDuplicateDialog(dup: DuplicateMatch): void {
  $('dup-project').textContent = dup.project;
  $('dup-last').textContent = new Date(dup.ts).toLocaleString();
  $('dup-out').textContent = dup.outDir;
  $('dup-dialog').setAttribute('data-open', 'true');
  ($('dup-open') as HTMLButtonElement).focus();
}
function closeDuplicateDialog(): void {
  $('dup-dialog').setAttribute('data-open', 'false');
}

// ---- "prank" popup (Stefan: pure joke, no actual slowdown behind it) --------
// Shown once per Start click; auto-dismisses after 30s or via the Close button, whichever
// comes first.
let prankTimer: number | undefined;
function openPrankDialog(): void {
  $('prank-dialog').setAttribute('data-open', 'true');
  window.clearTimeout(prankTimer);
  prankTimer = window.setTimeout(closePrankDialog, 30_000);
}
function closePrankDialog(): void {
  $('prank-dialog').setAttribute('data-open', 'false');
  window.clearTimeout(prankTimer);
}

// ---- report / error screens -------------------------------------------------
let lastReport: BuildReport | null = null;
// Tracks whether "Run app" was clicked for the CURRENT lastReport, so reopening the popup
// (the "Last result" toolbar control) restores the correct Kill-app enabled state instead of
// always resetting it - otherwise reopening after Run app would silently strand a live process
// with no GUI-side way to kill it short of re-clicking Run app and spawning a second instance.
let appLaunchedForLastReport = false;

function copyField(value: string, displayText?: string): string {
  const safe = value.replace(/"/g, '&quot;');
  return (
    `<div class="copyfield"><code>${escapeHtml(displayText ?? value)}</code>` +
    `<button type="button" data-copy="${safe}">${t.report.copy}</button></div>`
  );
}
function escapeHtml(s: string): string {
  return s.replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' })[c]!);
}

function showReport(report: BuildReport): void {
  const isNewReport = report !== lastReport;
  if (isNewReport) appLaunchedForLastReport = false;
  lastReport = report;
  const dl = $('report-dl');
  const rows: string[] = [];
  if (report.url) rows.push(`<dt>${t.report.url}</dt><dd>${copyField(report.url)}</dd>`);
  for (const c of report.credentials) {
    // Two separate copy buttons (email, password) - a combined "email / password" field
    // meant either value had to be copied and hand-trimmed before it was usable.
    rows.push(`<dt>${escapeHtml(c.role)} — ${t.report.email}</dt><dd>${copyField(c.email)}</dd>`);
    rows.push(`<dt>${escapeHtml(c.role)} — ${t.report.password}</dt><dd>${copyField(c.password)}</dd>`);
  }
  if (report.dbName) rows.push(`<dt>${t.report.db}</dt><dd>${copyField(report.dbName)}</dd>`);
  // The real Start command (a full quoted path) is what gets copied, but displaying the whole
  // thing makes the box overflow — show a short literal instead, copy still carries the real value.
  if (report.startCommand)
    rows.push(`<dt>${t.report.start}</dt><dd>${copyField(report.startCommand, 'dotnet run')}</dd>`);
  dl.innerHTML = rows.join('');
  dl.querySelectorAll<HTMLButtonElement>('[data-copy]').forEach((b) =>
    b.addEventListener('click', () => {
      void navigator.clipboard.writeText(b.getAttribute('data-copy')!);
      b.textContent = t.report.copied;
      setTimeout(() => (b.textContent = t.report.copy), 1200);
    }),
  );
  ($('report-open-browser') as HTMLButtonElement).disabled = !report.url;
  ($('report-run-app') as HTMLButtonElement).disabled = !report.outDir;
  ($('report-kill-app') as HTMLButtonElement).disabled = !appLaunchedForLastReport;
  $('report-title-text').textContent = t.report.title;
  $('report-screen').setAttribute('data-open', 'true');
  ($('toolbar-reopen') as HTMLButtonElement).disabled = false;
}

let lastError: { stage: string | null; message: string } | null = null;

function showError(stage: string | null, message: string): void {
  lastError = { stage, message };
  $('error-title-text').textContent = t.error.title(stage ?? '—');
  $('error-stage').textContent = stage ?? '—';
  $('error-cause').textContent = message;
  $('error-screen').setAttribute('data-open', 'true');
  // Surface the terminal popup so the failing output is visible.
  if ($('terminal-sheet').getAttribute('data-open') !== 'true') toggleDebug(true);
  ($('toolbar-reopen') as HTMLButtonElement).disabled = false;
}

function closeResultScreens(): void {
  $('report-screen').setAttribute('data-open', 'false');
  $('error-screen').setAttribute('data-open', 'false');
}

// Re-shows whichever result screen (done or error) was last produced, without re-running
// anything - lets Stefan get back to the credential copy buttons after closing the popup.
function reopenLastResult(): void {
  if (lastReport) showReport(lastReport);
  else if (lastError) showError(lastError.stage, lastError.message);
}

// ---- settings ---------------------------------------------------------------
async function openSettings(): Promise<void> {
  const s = await gui.settingsGet();
  ($('s-key') as HTMLInputElement).value = '';
  ($('s-out') as HTMLInputElement).value = s.outDir;
  const state = $('s-key-state');
  state.textContent = s.apiKeySet ? t.settings.apiKeySet : t.settings.apiKeyUnset;
  state.classList.toggle('unset', !s.apiKeySet);
  $('settings-sheet').setAttribute('data-open', 'true');
}
// The Genesis command field was removed from this sheet (Stefan - it's an advanced/debugging-
// only override); genesisCommand is simply never included in the patch anymore, so
// settings.ts's own `if (patch.genesisCommand !== undefined)` guard leaves the stored default
// ('dotnet run --project src/Genesis.Cli --') permanently in effect. Nothing else changed.
async function saveSettings(): Promise<void> {
  const key = ($('s-key') as HTMLInputElement).value;
  await gui.settingsSet({
    outDir: ($('s-out') as HTMLInputElement).value,
    ...(key ? { apiKey: key } : {}),
  });
  $('settings-sheet').setAttribute('data-open', 'false');
  toast('success', t.toast.settingsSaved);
}

// ---- recent projects sheet ---------------------------------------------------
// Reads logs/registry.json (CLI-owned, GUI never writes it) via recentProjectsList().
// Every entry's "Open folder" reuses the existing shell:openPath channel — no new
// shell-access surface, same as the duplicate-build dialog's "Open existing".
function renderRecentList(projects: RecentProject[]): void {
  const list = $('recent-list');
  list.innerHTML = '';
  if (projects.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'recent-empty';
    empty.textContent = t.recent.empty;
    list.appendChild(empty);
    return;
  }
  for (const p of projects) {
    const item = document.createElement('div');
    item.className = 'recent-item';
    const name = document.createElement('div');
    name.className = 'rp-name';
    name.textContent = p.project;
    const meta = document.createElement('div');
    meta.className = 'rp-meta';
    meta.textContent = t.recent.lastBuilt(new Date(p.ts).toLocaleString());
    const open = document.createElement('button');
    open.type = 'button';
    open.className = 'rp-open';
    open.textContent = t.recent.open;
    open.addEventListener('click', () => void gui.openPath(p.outDir));
    item.append(name, meta, open);
    list.appendChild(item);
  }
}

let lastRecentProjects: RecentProject[] = [];

async function openRecentSheet(): Promise<void> {
  lastRecentProjects = await gui.recentProjectsList();
  renderRecentList(lastRecentProjects);
  $('recent-sheet').setAttribute('data-open', 'true');
}

// ---- debug toggle -----------------------------------------------------------
// Opens/closes the terminal as a closable popup (#terminal-sheet) instead of an inline box that
// permanently ate space in .leftcol - Stefan: "so it wont cover the screen". The toggle checkbox
// and the popup's own close button both funnel through here, so they can never drift out of sync
// with each other.
function toggleDebug(on: boolean): void {
  $('terminal-sheet').setAttribute('data-open', on ? 'true' : 'false');
  ($('debug-toggle') as HTMLInputElement).checked = on;
  if (on) {
    ensureTerminalMounted();
    requestAnimationFrame(refitTerminal);
  }
}

// ---- static labels ----------------------------------------------------------
function applyStrings(): void {
  // OS window title (taskbar/alt-tab tooltip) - was a static "DPS-NN — Genesis" in index.html,
  // never actually localized despite everything else being. Stefan: "translate ... including
  // the title of the app".
  document.title = `DPS-NN — ${t.title}`;
  // Settings button's own label IS the app title now (was t.toolbar.settings / "Settings") -
  // the titlebar's separate .title-text element was removed so the name doesn't appear twice.
  $('settings-label').textContent = t.title;
  // Native hover tooltip - was a static title="Settings" attribute in index.html, never
  // localized (Stefan: "the text when i hover the icon has dpn-nn in english even if my lang is
  // bg" - it wasn't even "DPS-NN", it was literally the word "Settings", always in English).
  $('settings-open').title = t.toolbar.settings;
  $('hq-badge').textContent = t.hq;
  $('debug-label').textContent = t.toolbar.debug;
  $('terminal-sheet-title').textContent = t.toolbar.debug;
  $('music-sheet-title').textContent = t.music.button;
  $('music-label').textContent = t.music.button;
  $('music-youtube-label').textContent = t.music.youtube;
  $('music-random-label').textContent = t.music.random;
  $('music-stop-label').textContent = t.music.stop;
  $('music-tab-songsAudio-label').textContent = t.music.songsTab;
  $('music-tab-concertsAudio-label').textContent = t.music.concertsAudioTab;
  $('music-tab-concertsVideo-label').textContent = t.music.concertsVideoTab;
  $('toolbar-reopen').textContent = t.toolbar.reopen;
  $('recent-label').textContent = t.toolbar.recent;
  $('recent-title').textContent = t.recent.title;
  $('spec-heading').textContent = t.spec.heading;
  $('seed-heading').textContent = t.seed.heading;
  $('spec-well').textContent = t.spec.none;
  $('seed-well').textContent = t.seed.none;
  $('spec-upload').textContent = t.spec.upload;
  ($('spec-describe') as HTMLTextAreaElement).placeholder = t.spec.describe;
  $('spec-create').textContent = t.spec.create;
  $('seed-upload').textContent = t.seed.upload;
  ($('seed-claude') as HTMLButtonElement).title = t.seed.claudeHint;
  $('run-start').textContent = t.run.start;
  $('run-pause').textContent = t.run.pause;
  $('run-resume').textContent = t.run.resume;
  $('run-stop').textContent = t.run.stop;
  ($('run-pause') as HTMLButtonElement).title = t.run.pauseHint;
  ($('run-resume') as HTMLButtonElement).title = t.run.resumeHint;
  $('progress-stage').textContent = t.progress.idle;
  $('progress-count').textContent = t.progress.of(0, TOTAL_STAGES);
  // dialog labels
  $('dup-title').textContent = t.dup.title;
  $('dup-lead').textContent = t.dup.lead;
  $('dup-l-project').textContent = t.dup.project;
  $('dup-l-last').textContent = t.dup.lastBuild;
  $('dup-l-out').textContent = t.dup.output;
  $('dup-open').textContent = t.dup.open;
  $('dup-regen').textContent = t.dup.regen;
  $('dup-cancel').textContent = t.dup.cancel;
  $('prank-title').textContent = t.prank.title;
  $('prank-body').textContent = t.prank.body;
  $('prank-ok').textContent = t.prank.close;
  $('report-run-app').textContent = t.report.runApp;
  $('report-kill-app').textContent = t.report.killApp;
  $('report-open-browser').textContent = t.report.openBrowser;
  $('report-open-folder').textContent = t.report.openFolder;
  $('report-new').textContent = t.report.close;
  $('error-l-stage').textContent = t.error.stage;
  $('error-retry').textContent = t.error.retry;
  $('error-new').textContent = t.report.close;
  // Settings sheet heading is the app title too now (was t.settings.title / "Settings") - same
  // move as the toolbar button's own label, one commit ago.
  $('settings-title').textContent = t.title;
  $('s-key-label').textContent = t.settings.apiKey;
  ($('s-key') as HTMLInputElement).placeholder = t.settings.apiKeyPlaceholder;
  $('s-out-label').textContent = t.settings.outDir;
  $('settings-save').textContent = t.settings.save;
  $('settings-cancel').textContent = t.settings.cancel;
}

// ---- language picker (phase 3b) ---------------------------------------------
// Data-driven off i18n/LANGUAGES: the picker is a collapsed flag button + a menu of all
// languages. Selecting one swaps `t` live, applies the SGA font-swap class, persists the
// choice, and re-renders every already-painted string via retranslate().
function buildLangPicker(): void {
  const picker = $('lang-picker');
  picker.innerHTML =
    `<button type="button" class="chip lang-toggle" id="lang-toggle" aria-haspopup="true" aria-expanded="false">` +
    `<span class="flag" id="lang-current-flag"></span><span id="lang-current-code"></span></button>` +
    `<div class="lang-menu" id="lang-menu" role="menu" hidden></div>`;
  const menu = $('lang-menu');
  let jokeDividerDrawn = false;
  for (const l of LANGUAGES) {
    if (l.joke && !jokeDividerDrawn) {
      const divider = document.createElement('div');
      divider.className = 'lang-divider';
      divider.setAttribute('role', 'separator');
      menu.appendChild(divider);
      jokeDividerDrawn = true;
    }
    const item = document.createElement('button');
    item.type = 'button';
    item.className = 'lang-item';
    item.setAttribute('role', 'menuitemradio');
    item.dataset.code = l.code;
    item.title = l.label;
    // flagSvg is a trusted static string (no user input); the name is set via textContent.
    const flag = document.createElement('span');
    flag.className = 'flag';
    flag.innerHTML = flagSvg(l.code);
    const name = document.createElement('span');
    name.className = 'lang-name';
    name.textContent = l.nativeLabel;
    item.append(flag, name);
    item.addEventListener('click', () => {
      void setLanguage(l.code);
      closeLangMenu();
    });
    menu.appendChild(item);
  }
  $('lang-toggle').addEventListener('click', (e) => {
    e.stopPropagation();
    toggleLangMenu();
  });
  menu.addEventListener('click', (e) => e.stopPropagation());
  document.addEventListener('click', closeLangMenu);
}

// Each picker's own toggle button calls e.stopPropagation(), so clicking picker B's toggle never
// bubbles to picker A's `document.addEventListener('click', closeA)` — A stayed open when B
// opened (Stefan's screenshot: theme menu still visible after clicking the music button). Every
// toggle now closes the other explicitly before opening its own. Music is its own popup dialog
// now (like Settings/Debug), not a click-outside dropdown, so it isn't part of this set.
function closeAllPickerMenus(except?: 'lang' | 'theme'): void {
  if (except !== 'lang') closeLangMenu();
  if (except !== 'theme') closeThemeMenu();
}

function toggleLangMenu(): void {
  const wasOpen = !$('lang-menu').hasAttribute('hidden');
  closeAllPickerMenus();
  if (!wasOpen) openLangMenu();
}
function openLangMenu(): void {
  $('lang-menu').removeAttribute('hidden');
  $('lang-toggle').setAttribute('aria-expanded', 'true');
}
function closeLangMenu(): void {
  $('lang-menu').setAttribute('hidden', '');
  $('lang-toggle').setAttribute('aria-expanded', 'false');
}

function updateLangPicker(): void {
  const meta = langMeta(currentLang);
  $('lang-current-flag').innerHTML = flagSvg(currentLang);
  $('lang-current-code').textContent = meta.short;
  ($('lang-toggle') as HTMLButtonElement).title = meta.label;
  document.querySelectorAll<HTMLElement>('#lang-menu .lang-item').forEach((el) => {
    const on = el.dataset.code === currentLang;
    el.setAttribute('aria-checked', on ? 'true' : 'false');
    el.classList.toggle('active', on);
  });
}

async function setLanguage(code: LangCode): Promise<void> {
  currentLang = code;
  t = stringsFor(code);
  // SGA is a font swap over the English strings (arch doc) — toggle the seam class; the actual
  // glyph font ships with the ENCHANT theme, so today this falls back to the normal faces.
  $('window').classList.toggle('lang-sga', code === 'sga');
  retranslate();
  try {
    await gui.settingsSet({ language: code });
  } catch {
    /* persistence is best-effort; the language still applied for this session */
  }
}

// Re-paint every already-rendered string in the new language without losing run/selection
// state. applyStrings() resets static labels + the idle progress/well defaults; we then
// restore the selection-driven wells, any live progress, the select placeholders, and an open
// result screen.
function retranslate(): void {
  applyStrings();
  onSpecChosen(selectedSpec);
  onSeedChosen(selectedSeed);
  if (runState !== 'idle' && lastProgress) {
    setProgress(lastProgress.index, lastProgress.total, lastProgress.stage, lastProgress.cls);
  }
  const specSel = $('spec-select') as HTMLSelectElement;
  const seedSel = $('seed-select') as HTMLSelectElement;
  if (specSel.options[0]) specSel.options[0].textContent = t.spec.pick;
  if (seedSel.options[0]) seedSel.options[0].textContent = t.seed.pick;
  if ($('report-screen').getAttribute('data-open') === 'true' && lastReport) showReport(lastReport);
  if ($('error-screen').getAttribute('data-open') === 'true' && lastError) showError(lastError.stage, lastError.message);
  if ($('recent-sheet').getAttribute('data-open') === 'true') renderRecentList(lastRecentProjects);
  updateLangPicker();
  updateThemePicker();
  updateMusicPickerVisibility();
}

// ---- theme picker (design-specs.md §5 "Theme switcher") ---------------------
// Mirrors the language picker's data-driven build/toggle pattern exactly (WALLPAPERS registry,
// same menu/backdrop-click-closes behavior). Selecting a wallpaper swaps [data-theme]/[data-skin]
// on .window — every component reads only CSS custom properties off those attributes
// (design-specs.md §2), so no other renderer code needs to know a wallpaper changed. Flat list of
// 10 clickable options (one per image) per Stefan's request — no in-menu descriptions, labels
// come from t.wallpaper so the picker translates like every other chrome string.
function buildThemePicker(): void {
  const picker = $('theme-picker');
  picker.innerHTML =
    `<button type="button" class="chip theme-toggle" id="theme-toggle" aria-haspopup="true" aria-expanded="false">` +
    `<span id="theme-current-label"></span></button>` +
    `<div class="theme-menu" id="theme-menu" role="menu" hidden></div>`;
  const menu = $('theme-menu');
  for (const w of WALLPAPERS) {
    const item = document.createElement('button');
    item.type = 'button';
    item.className = 'theme-item';
    item.setAttribute('role', 'menuitemradio');
    item.dataset.id = w.id;
    item.addEventListener('click', () => {
      void setWallpaper(w);
      closeThemeMenu();
    });
    menu.appendChild(item);
  }
  $('theme-toggle').addEventListener('click', (e) => {
    e.stopPropagation();
    toggleThemeMenu();
  });
  menu.addEventListener('click', (e) => e.stopPropagation());
  document.addEventListener('click', closeThemeMenu);
}

function toggleThemeMenu(): void {
  const wasOpen = !$('theme-menu').hasAttribute('hidden');
  closeAllPickerMenus();
  if (!wasOpen) openThemeMenu();
}
function openThemeMenu(): void {
  $('theme-menu').removeAttribute('hidden');
  $('theme-toggle').setAttribute('aria-expanded', 'true');
}
function closeThemeMenu(): void {
  $('theme-menu').setAttribute('hidden', '');
  $('theme-toggle').setAttribute('aria-expanded', 'false');
}

function updateThemePicker(): void {
  // The dropdown TOGGLE button shows the generic "Theme" label now (was the current wallpaper's
  // own name, e.g. "Das Deutsche Herz") - Stefan. The active wallpaper's real name still shows
  // as the button's title tooltip AND as the highlighted/active item inside the open menu below.
  const currentLabel = t.wallpaper[currentWallpaper.labelKey];
  $('theme-current-label').textContent = t.wallpaper.title;
  ($('theme-toggle') as HTMLButtonElement).title = currentLabel;
  document.querySelectorAll<HTMLElement>('#theme-menu .theme-item').forEach((el) => {
    const w = WALLPAPERS.find((x) => x.id === el.dataset.id)!;
    el.textContent = t.wallpaper[w.labelKey];
    const on = w.id === currentWallpaper.id;
    el.setAttribute('aria-checked', on ? 'true' : 'false');
    el.classList.toggle('active', on);
  });
}

async function setWallpaper(w: WallpaperMeta): Promise<void> {
  currentWallpaper = w;
  const win = $('window');
  win.setAttribute('data-theme', w.theme);
  if (w.skin) win.setAttribute('data-skin', w.skin);
  else win.removeAttribute('data-skin');
  updateThemePicker();
  updateMusicPickerVisibility();
  applyFooterVisibility();
  try {
    await gui.settingsSet({ wallpaper: w.id });
  } catch {
    /* persistence is best-effort; the wallpaper still applied for this session */
  }
}

// Footer text is emptied on IMPERATOR (Stefan: he wants to see the bottom of the Veselin Marinov
// art, not have it covered by footer text) — but the .footer ELEMENT and its reserved space stay
// on every theme, so the content area's total height never shifts when switching wallpapers.
let lastFooterText = '';
function applyFooterVisibility(): void {
  $('footer').textContent = currentWallpaper.theme === 'imperator' ? '' : lastFooterText;
}

// ---- "Play Veselin Marinov" (IMPERATOR only) ---------------------------------
// A closable popup (#music-sheet), same pattern as the debug terminal (Stefan: "make a popup
// like the settings or the terminal for marinov songs") instead of a toolbar dropdown menu -
// dropdowns close on outside click, which the old menu never wired up (Stefan: "the dropdosn
// never close"), and a real dialog also gives room to show the video full-size when expanded.
const VESELIN_MARINOV_YOUTUBE_URL = 'https://www.youtube.com/watch?v=qcjyeG2FgXs&list=PLF0F4B6557CF21926';
const MUSIC_CATEGORIES: MusicCategory[] = ['songsAudio', 'concertsAudio', 'concertsVideo'];
let activeMusicCategory: MusicCategory = 'songsAudio';

function updateMusicPickerVisibility(): void {
  $('music-picker').toggleAttribute('hidden', currentWallpaper.theme !== 'imperator');
}

function toggleMusicSheet(): void {
  const open = $('music-sheet').getAttribute('data-open') === 'true';
  if (open) closeMusicSheet();
  else openMusicSheet();
}
function openMusicSheet(): void {
  $('music-sheet').setAttribute('data-open', 'true');
  void selectMusicCategory(activeMusicCategory);
}
function closeMusicSheet(): void {
  $('music-sheet').setAttribute('data-open', 'false');
}

function formatTime(seconds: number): string {
  if (!Number.isFinite(seconds)) return '0:00';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${String(s).padStart(2, '0')}`;
}

// The seek/timeupdate/loadedmetadata listeners are bound ONCE (not per-track) - the previous
// version called bindSeekBar() fresh on every playLocalFile(), which ADDED a new set of
// listeners on top of the old ones every time without ever removing them (both <audio>/<video>
// are the SAME persistent DOM elements across tracks). After playing 2+ tracks in one session,
// multiple stacked 'input' handlers all fought to set currentTime at once - exactly the kind of
// bug that makes a seek bar "jump back to 0:00" instead of landing where you clicked (Stefan's
// report). activeMedia tracks which of the two elements is currently live; both media elements'
// events are listened to unconditionally, but only acted on when they match activeMedia.
let activeMedia: HTMLMediaElement | null = null;

function wireMediaPlayback(): void {
  const seek = $('music-seek') as HTMLInputElement;
  const cur = $('music-time-current');
  const total = $('music-time-total');
  const audio = $('music-player') as HTMLAudioElement;
  const video = $('music-video') as HTMLVideoElement;
  const playPause = $('music-playpause') as HTMLButtonElement;

  for (const media of [audio, video]) {
    media.addEventListener('loadedmetadata', () => {
      if (media !== activeMedia) return;
      seek.max = String(media.duration || 0);
      total.textContent = formatTime(media.duration);
    });
    media.addEventListener('timeupdate', () => {
      if (media !== activeMedia) return;
      if (!seek.matches(':active')) seek.value = String(media.currentTime);
      cur.textContent = formatTime(media.currentTime);
    });
    media.addEventListener('play', () => {
      if (media !== activeMedia) return;
      playPause.textContent = '❚❚';
      playPause.setAttribute('aria-label', 'Pause');
    });
    media.addEventListener('pause', () => {
      if (media !== activeMedia) return;
      playPause.textContent = '▶';
      playPause.setAttribute('aria-label', 'Resume');
    });
  }
  seek.addEventListener('input', () => {
    if (activeMedia) activeMedia.currentTime = Number(seek.value);
  });
  playPause.addEventListener('click', () => {
    if (!activeMedia) return;
    if (activeMedia.paused) void activeMedia.play();
    else activeMedia.pause();
  });
}

// Routed through the app-media:// custom protocol (registered in main/index.ts) instead of a
// raw file:// URL — file:// media only loads in production, where the renderer itself is served
// from file:// (same-origin); in dev the renderer is http://localhost:5173 and Chromium blocks
// file:// media as cross-origin ("no supported source was found" regardless of path-encoding).
// app-media: is registered secure+cors-enabled so it loads from either origin.
function toMediaUrl(path: string): string {
  return `app-media://play?path=${encodeURIComponent(path)}`;
}

function playLocalFile(path: string, label: string): void {
  const isVideo = /\.(mp4|webm|mov)$/i.test(path);
  const audio = $('music-player') as HTMLAudioElement;
  const video = $('music-video') as HTMLVideoElement;
  const url = toMediaUrl(path);
  $('music-player-bar').hidden = false;
  if (isVideo) {
    audio.pause();
    audio.removeAttribute('src');
    video.hidden = false;
    video.src = url;
    activeMedia = video;
    void video.play().catch((e: Error) => toast('danger', t.toast.couldNotPlayTrack, e.message));
  } else {
    video.pause();
    video.removeAttribute('src');
    video.hidden = true;
    audio.src = url;
    activeMedia = audio;
    void audio.play().catch((e: Error) => toast('danger', t.toast.couldNotPlayTrack, e.message));
  }
  $('music-now-playing').textContent = label;
  markActiveTrackItem(label);
}

function markActiveTrackItem(label: string): void {
  $('music-track-list')
    .querySelectorAll<HTMLButtonElement>('.music-item')
    .forEach((item) => item.classList.toggle('active', item.textContent === label));
}

function stopMusic(): void {
  const audio = $('music-player') as HTMLAudioElement;
  const video = $('music-video') as HTMLVideoElement;
  for (const media of [audio, video]) {
    media.pause();
    media.removeAttribute('src');
    media.load();
  }
  activeMedia = null;
  video.hidden = true;
  $('music-player-bar').hidden = true;
  $('music-now-playing').textContent = '';
  markActiveTrackItem('');
}

async function playRandomLocalTrack(category: MusicCategory): Promise<void> {
  const path = await gui.musicRandomTrack(category);
  if (!path) {
    toast('warning', t.toast.noLocalTracks, t.toast.noLocalTracksHint);
    return;
  }
  const label = path.replace(/^.*[/\\]/, '');
  playLocalFile(path, label);
}

async function selectMusicCategory(category: MusicCategory): Promise<void> {
  activeMusicCategory = category;
  $('music-tabs')
    .querySelectorAll<HTMLButtonElement>('.music-tab')
    .forEach((tab) => tab.classList.toggle('active', tab.id === `music-tab-${category}`));
  $('music-youtube').hidden = category !== 'songsAudio';

  const list = $('music-track-list');
  list.innerHTML = '';
  const files = await gui.musicList(category);
  if (files.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'music-empty';
    empty.textContent = t.music.noTracks;
    list.appendChild(empty);
    return;
  }
  // Built in a DocumentFragment and appended once (one paint pass instead of 80 reflows). The
  // per-row text-clipping bug was NOT here though - it was `.music-item` shrinking vertically as
  // a flex-column child with overflow:hidden; see the .music-item rule in styles.css.
  const fragment = document.createDocumentFragment();
  for (const file of files) {
    const item = document.createElement('button');
    item.type = 'button';
    item.className = 'music-item';
    item.textContent = file;
    item.title = file;
    item.addEventListener('click', async () => {
      const dir = (await gui.musicRandomTrack(category))?.replace(/[^/\\]+$/, '') ?? '';
      playLocalFile(dir + file, file);
    });
    fragment.appendChild(item);
  }
  list.appendChild(fragment);
}

function wireMusicSheet(): void {
  wireMediaPlayback();
  $('music-toggle').addEventListener('click', () => toggleMusicSheet());
  $('music-close').addEventListener('click', () => closeMusicSheet());
  $('music-maximize').addEventListener('click', () => {
    const maximized = $('music-dialog').classList.toggle('maximized');
    ($('music-maximize') as HTMLButtonElement).setAttribute('aria-label', maximized ? 'Restore' : 'Maximize');
    $('music-maximize').textContent = maximized ? '❐' : '▢';
  });
  $('music-youtube').addEventListener('click', () => {
    void gui.openExternal(VESELIN_MARINOV_YOUTUBE_URL);
  });
  $('music-random').addEventListener('click', () => void playRandomLocalTrack(activeMusicCategory));
  for (const category of MUSIC_CATEGORIES) {
    $(`music-tab-${category}`).addEventListener('click', () => void selectMusicCategory(category));
  }
  $('music-stop').addEventListener('click', () => stopMusic());
  updateMusicPickerVisibility();
}

// ---- file upload (renderer reads bytes, main validates + stages) ------------
async function uploadFile(kind: 'spec' | 'seed'): Promise<void> {
  const input = document.createElement('input');
  input.type = 'file';
  input.accept = 'application/json,.json';
  input.addEventListener('change', async () => {
    const file = input.files?.[0];
    if (!file) return;
    const bytes = new Uint8Array(await file.arrayBuffer());
    try {
      const handle =
        kind === 'spec' ? await gui.specUpload(bytes, file.name) : await gui.seedUpload(bytes, file.name);
      await reloadLists();
      if (kind === 'spec') {
        specs.push(handle);
        onSpecChosen(handle);
      } else {
        seeds.push(handle);
        onSeedChosen(handle);
      }
      toast(
        'success',
        kind === 'spec' ? t.toast.specUploaded : t.toast.seedUploaded,
        `${handle.name} (${formatBytes(handle.size)})`,
      );
    } catch (e) {
      toast('danger', t.toast.uploadFailed, (e as Error).message);
    }
  });
  input.click();
}

// ---- wiring -----------------------------------------------------------------
function wire(): void {
  // window chrome
  $('win-min').addEventListener('click', () => gui.windowMinimize());
  $('win-max').addEventListener('click', () => gui.windowMaximize());
  $('win-close').addEventListener('click', () => gui.windowClose());

  // selection
  ($('spec-select') as HTMLSelectElement).addEventListener('change', (e) => {
    const v = (e.target as HTMLSelectElement).value;
    onSpecChosen(v === '' ? null : specs[Number(v)]);
  });
  ($('seed-select') as HTMLSelectElement).addEventListener('change', (e) => {
    const v = (e.target as HTMLSelectElement).value;
    onSeedChosen(v === '' ? null : seeds[Number(v)]);
  });
  $('spec-upload').addEventListener('click', () => void uploadFile('spec'));
  $('spec-create').addEventListener('click', () => void doCreate());
  $('seed-upload').addEventListener('click', () => void uploadFile('seed'));

  // run controls
  $('run-start').addEventListener('click', () => void attemptStart());
  $('run-pause').addEventListener('click', () => void doPause());
  $('run-resume').addEventListener('click', () => void doResume());
  $('run-stop').addEventListener('click', () => void doStop());

  // debug
  ($('debug-toggle') as HTMLInputElement).addEventListener('change', (e) =>
    toggleDebug((e.target as HTMLInputElement).checked),
  );

  // duplicate dialog
  $('dup-open').addEventListener('click', () => {
    closeDuplicateDialog();
    if (pendingDup) void gui.openPath(pendingDup.outDir);
  });
  $('dup-regen').addEventListener('click', () => {
    closeDuplicateDialog();
    void doStart();
  });
  $('dup-cancel').addEventListener('click', closeDuplicateDialog);
  $('dup-close').addEventListener('click', closeDuplicateDialog);

  // prank popup
  $('prank-ok').addEventListener('click', closePrankDialog);
  $('prank-close').addEventListener('click', closePrankDialog);

  // report/error actions
  $('report-run-app').addEventListener('click', () => {
    if (!lastReport?.outDir) return;
    gui
      .runApp(lastReport.outDir)
      .then(() => {
        appLaunchedForLastReport = true;
        ($('report-kill-app') as HTMLButtonElement).disabled = false;
      })
      .catch((e: Error) => toast('danger', t.toast.couldNotStartApp, e.message));
  });
  $('report-kill-app').addEventListener('click', () => {
    if (!lastReport?.outDir) return;
    void gui.killApp(lastReport.outDir);
    appLaunchedForLastReport = false;
    ($('report-kill-app') as HTMLButtonElement).disabled = true;
  });
  $('report-open-browser').addEventListener('click', () => {
    if (lastReport?.url) void gui.openExternal(lastReport.url);
  });
  $('report-open-folder').addEventListener('click', () => {
    if (lastReport?.outDir) void gui.openPath(lastReport.outDir);
  });
  const closeAndReset = () => {
    closeResultScreens();
    setRunState('idle');
  };
  $('report-new').addEventListener('click', closeAndReset);
  $('report-close').addEventListener('click', closeAndReset);
  $('error-new').addEventListener('click', closeAndReset);
  $('error-close').addEventListener('click', closeAndReset);
  $('toolbar-reopen').addEventListener('click', reopenLastResult);

  // recent projects
  $('recent-open').addEventListener('click', () => void openRecentSheet());
  $('recent-close').addEventListener('click', () => $('recent-sheet').setAttribute('data-open', 'false'));

  // debug terminal popup
  $('terminal-close').addEventListener('click', () => toggleDebug(false));
  $('terminal-maximize').addEventListener('click', () => {
    const maximized = $('terminal-dialog').classList.toggle('maximized');
    ($('terminal-maximize') as HTMLButtonElement).setAttribute('aria-label', maximized ? 'Restore' : 'Maximize');
    $('terminal-maximize').textContent = maximized ? '❐' : '▢';
    requestAnimationFrame(refitTerminal);
  });

  // settings
  $('settings-open').addEventListener('click', () => void openSettings());
  $('settings-close').addEventListener('click', () => $('settings-sheet').setAttribute('data-open', 'false'));
  $('settings-cancel').addEventListener('click', () => $('settings-sheet').setAttribute('data-open', 'false'));
  $('settings-save').addEventListener('click', () => void saveSettings());

  // keyboard: Esc closes overlays; Enter in dup dialog = quiet default (open existing)
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      closeDuplicateDialog();
      closeResultScreens();
      closeLangMenu();
      closeThemeMenu();
      $('settings-sheet').setAttribute('data-open', 'false');
      $('recent-sheet').setAttribute('data-open', 'false');
      if ($('terminal-sheet').getAttribute('data-open') === 'true') toggleDebug(false);
      if ($('music-sheet').getAttribute('data-open') === 'true') closeMusicSheet();
      closePrankDialog();
    }
    if (e.key === 'Enter' && $('dup-dialog').getAttribute('data-open') === 'true') {
      $('dup-open').click();
    }
  });

  window.addEventListener('resize', refitTerminal);
}

// ---- IPC event subscriptions ------------------------------------------------
function subscribe(): void {
  gui.onLog((line) => {
    ensureTerminalMounted();
    term.writeln(line);
  });
  gui.onProgress((e) => {
    setProgress(e.index, e.total, String(e.stage), e.phase === 'error' ? 'error' : 'running');
    if (e.phase === 'end') toast('success', t.toast.stageDone(e.index, e.total), String(e.stage));
  });
  gui.onAwaitingLlm((e) => {
    setRunState('awaiting-llm');
    toast('warning', t.toast.awaitingLlm, e.stage);
  });
  gui.onDone((report) => {
    setProgress(TOTAL_STAGES, TOTAL_STAGES, 'report', 'success');
    setRunState('done');
    toast('success', t.toast.done);
    showReport(report);
  });
  gui.onError((e) => {
    setProgress(Number($('progress-count').textContent!.split('/')[0]) || 0, TOTAL_STAGES, String(e.stage ?? 'error'), 'error');
    setRunState('error');
    toast('danger', t.toast.failed, e.stage ?? undefined);
    showError(e.stage ? String(e.stage) : null, e.message);
  });
}

// ---- ambient starfield (z.canvas motion; respects reduced-motion) -----------
function starfield(): void {
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const win = $('window');
  const canvas = $('stars') as HTMLCanvasElement;
  const ctx = canvas.getContext('2d');
  if (!ctx) return;
  const dpr = Math.min(window.devicePixelRatio || 1, 2);
  let particles: { x: number; y: number; r: number; s: number }[] = [];
  function resize(): void {
    const r = win.getBoundingClientRect();
    canvas.width = r.width * dpr;
    canvas.height = r.height * dpr;
    canvas.style.width = `${r.width}px`;
    canvas.style.height = `${r.height}px`;
    if (!particles.length) {
      for (let i = 0; i < 80; i++) {
        particles.push({
          x: Math.random() * canvas.width,
          y: Math.random() * canvas.height,
          r: Math.random() * 1.4 * dpr + 0.3,
          s: (Math.random() * 0.05 + 0.01) * dpr,
        });
      }
    }
  }
  window.addEventListener('resize', resize);
  resize();
  function frame(): void {
    const accent = getComputedStyle(win).getPropertyValue('--accent').trim() || '#3fd0ff';
    ctx!.clearRect(0, 0, canvas.width, canvas.height);
    ctx!.fillStyle = accent;
    for (const p of particles) {
      ctx!.globalAlpha = 0.5;
      ctx!.beginPath();
      ctx!.arc(p.x, p.y, p.r, 0, Math.PI * 2);
      ctx!.fill();
      if (!reduce) {
        p.y += p.s;
        if (p.y > canvas.height) {
          p.y = 0;
          p.x = Math.random() * canvas.width;
        }
      }
    }
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
}

// ---- boot -------------------------------------------------------------------
async function boot(): Promise<void> {
  buildLangPicker();
  buildThemePicker();
  // Restore the persisted GUI language + wallpaper BEFORE building the music menu — it was
  // previously built first, so updateMusicPickerVisibility() ran against the default NOVA
  // wallpaper and hid the button even when the persisted choice was IMPERATOR (real bug: the
  // button never showed on a restored VM wallpaper, only after manually re-picking it).
  let language: string | undefined;
  let wallpaper: string | undefined;
  try {
    const s = await gui.settingsGet();
    language = s.language;
    wallpaper = s.wallpaper;
  } catch {
    /* settings unavailable — fall back to EN / the default wallpaper */
  }
  currentLang = normalizeLang(language);
  t = stringsFor(currentLang);
  currentWallpaper = normalizeWallpaper(wallpaper);
  $('window').classList.toggle('lang-sga', currentLang === 'sga');
  $('window').setAttribute('data-theme', currentWallpaper.theme);
  if (currentWallpaper.skin) $('window').setAttribute('data-skin', currentWallpaper.skin);
  applyStrings();
  updateLangPicker();
  updateThemePicker();
  wireMusicSheet();
  wire();
  subscribe();
  starfield();
  setRunState('idle');
  await reloadLists();
  preselectSample();
  lastFooterText = await gui.footerGet();
  applyFooterVisibility();
  // Boot splash (index.html #boot-splash, inline-styled so it paints before this file even
  // finishes loading) comes down here, as the LAST step - once everything above has actually
  // run, not on a timer/guess.
  document.getElementById('boot-splash')?.remove();
}

void boot();
