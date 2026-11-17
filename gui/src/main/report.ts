import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { BuildReport } from '@shared/ipc-types';

// The completion readout is parsed from the banner the CLI prints on a green run
// (Program.cs PrintCompletionBanner) — the single decoupled source that already carries
// URL, seeded credentials, DB name, and the start command. Passwords are NOT in the raw
// spec (roles are $variation placeholders resolved downstream), so parsing the emitted
// banner is both simpler and more correct than re-deriving them. URL/DB fall back to the
// generated project's own files when the banner is incomplete.

/**
 * Parse the ДПС-НН completion banner out of captured stdout.
 *
 * Banner shape (box-drawn):
 *   ║  {ProjectName}                    ║
 *   ║  🔗 {url}                         ║
 *   ╠══════════════════════════════════╣
 *   ║  {Role}          {email} / {pwd}  ║   (one per seeded user)
 *   ╚══════════════════════════════════╝
 *   Database: {dbName}
 *   Start: dotnet run --project "{path}"
 */
export function parseCompletionBanner(stdout: string, projectDir?: string): BuildReport {
  const lines = stdout.split(/\r?\n/);
  const report: BuildReport = {
    url: null,
    credentials: [],
    dbName: null,
    startCommand: null,
    outDir: projectDir ?? null,
    summaryPath: null,
  };

  let inCredBlock = false;
  for (const raw of lines) {
    const line = raw.trim();

    if (line.includes('🔗')) {
      const m = line.match(/https?:\/\/\S+/);
      if (m) report.url = m[0].replace(/[║╗╝]+$/, '').trim();
    }

    // The credential rows sit between the ╠ separator and the ╚ bottom border.
    if (line.startsWith('╠')) inCredBlock = true;
    else if (line.startsWith('╚')) inCredBlock = false;
    else if (inCredBlock && line.startsWith('║')) {
      const body = line.replace(/^║/, '').replace(/║$/, '').trim();
      const slash = body.indexOf(' / ');
      if (slash > -1) {
        const left = body.slice(0, slash).trim();
        const password = body.slice(slash + 3).trim();
        const parts = left.split(/\s+/);
        const email = parts[parts.length - 1];
        const role = left.slice(0, left.length - email.length).trim() || '—';
        if (email && password) report.credentials.push({ role, email, password });
      }
    }

    if (line.startsWith('Database:')) report.dbName = line.slice('Database:'.length).trim();
    if (line.startsWith('Start:')) report.startCommand = line.slice('Start:'.length).trim();
  }

  // File fallbacks — only fill gaps the banner left.
  if (projectDir && existsSync(projectDir)) {
    if (!report.url) report.url = urlFromLaunchSettings(projectDir);
    if (!report.dbName) report.dbName = dbFromAppSettings(projectDir);
  }

  return report;
}

function urlFromLaunchSettings(projectDir: string): string | null {
  const path = join(projectDir, 'Properties', 'launchSettings.json');
  if (!existsSync(path)) return null;
  try {
    const doc = JSON.parse(readFileSync(path, 'utf8'));
    for (const profile of Object.values<any>(doc.profiles ?? {})) {
      const au: string | undefined = profile?.applicationUrl;
      if (au) return au.split(';').find((u: string) => u.startsWith('http://')) ?? au;
    }
  } catch {
    /* best-effort */
  }
  return null;
}

function dbFromAppSettings(projectDir: string): string | null {
  const path = join(projectDir, 'appsettings.json');
  if (!existsSync(path)) return null;
  try {
    const doc = JSON.parse(readFileSync(path, 'utf8'));
    const cs = doc.ConnectionStrings ?? {};
    for (const val of Object.values<any>(cs)) {
      const m = String(val).match(/Database=([^;]+)/);
      if (m) return m[1];
    }
  } catch {
    /* best-effort */
  }
  return null;
}
