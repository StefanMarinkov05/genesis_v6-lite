import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { app } from 'electron';
import type { Settings, SettingsPatch } from '@shared/ipc-types';
import { bundledCli, workspaceRoot } from './paths';

// Settings live in Electron's per-user userData dir, NOT in the repo — so an API key is
// never written anywhere git tracks. The key is the sensitive bit: it is stored here,
// injected into the CLI child's env by genesis-runner, and NEVER returned to the
// renderer (settings:get reports only apiKeySet: boolean). See the security model in
// docs/explanation/gui-architecture.md.

interface StoredSettings {
  wallpaper: string;
  language: string;
  genesisCommand: string;
  outDir: string;
  /** Persisted key; may be empty when the user relies on the ANTHROPIC_API_KEY env var instead. */
  apiKey: string;
}

function defaults(): StoredSettings {
  return {
    wallpaper: 'nova-astro-hud',
    language: 'en',
    genesisCommand: defaultGenesisCommand(),
    outDir: 'out',
    apiKey: '',
  };
}

/** ADR-010 decision #1: the dev CLI form the batch scripts use; a packaged build runs its bundled
 *  self-contained dps-nn.exe instead (ADR-014). Quoted: the install path may contain spaces. */
const DEV_COMMAND = 'dotnet run --project src/Genesis.Cli --';
function defaultGenesisCommand(): string {
  const exe = bundledCli();
  return exe ? `"${exe}"` : DEV_COMMAND;
}

function settingsPath(): string {
  const dir = app.getPath('userData');
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
  return join(dir, 'settings.json');
}

function loadStored(): StoredSettings {
  const path = settingsPath();
  if (!existsSync(path)) return defaults();
  try {
    return { ...defaults(), ...JSON.parse(readFileSync(path, 'utf8')) };
  } catch {
    return defaults();
  }
}

function saveStored(s: StoredSettings): void {
  writeFileSync(settingsPath(), JSON.stringify(s, null, 2), 'utf8');
}

/** True iff a key is available at all — persisted OR from the process env. */
function keyIsSet(stored: StoredSettings): boolean {
  return Boolean(stored.apiKey || process.env.ANTHROPIC_API_KEY);
}

/** The renderer-facing view: never includes the key value. */
function toPublic(stored: StoredSettings): Settings {
  return {
    wallpaper: stored.wallpaper,
    language: stored.language,
    genesisCommand: stored.genesisCommand,
    outDir: stored.outDir,
    apiKeySet: keyIsSet(stored),
  };
}

export function getSettings(): Settings {
  return toPublic(loadStored());
}

export function setSettings(patch: SettingsPatch): Settings {
  const stored = loadStored();
  if (patch.wallpaper !== undefined) stored.wallpaper = patch.wallpaper;
  if (patch.language !== undefined) stored.language = patch.language;
  if (patch.genesisCommand !== undefined) stored.genesisCommand = patch.genesisCommand;
  if (patch.outDir !== undefined) stored.outDir = patch.outDir;
  // apiKey is write-only: accept and persist, never echo back.
  if (patch.apiKey !== undefined) stored.apiKey = patch.apiKey;
  saveStored(stored);
  return toPublic(stored);
}

/** The effective API key for the child env: persisted value wins, else the ambient env var. */
export function effectiveApiKey(): string | undefined {
  const stored = loadStored();
  return stored.apiKey || process.env.ANTHROPIC_API_KEY || undefined;
}

/** Absolute output directory the CLI writes generated projects into (--out). */
export function effectiveOutDir(): string {
  const out = loadStored().outDir || 'out';
  // Relative outDir resolves under the workspace, the CLI's cwd (same as the CLI's default "out").
  return out.match(/^([a-zA-Z]:[\\/]|[\\/])/) ? out : join(workspaceRoot(), out);
}

/** The configured CLI invocation, split into a program + base args (naive shell split). */
export function effectiveGenesisCommand(): { program: string; baseArgs: string[] } {
  let cmd = loadStored().genesisCommand.trim() || defaultGenesisCommand();
  // A packaged build has no src/ to `dotnet run`; a dev-form command can only be stale here.
  if (bundledCli() && cmd === DEV_COMMAND) cmd = defaultGenesisCommand();
  const parts = cmd.match(/(?:[^\s"]+|"[^"]*")+/g)?.map((p) => p.replace(/^"|"$/g, '')) ?? [];
  return { program: parts[0] ?? 'dotnet', baseArgs: parts.slice(1) };
}
