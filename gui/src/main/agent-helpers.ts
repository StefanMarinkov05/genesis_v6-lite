import { readFileSync } from 'node:fs';

// Agent-mode (ADR-013) helpers kept free of Electron imports so node --test can load them.

/** meta.projectName from a spec JSON, or null while the file is missing/half-written (agent mode polls it). The pattern mirrors spec.schema.json, which also keeps path separators out of the project dir. */
export function tryReadProjectName(specPath: string): string | null {
  try {
    const doc = JSON.parse(readFileSync(specPath, 'utf8'));
    const name = doc?.meta?.projectName;
    if (typeof name === 'string' && /^[A-Za-z][A-Za-z0-9]*$/.test(name)) return name;
  } catch {
    /* missing or partially written */
  }
  return null;
}

/** The agent's own "stopped - <reason>" line (exit 1 prints it on stdout, not stderr). */
export function agentStopLine(stdout: string): string | null {
  const m = stdout.match(/agent: stopped - (.+)/);
  return m ? m[1].trim() : null;
}
