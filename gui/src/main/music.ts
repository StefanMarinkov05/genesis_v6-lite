import { existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { musicCategoryDir, type MusicCategory } from './paths';
import { filterMp3Files, filterVideoFiles, pickRandom } from './music-parser';

// "Play Veselin Marinov" (IMPERATOR theme only) — Stefan drops his own files into
// design/music/{songs-audio,concerts-audio,concerts-video}/; this never ships any media itself.
// Read-only, best-effort: an empty/missing folder means "no track available" rather than a
// thrown error, matching the same stance logs/registry.json's reader takes for a missing file.

function filterFor(category: MusicCategory, names: string[]): string[] {
  return category === 'concertsVideo' ? filterVideoFiles(names) : filterMp3Files(names);
}

/** Every playable filename in the given category's subfolder, or [] if missing/empty. */
export function listMusicFiles(category: MusicCategory): string[] {
  const dir = musicCategoryDir(category);
  if (!existsSync(dir)) return [];
  return filterFor(category, readdirSync(dir));
}

/** Absolute path to a random file in the given category, or null if none exist. */
export function pickRandomTrack(category: MusicCategory): string | null {
  const files = listMusicFiles(category);
  if (files.length === 0) return null;
  return join(musicCategoryDir(category), pickRandom(files));
}
