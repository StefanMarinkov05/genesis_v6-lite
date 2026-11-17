import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { filterMp3Files, filterVideoFiles, pickRandom } from '../src/main/music-parser.ts';

// Guards the "Play Veselin Marinov" random-track picker's pure logic — no filesystem/Electron
// import, so it's directly unit-testable under plain node --test (same split as
// registry-parser.ts).

test('filterMp3Files keeps only .mp3 (case-insensitive) and sorts them', () => {
  const result = filterMp3Files(['b.MP3', 'notes.txt', 'a.mp3', 'cover.jpg']);
  assert.deepEqual(result, ['a.mp3', 'b.MP3']);
});

test('filterMp3Files returns an empty list when nothing matches', () => {
  assert.deepEqual(filterMp3Files(['readme.md', 'art.png']), []);
});

test('pickRandom always returns an element from the list', () => {
  const files = ['one.mp3', 'two.mp3', 'three.mp3'];
  for (let i = 0; i < 20; i++) {
    assert.ok(files.includes(pickRandom(files)));
  }
});

test('pickRandom throws on an empty list rather than returning undefined', () => {
  assert.throws(() => pickRandom([]));
});

test('filterVideoFiles keeps only mp4/webm/mov (case-insensitive) and sorts them', () => {
  const result = filterVideoFiles(['b.MP4', 'notes.txt', 'a.webm', 'c.mov', 'cover.jpg']);
  assert.deepEqual(result, ['a.webm', 'b.MP4', 'c.mov']);
});
