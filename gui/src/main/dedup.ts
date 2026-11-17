import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import type { DuplicateMatch, FileHandle } from '@shared/ipc-types';
import { workspaceRoot } from './paths';

// Dedup is entirely GUI-side and does NOT touch the headless CLI (ADR-010). It hashes
// the (spec, seed) file contents and looks the pair up in a GUI-owned manifest. This
// reuses the *idea* of the CLI's logs/registry.json without depending on or extending
// the CLI's own formats.

interface ManifestEntry {
  project: string;
  outDir: string;
  ts: string;
  status: 'success' | 'failed';
}

type Manifest = Record<string, ManifestEntry>;

function manifestPath(): string {
  const dir = join(workspaceRoot(), 'logs');
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
  return join(dir, 'gui-build-manifest.json');
}

function loadManifest(): Manifest {
  const path = manifestPath();
  if (!existsSync(path)) return {};
  try {
    return JSON.parse(readFileSync(path, 'utf8')) as Manifest;
  } catch {
    return {};
  }
}

function sha256(bytes: Buffer): string {
  return createHash('sha256').update(bytes).digest('hex');
}

/** The stable key for a (spec, seed) pair. A null seed hashes as an empty buffer. */
export function pairHash(spec: FileHandle, seed: FileHandle | null): string {
  const specBytes = readFileSync(spec.path);
  const seedBytes = seed ? readFileSync(seed.path) : Buffer.alloc(0);
  return `${sha256(specBytes)}:${sha256(seedBytes)}`;
}

/** A prior *successful* build for this exact (spec, seed) pair, or null. */
export function checkDuplicate(spec: FileHandle, seed: FileHandle | null): DuplicateMatch | null {
  const entry = loadManifest()[pairHash(spec, seed)];
  if (!entry || entry.status !== 'success') return null;
  return { project: entry.project, ts: entry.ts, outDir: entry.outDir };
}

/** Record a completed build so future identical (spec, seed) pairs are detected. */
export function recordBuild(
  spec: FileHandle,
  seed: FileHandle | null,
  entry: ManifestEntry,
): void {
  const manifest = loadManifest();
  manifest[pairHash(spec, seed)] = entry;
  writeFileSync(manifestPath(), JSON.stringify(manifest, null, 2), 'utf8');
}
