// The wallpaper registry (design-specs.md §4's theme/skin lineup, flattened to one picker per
// Stefan's request). Each entry is a full "look" — it carries its own theme's colors/fonts
// (`data-theme`) plus, where relevant, a skin override (`data-skin`) and its own background
// image. Mirrors i18n/index.ts's LANGUAGES pattern: the picker in main.ts is fully data-driven
// off this list, so adding a wallpaper later is a table entry here + a CSS rule, not a wiring
// change. `labelKey` indexes into each language table's `wallpaper` strings (i18n/*.ts) so the
// picker's labels translate; there is no in-picker description anymore (removed per request).

export type ThemeId = 'nova' | 'imperator' | 'veil' | 'forge' | 'enchant';

export interface WallpaperMeta {
  /** Stable id, also the persisted settings.wallpaper value. */
  id: string;
  /** The theme whose colors/fonts/panel style this wallpaper carries. */
  theme: ThemeId;
  /** Skin override on top of the theme, '' = the theme's own default. */
  skin: string;
  /** Key into i18n's `wallpaper` string table (Strings['wallpaper']). */
  labelKey:
    | 'novaAstroHud'
    | 'novaCodeCorridor'
    | 'novaGerman'
    | 'imperatorPoster1'
    | 'imperatorPoster2'
    | 'imperatorPoster3'
    | 'veilOccult'
    | 'veilIslamic'
    | 'forge'
    | 'enchant';
}

// nova-code-corridor is FIRST (Stefan: default wallpaper on first open should be the coding
// theme) - normalizeWallpaper()/every "unknown or unset" fallback resolves to WALLPAPERS[0].
export const WALLPAPERS: readonly WallpaperMeta[] = [
  { id: 'nova-code-corridor', theme: 'nova', skin: 'code', labelKey: 'novaCodeCorridor' },
  { id: 'nova-astro-hud', theme: 'nova', skin: '', labelKey: 'novaAstroHud' },
  { id: 'nova-german', theme: 'nova', skin: 'german', labelKey: 'novaGerman' },
  { id: 'imperator-poster-1', theme: 'imperator', skin: '', labelKey: 'imperatorPoster1' },
  { id: 'imperator-poster-2', theme: 'imperator', skin: 'poster2', labelKey: 'imperatorPoster2' },
  { id: 'imperator-poster-3', theme: 'imperator', skin: 'poster3', labelKey: 'imperatorPoster3' },
  { id: 'veil-occult', theme: 'veil', skin: 'occult', labelKey: 'veilOccult' },
  { id: 'veil-islamic', theme: 'veil', skin: 'islamic', labelKey: 'veilIslamic' },
  { id: 'forge', theme: 'forge', skin: '', labelKey: 'forge' },
  { id: 'enchant', theme: 'enchant', skin: '', labelKey: 'enchant' },
] as const;

/** Resolve a (possibly persisted/unknown) wallpaper id to a valid one; defaults to the first entry. */
export function normalizeWallpaper(id: string | undefined | null): WallpaperMeta {
  return WALLPAPERS.find((w) => w.id === id) ?? WALLPAPERS[0]!;
}
