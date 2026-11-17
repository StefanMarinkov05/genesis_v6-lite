import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { RecentProject } from '@shared/ipc-types';
import { workspaceRoot } from './paths.ts';
import { parseRegistrySnapshot } from './registry-parser.ts';

// Reads the CLI's own logs/registry.json (Program.cs "RecordProjectRegistry") — the GUI never
// writes this file, only the headless CLI does, one snapshot entry per project name keyed to
// its LATEST run. Read-only, best-effort: a missing/malformed registry means "no recent
// projects" rather than a thrown error, matching the CLI's own "registry is best-effort" stance.

function registryPath(): string {
  return join(workspaceRoot(), 'logs', 'registry.json');
}

/** All recorded projects, newest first. Empty array if the registry doesn't exist or is malformed. */
export function listRecentProjects(): RecentProject[] {
  const path = registryPath();
  if (!existsSync(path)) return [];
  return parseRegistrySnapshot(readFileSync(path, 'utf8'));
}
