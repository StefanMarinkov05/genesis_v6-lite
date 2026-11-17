import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { agentStopLine, tryReadProjectName } from '../src/main/agent-helpers.ts';

// Agent mode (ADR-013): the runner learns the project name by polling the spec the CLI drafts,
// and reports the agent's own stop reason on failure.

test('tryReadProjectName: null until the spec exists, then its projectName', () => {
  const dir = mkdtempSync(join(tmpdir(), 'dpsnn-agent-'));
  try {
    const spec = join(dir, 'project-spec.json');
    assert.equal(tryReadProjectName(spec), null);
    writeFileSync(spec, '{"meta": {"projectName": "Book'); // half-written
    assert.equal(tryReadProjectName(spec), null);
    writeFileSync(spec, JSON.stringify({ meta: { projectName: 'BookNook' } }));
    assert.equal(tryReadProjectName(spec), 'BookNook');
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('tryReadProjectName rejects names that could escape the output folder', () => {
  const dir = mkdtempSync(join(tmpdir(), 'dpsnn-agent-'));
  try {
    const spec = join(dir, 'project-spec.json');
    for (const bad of ['../x', 'a/b', '', '1abc']) {
      writeFileSync(spec, JSON.stringify({ meta: { projectName: bad } }));
      assert.equal(tryReadProjectName(spec), null, bad);
    }
  } finally {
    rmSync(dir, { recursive: true, force: true });
  }
});

test('agentStopLine extracts the CLI stop reason', () => {
  const out = '[dps-nn] x\n[dps-nn] agent: stopped - still failing after 3 repair round(s). Report: /o/agent-report.json\n';
  assert.equal(agentStopLine(out), 'still failing after 3 repair round(s). Report: /o/agent-report.json');
  assert.equal(agentStopLine('[dps-nn] agent: done after 1 attempt(s).'), null);
});
