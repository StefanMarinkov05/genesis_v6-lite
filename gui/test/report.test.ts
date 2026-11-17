import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { parseCompletionBanner } from '../src/main/report.ts';

// Verifies the completion-banner parser against a real banner shaped exactly like
// Program.cs PrintCompletionBanner emits (box-drawn, padded). This is the bug-prone
// custom logic that turns captured CLI stdout into the report readout.

const banner = [
  'some earlier build noise',
  '',
  '╔════════════════════════════════════════════════════════╗',
  '║  FaceNote                                              ║',
  '║  🔗 http://localhost:5043                              ║',
  '╠════════════════════════════════════════════════════════╣',
  '║  Admin            admin@facenote.bg / Admin!234        ║',
  '║  User             user@facenote.bg / User!234          ║',
  '╚════════════════════════════════════════════════════════╝',
  'Database: FaceNote_20260712',
  'Start: dotnet run --project "out/FaceNote"',
  '',
].join('\n');

test('parses url, credentials, db and start command', () => {
  const r = parseCompletionBanner(banner);
  assert.equal(r.url, 'http://localhost:5043');
  assert.equal(r.dbName, 'FaceNote_20260712');
  assert.equal(r.startCommand, 'dotnet run --project "out/FaceNote"');
  assert.equal(r.credentials.length, 2);
  assert.deepEqual(r.credentials[0], {
    role: 'Admin',
    email: 'admin@facenote.bg',
    password: 'Admin!234',
  });
  assert.deepEqual(r.credentials[1], {
    role: 'User',
    email: 'user@facenote.bg',
    password: 'User!234',
  });
});

test('does not mistake the project-name or url rows for credentials', () => {
  const r = parseCompletionBanner(banner);
  // Only the two rows between ╠ and ╚ are credentials; the name/url rows precede ╠.
  assert.ok(r.credentials.every((c) => c.email.includes('@')));
});

test('empty stdout yields an all-null report, never throws', () => {
  const r = parseCompletionBanner('');
  assert.equal(r.url, null);
  assert.equal(r.dbName, null);
  assert.equal(r.credentials.length, 0);
});
