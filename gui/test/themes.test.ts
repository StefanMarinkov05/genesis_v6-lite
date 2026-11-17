import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { WALLPAPERS, normalizeWallpaper } from '../src/renderer/themes.ts';

// Guards the wallpaper registry (design-specs.md §4's theme/skin lineup, flattened into one
// picker of 10 clickable options per Stefan's request) the same way i18n.test.ts guards
// LANGUAGES: unique ids, every theme's structural rules honored, and a working fallback.

test('WALLPAPERS has ten unique ids, each with a theme and a labelKey', () => {
  assert.equal(WALLPAPERS.length, 10);
  const ids = new Set(WALLPAPERS.map((w) => w.id));
  assert.equal(ids.size, 10);
  assert.ok(WALLPAPERS.every((w) => w.theme.length > 0 && w.labelKey.length > 0));
});

test('the default (first) wallpaper is the NOVA code-corridor look', () => {
  assert.equal(WALLPAPERS[0]!.theme, 'nova');
  assert.equal(WALLPAPERS[0]!.skin, 'code');
});

test('IMPERATOR never carries a skin belonging to another theme (VM-exclusive, decision #1)', () => {
  const imperatorWallpapers = WALLPAPERS.filter((w) => w.theme === 'imperator');
  assert.equal(imperatorWallpapers.length, 3); // 3 VM posters
  // Each imperator entry's own "skin" value (poster2/poster3/'') only ever selects between
  // imperator's OWN images — never reuses a skin id from a different theme like 'islamic'.
  assert.ok(imperatorWallpapers.every((w) => !['islamic', 'occult', 'german'].includes(w.skin)));
});

test('VEIL has exactly its two documented skin-backed wallpapers', () => {
  const veilWallpapers = WALLPAPERS.filter((w) => w.theme === 'veil');
  const skins = veilWallpapers.map((w) => w.skin).sort();
  assert.deepEqual(skins, ['islamic', 'occult']);
});

test('normalizeWallpaper resolves a known id to its own entry', () => {
  const w = normalizeWallpaper('forge');
  assert.equal(w.id, 'forge');
  assert.equal(w.theme, 'forge');
});

test('normalizeWallpaper falls back to the default (first) wallpaper for an unknown/missing id', () => {
  assert.equal(normalizeWallpaper('does-not-exist').id, WALLPAPERS[0]!.id);
  assert.equal(normalizeWallpaper(undefined).id, WALLPAPERS[0]!.id);
  assert.equal(normalizeWallpaper(null).id, WALLPAPERS[0]!.id);
});
