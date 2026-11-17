// Flag glyphs for the language picker, as inline SVG strings (NOT <img>/PNG). Inline SVG is
// the CSP-safe, self-contained choice: the renderer's strict CSP allows no external hosts and
// there are no bundled flag binaries; these are static, code-authored markup (no user input),
// so injecting them via innerHTML is safe. Each is a small 3:2 emblem that reads correctly at
// ~18px. viewBox clipping (default `overflow:hidden` on inline <svg>) keeps strokes inside the
// frame, so no per-flag clipPath IDs are needed (which would collide across instances).
//
// EN → Union Jack (simplified but recognizable). BG/DE/RU/TR → national flags. Romani → the
// blue-over-green field with the red chakra. SGA is not a nation: it gets a runic emblem tile
// (ties to the ENCHANT theme, design-specs.md §176), not a flag. The joke tier (Morse/hex/sign)
// isn't a nation either — each gets a small in-character emblem instead of a flag.

import type { LangCode } from './index.ts';

const uk = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="40" fill="#012169"/>
  <path d="M0,0 60,40 M60,0 0,40" stroke="#fff" stroke-width="8"/>
  <path d="M0,0 60,40 M60,0 0,40" stroke="#C8102E" stroke-width="4"/>
  <path d="M30,0 V40 M0,20 H60" stroke="#fff" stroke-width="13"/>
  <path d="M30,0 V40 M0,20 H60" stroke="#C8102E" stroke-width="7"/>
</svg>`;

const bg = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="13.34" y="0" fill="#fff"/>
  <rect width="60" height="13.34" y="13.34" fill="#00966E"/>
  <rect width="60" height="13.34" y="26.66" fill="#D62612"/>
</svg>`;

const de = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="13.34" y="0" fill="#000"/>
  <rect width="60" height="13.34" y="13.34" fill="#DD0000"/>
  <rect width="60" height="13.34" y="26.66" fill="#FFCE00"/>
</svg>`;

const ru = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="13.34" y="0" fill="#fff"/>
  <rect width="60" height="13.34" y="13.34" fill="#0039A6"/>
  <rect width="60" height="13.34" y="26.66" fill="#D52B1E"/>
</svg>`;

const tr = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="40" fill="#E30A17"/>
  <circle cx="24" cy="20" r="9" fill="#fff"/>
  <circle cx="26.6" cy="20" r="7.2" fill="#E30A17"/>
  <path d="M33.5,20 L44,16.7 L37,22.9 L40,13 L40,27 L37,17.1 L44,23.3 Z" fill="#fff"/>
</svg>`;

// Romani flag: blue upper half (sky), green lower half (earth), red 16-spoke chakra centered.
// Spokes approximated as an 8-line star (readable at small sizes) inside a red rim.
const romani = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="20" y="0" fill="#0088C7"/>
  <rect width="60" height="20" y="20" fill="#3AA935"/>
  <g stroke="#C1272D" stroke-width="2" fill="none">
    <circle cx="30" cy="20" r="7.5"/>
    <path d="M30,12 V28 M22,20 H38 M24.3,14.3 L35.7,25.7 M35.7,14.3 L24.3,25.7"/>
  </g>
</svg>`;

// SGA: a dark arcane tile with a cyan↔magenta rune glyph (ENCHANT motif), since SGA is a
// script, not a country. Purely decorative — the actual enchanting font ships with ENCHANT.
const sga = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="40" fill="#160a2b"/>
  <g stroke="#c56bff" stroke-width="2.4" fill="none" stroke-linecap="round">
    <path d="M22,10 V30 M22,10 H34 M22,20 H32 M38,10 V30 M38,30 H30"/>
  </g>
  <rect x="1" y="1" width="58" height="38" fill="none" stroke="#3fd0ff" stroke-width="1.5" opacity="0.5"/>
</svg>`;

// Morse: dot-dash pips on a dark technical field, echoing the terminal/mono aesthetic.
const morseFull = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="40" fill="#0a0e08"/>
  <g stroke="#8be04a" stroke-width="4" stroke-linecap="round">
    <line x1="10" y1="14" x2="18" y2="14"/>
    <line x1="24" y1="14" x2="36" y2="14"/>
    <line x1="42" y1="14" x2="50" y2="14"/>
    <circle cx="14" cy="26" r="2.6" fill="#8be04a" stroke="none"/>
    <circle cx="24" cy="26" r="2.6" fill="#8be04a" stroke="none"/>
    <line x1="32" y1="26" x2="44" y2="26"/>
  </g>
</svg>`;

// Hex: a small circuit/chip tile with "0x" — the joke's own explicit callout.
const hex = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="40" fill="#0a0a12"/>
  <rect x="10" y="10" width="20" height="20" rx="2" fill="none" stroke="#f5a623" stroke-width="2"/>
  <g stroke="#f5a623" stroke-width="1.6">
    <line x1="6" y1="16" x2="10" y2="16"/><line x1="6" y1="24" x2="10" y2="24"/>
    <line x1="30" y1="16" x2="34" y2="16"/><line x1="30" y1="24" x2="34" y2="24"/>
  </g>
  <text x="44" y="25" font-family="Consolas,monospace" font-size="15" fill="#f5a623" text-anchor="middle">0x</text>
</svg>`;

// Sign language: a simple raised open hand — the whole "flag" is the placeholder glyph itself.
const sign = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="40" fill="#141018"/>
  <text x="30" y="28" font-size="22" text-anchor="middle">✋</text>
</svg>`;

// Pirate: a black "Jolly Roger" field with a simplified skull-and-crossbones.
const pirate = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="40" fill="#0b0b0b"/>
  <circle cx="30" cy="16" r="8" fill="#eee"/>
  <circle cx="27" cy="15" r="1.6" fill="#0b0b0b"/>
  <circle cx="33" cy="15" r="1.6" fill="#0b0b0b"/>
  <path d="M27,20 Q30,22 33,20" stroke="#0b0b0b" stroke-width="1.4" fill="none"/>
  <g stroke="#eee" stroke-width="2.4" stroke-linecap="round">
    <path d="M16,30 L44,24 M16,24 L44,30"/>
  </g>
</svg>`;

// l33t: a green terminal field with a stylized "1337".
const leet = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="40" fill="#020a02"/>
  <text x="30" y="26" font-family="Consolas,monospace" font-size="15" fill="#39ff6a" text-anchor="middle">1337</text>
</svg>`;

// Flip: the flag itself is drawn upside down (rotated 180°) — the joke applied to its own icon.
const flip = `
<svg viewBox="0 0 60 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
  <rect width="60" height="40" fill="#101820"/>
  <g transform="rotate(180 30 20)">
    <text x="30" y="26" font-family="Consolas,monospace" font-size="14" fill="#6ee6ff" text-anchor="middle">flip</text>
  </g>
</svg>`;

const FLAGS: Record<LangCode, string> = {
  en: uk, bg, de, ru, tr, romani, sga, morseFull, hex, sign, pirate, leet, flip,
};

/** Inline SVG markup for a language's flag/emblem (trusted static string, safe for innerHTML). */
export function flagSvg(code: LangCode): string {
  return FLAGS[code].trim();
}
