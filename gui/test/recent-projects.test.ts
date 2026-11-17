import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { parseRegistrySnapshot } from '../src/main/registry-parser.ts';

// Verifies registry-parser.ts against a real registry.json shape (Program.cs
// "RecordProjectRegistry" — {project: {ts, project, specPath, seedResponsePath, outDir}}).
// recent-projects.ts (the filesystem-reading wrapper) depends on genesisRoot(), which needs
// Electron's `app` module and isn't importable under plain node --test — this file has zero
// such import, so it covers the actual bug-prone logic (sort order, malformed-JSON fallback)
// directly, without mocking Electron.

const snapshot = JSON.stringify({
  FaceNote: {
    ts: '2026-07-16T17:32:28.9186529+00:00',
    project: 'FaceNote',
    specPath: 'D:\\Projects\\dps-nn\\fixtures\\project-spec.facenote.json',
    seedResponsePath: 'D:\\Projects\\dps-nn\\prompts\\in\\seed-response.json',
    outDir: 'D:\\Projects\\dps-nn\\out',
  },
  FaceNoteEn: {
    ts: '2026-07-11T00:01:51.2281136+00:00',
    project: 'FaceNoteEn',
    specPath: 'D:\\Projects\\dps-nn\\fixtures\\project-spec.facenoteen.json',
    seedResponsePath: null,
    outDir: 'D:\\Projects\\dps-nn\\out\\FaceNoteEn',
  },
});

test('parses every registry entry and sorts newest-first', () => {
  const result = parseRegistrySnapshot(snapshot);
  assert.equal(result.length, 2);
  assert.equal(result[0]!.project, 'FaceNote'); // 2026-07-16, newer
  assert.equal(result[1]!.project, 'FaceNoteEn'); // 2026-07-11, older
  assert.equal(result[0]!.outDir, 'D:\\Projects\\dps-nn\\out');
});

test('malformed JSON yields an empty list instead of throwing', () => {
  assert.deepEqual(parseRegistrySnapshot('{not valid json'), []);
});

test('an empty registry object yields an empty list', () => {
  assert.deepEqual(parseRegistrySnapshot('{}'), []);
});
