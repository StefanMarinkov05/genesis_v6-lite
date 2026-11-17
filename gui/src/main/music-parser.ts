// Pure logic for picking a random track from a list of filenames — no filesystem/Electron
// import, so it's directly unit-testable under plain node --test (same split as
// registry-parser.ts / recent-projects.ts).

const AUDIO_EXTENSIONS = ['.mp3'];
// concerts-video (Stefan) needs its own extension set - common container formats a browser
// <video> element can play natively without a transcode step.
const VIDEO_EXTENSIONS = ['.mp4', '.webm', '.mov'];

/** Filters to .mp3 files only (case-insensitive extension), sorted for a stable base order. */
export function filterMp3Files(entries: readonly string[]): string[] {
  return filterByExtensions(entries, AUDIO_EXTENSIONS);
}

/** Filters to playable video files only (case-insensitive extension), sorted. */
export function filterVideoFiles(entries: readonly string[]): string[] {
  return filterByExtensions(entries, VIDEO_EXTENSIONS);
}

function filterByExtensions(entries: readonly string[], extensions: readonly string[]): string[] {
  return entries.filter((f) => extensions.some((ext) => f.toLowerCase().endsWith(ext))).sort();
}

/** Picks one filename at random from a non-empty list. Caller guards the empty case. */
export function pickRandom(files: readonly string[]): string {
  if (files.length === 0) throw new Error('pickRandom called with an empty list');
  return files[Math.floor(Math.random() * files.length)]!;
}
