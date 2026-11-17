import { existsSync, mkdirSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import type { FileHandle } from '@shared/ipc-types';
import { seedSearchDirs, specSearchDirs, uploadsDir } from './paths';

// Discovery mirrors the CLI's own interactive pickers (Program.cs ResolveSpecInteractively
// / ResolveSeedInteractively) so the GUI list-select offers exactly what the CLI would:
// project-spec*.json (minus .lock.json) + archived *-schema.json for specs; *.json for
// seeds. Uploads are validated as JSON (specs additionally need meta.projectName) and
// staged under gui/.uploads before being handed to a run.

function listDir(dir: string, match: (name: string) => boolean): string[] {
  if (!existsSync(dir)) return [];
  try {
    return readdirSync(dir)
      .filter(match)
      .map((n) => join(dir, n));
  } catch {
    return [];
  }
}

export function listSpecs(): FileHandle[] {
  const out: FileHandle[] = [];
  const seen = new Set<string>();
  for (const { dir, source } of specSearchDirs()) {
    const specs = listDir(
      dir,
      (n) =>
        (n.startsWith('project-spec') && n.endsWith('.json') && !n.endsWith('.lock.json')) ||
        n.endsWith('-schema.json'),
    );
    for (const path of specs) {
      if (seen.has(path)) continue;
      seen.add(path);
      out.push({
        path,
        name: basename(path),
        source: source === 'cwd' ? 'cwd' : source,
        size: statSync(path).size,
      });
    }
  }
  return out.sort((a, b) => a.name.localeCompare(b.name));
}

export function listSeeds(): FileHandle[] {
  const out: FileHandle[] = [];
  const seen = new Set<string>();
  for (const { dir, source } of seedSearchDirs()) {
    for (const path of listDir(dir, (n) => n.endsWith('.json'))) {
      if (seen.has(path)) continue;
      seen.add(path);
      out.push({ path, name: basename(path), source, size: statSync(path).size });
    }
  }
  return out.sort((a, b) => a.name.localeCompare(b.name));
}

function stage(bytes: Uint8Array, name: string, kind: 'spec' | 'seed'): FileHandle {
  const text = Buffer.from(bytes).toString('utf8');
  let doc: unknown;
  try {
    doc = JSON.parse(text);
  } catch (e) {
    throw new Error(`Uploaded ${kind} is not valid JSON: ${(e as Error).message}`);
  }
  if (kind === 'spec') {
    const projectName = (doc as { meta?: { projectName?: unknown } })?.meta?.projectName;
    if (typeof projectName !== 'string' || !projectName) {
      throw new Error('Uploaded spec is missing meta.projectName.');
    }
  }
  const dir = uploadsDir();
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
  const safe = basename(name).replace(/[^\w.\-]+/g, '_');
  const path = join(dir, safe);
  writeFileSync(path, text, 'utf8');
  return { path, name: safe, source: 'uploaded', size: statSync(path).size };
}

export function uploadSpec(bytes: Uint8Array, name: string): FileHandle {
  return stage(bytes, name, 'spec');
}

export function uploadSeed(bytes: Uint8Array, name: string): FileHandle {
  return stage(bytes, name, 'seed');
}
