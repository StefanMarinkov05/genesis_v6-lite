import type { RecentProject } from '@shared/ipc-types';

// Pure parsing logic for the CLI's own logs/registry.json (Program.cs "RecordProjectRegistry").
// Deliberately has ZERO import of './paths' (which pulls in Electron's `app`) so this file is
// directly unit-testable under plain `node --test` — same reasoning as report.ts staying
// Electron-free. recent-projects.ts is the thin filesystem/workspaceRoot()-dependent wrapper.

interface RegistryRecord {
  ts: string;
  project: string;
  specPath: string;
  seedResponsePath: string | null;
  outDir: string;
}

/** Raw registry.json text -> recent projects, newest first. Empty array on malformed JSON. */
export function parseRegistrySnapshot(raw: string): RecentProject[] {
  let snapshot: Record<string, RegistryRecord>;
  try {
    snapshot = JSON.parse(raw) as Record<string, RegistryRecord>;
  } catch {
    return [];
  }
  return Object.values(snapshot)
    .map((r) => ({ project: r.project, ts: r.ts, specPath: r.specPath, outDir: r.outDir }))
    .sort((a, b) => new Date(b.ts).getTime() - new Date(a.ts).getTime());
}
