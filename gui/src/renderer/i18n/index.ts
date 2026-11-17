// i18n registry — the single place the renderer resolves a language code to its string table
// and its picker metadata. Adding a language is a table fill here + one entry in LANGUAGES;
// the flag-picker and switch machinery in main.ts are data-driven off this list, so no wiring
// changes are needed per language.
//
// SCOPE: GUI chrome only. See the boundary note in en.ts — none of this touches the language a
// GENERATED app is produced in.

import { en, type Strings } from './en.ts';
import { bg } from './bg.ts';
import { de } from './de.ts';
import { ru } from './ru.ts';
import { tr } from './tr.ts';
import { romani } from './romani.ts';
import { sga } from './sga.ts';
import { morseFull } from './morse-full.ts';
import { hex } from './hex.ts';
import { buildSign } from './sign.ts';
import { pirate } from './pirate.ts';
import { leet } from './leet.ts';
import { flip } from './flip.ts';

export type { Strings };

/**
 * The supported GUI languages. `sga` is a font swap over English, not a translation.
 * `morseFull`/`hex`/`sign`/`pirate`/`leet`/`flip` are the MECHANICALLY-GENERATED
 * joke tier (see encoders.ts) — real encodings of the EN table, not hand-written prose.
 * `morseFull`/`hex` commit fully; `sign` is a random run of hand-gesture emoji, re-rolled every
 * time it's (re)selected (stringsFor() below rebuilds it fresh instead of reusing a cached
 * table, unlike every other language); `pirate` is a whole-word dictionary swap; `leet` is
 * character substitution; `flip` is upside-down/mirrored Unicode.
 * (`yoda`, the one hand-written joke-tier table, was removed - Stefan.)
 */
export type LangCode =
  | 'en' | 'bg' | 'de' | 'ru' | 'tr' | 'romani' | 'sga'
  | 'morseFull' | 'hex' | 'sign' | 'pirate' | 'leet' | 'flip';

export interface LanguageMeta {
  code: LangCode;
  /** English name (for tooltips/aria). */
  label: string;
  /** Endonym (or, for the joke tier, an in-character name) shown in the picker menu. */
  nativeLabel: string;
  /** Compact code shown on the collapsed picker button (e.g. "EN", "RMN"). */
  short: string;
  /** True for the novelty tier — the picker renders a thin divider before the first one. */
  joke?: boolean;
}

// Order = picker order. Real languages first (EN base, then the four translations, Turkish),
// SGA bridges to the joke tier (real script but not a translation), then the joke languages.
export const LANGUAGES: readonly LanguageMeta[] = [
  { code: 'en', label: 'English', nativeLabel: 'English', short: 'EN' },
  { code: 'bg', label: 'Bulgarian', nativeLabel: 'Български', short: 'BG' },
  { code: 'de', label: 'German', nativeLabel: 'Deutsch', short: 'DE' },
  { code: 'ru', label: 'Russian', nativeLabel: 'Русский', short: 'RU' },
  { code: 'tr', label: 'Turkish', nativeLabel: 'Türkçe', short: 'TR' },
  { code: 'romani', label: 'Romani', nativeLabel: 'Romani ćhib', short: 'RMN' },
  { code: 'sga', label: 'Standard Galactic', nativeLabel: 'ᔑ⊣ᔑ', short: 'SGA' },
  { code: 'morseFull', label: 'Morse (everything)', nativeLabel: '− − − · · ·', short: 'MOR+', joke: true },
  { code: 'hex', label: 'Hexadecimal', nativeLabel: '0x48 0x45 0x58', short: '0x', joke: true },
  { code: 'sign', label: 'Sign language', nativeLabel: '✋', short: '✋', joke: true },
  { code: 'pirate', label: 'Pirate', nativeLabel: 'Pirate speak', short: 'ARR', joke: true },
  { code: 'leet', label: 'l33tspeak', nativeLabel: '1337sp34k', short: '1337', joke: true },
  { code: 'flip', label: 'Upside down', nativeLabel: 'uʍop-ǝpısdn', short: 'ᔭᴉꞁℲ', joke: true },
] as const;

/** Metadata for a language code (falls back to EN's entry for an unknown code). */
export function langMeta(code: string | undefined | null): LanguageMeta {
  const c = normalizeLang(code);
  return LANGUAGES.find((l) => l.code === c)!;
}

// `sign` deliberately excluded - it's rebuilt fresh on every stringsFor() call (below) instead
// of cached here, so its random gestures actually change each time it's selected.
const CATALOG: Record<Exclude<LangCode, 'sign'>, Strings> = {
  en, bg, de, ru, tr, romani, sga,
  morseFull, hex, pirate, leet, flip,
};

/** Resolve a (possibly persisted/unknown) code to a valid one; defaults to EN. */
export function normalizeLang(code: string | undefined | null): LangCode {
  return LANGUAGES.some((l) => l.code === code) ? (code as LangCode) : 'en';
}

/** The string table for a language code (falls back to EN for an unknown code). */
export function stringsFor(code: string | undefined | null): Strings {
  const c = normalizeLang(code);
  return c === 'sign' ? buildSign() : CATALOG[c];
}

export { en };
