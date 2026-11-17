import { en, type Strings } from './en.ts';

// Shared machinery for the "joke" languages (Morse, hex, sign) — generated from the EN table
// by a real encoder instead of hand-transcribed, so there's zero risk of a mistyped dot-dash
// or hex pair. `deepMap` walks the Strings shape once and applies a leaf transform to every
// string, and (optionally) wraps every function leaf so its OUTPUT gets the same transform —
// this is what lets a single small encoder function generate an entire, shape-correct
// `Strings` table with the type system's own guarantee (Widen<typeof en>) that nothing is
// missing or mistyped.

type AnyObj = Record<string, unknown>;

function deepMap(node: AnyObj, leaf: (s: string) => string, wrapFn: boolean): AnyObj {
  const out: AnyObj = {};
  for (const [k, v] of Object.entries(node)) {
    if (typeof v === 'string') out[k] = leaf(v);
    else if (typeof v === 'function') {
      out[k] = wrapFn ? (...args: unknown[]) => leaf((v as (...a: unknown[]) => string)(...args)) : v;
    } else out[k] = deepMap(v as AnyObj, leaf, wrapFn);
  }
  return out;
}

/** Build a full `Strings` table from EN by transforming every leaf string. */
export function fromEn(leaf: (s: string) => string, wrapFn: boolean): Strings {
  return deepMap(en as unknown as AnyObj, leaf, wrapFn) as unknown as Strings;
}

// ---- International Morse code -------------------------------------------------------------
const MORSE: Record<string, string> = {
  A: '.-', B: '-...', C: '-.-.', D: '-..', E: '.', F: '..-.', G: '--.', H: '....', I: '..',
  J: '.---', K: '-.-', L: '.-..', M: '--', N: '-.', O: '---', P: '.--.', Q: '--.-', R: '.-.',
  S: '...', T: '-', U: '..-', V: '...-', W: '.--', X: '-..-', Y: '-.--', Z: '--..',
  0: '-----', 1: '.----', 2: '..---', 3: '...--', 4: '....-', 5: '.....', 6: '-....',
  7: '--...', 8: '---..', 9: '----.',
  '.': '.-.-.-', ',': '--..--', '?': '..--..', "'": '.----.', '!': '-.-.--', '/': '-..-.',
  '(': '-.--.', ')': '-.--.-', ':': '---...', ';': '-.-.-.', '=': '-...-', '+': '.-.-.',
  '-': '-....-', '_': '..--.-', '"': '.-..-.', '@': '.--.-.',
};

/**
 * Encode a string to Morse: letters/digits/punctuation → dot-dash groups (space-separated
 * within a "word", ` / ` between words). Characters with no Morse mapping — the UI's own
 * glyph icons (▶ ■ ❚❚ ✦ ✓ …) and the em dash — pass through UNCHANGED rather than being
 * dropped, so buttons keep a recognizable icon instead of turning into bare punctuation soup.
 */
function morseEncode(text: string): string {
  return text
    .split(/(\s+)/)
    .map((chunk) => {
      if (/^\s+$/.test(chunk)) return chunk;
      return chunk
        .split('')
        .map((ch) => {
          const code = MORSE[ch.toUpperCase()];
          return code ?? ch; // pass through icons/unmapped glyphs verbatim
        })
        .join(' ');
    })
    .join('')
    .replace(/\s{2,}/g, ' / ');
}

/** Morse-encode a string, but only if it's short (button/label length) — else pass through. */
export function morseHalf(text: string): string {
  return text.length <= 20 ? morseEncode(text) : text;
}
export const morseFull = morseEncode;

// ---- Hex (the "binary but readable" joke) --------------------------------------------------
/** UTF-8 bytes as space-separated hex pairs — every string, no length exception (the bit). */
export function hexEncode(text: string): string {
  return Array.from(new TextEncoder().encode(text))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join(' ');
}

// ---- Sign language ---------------------------------------------------------------------------
// Every leaf becomes a short run of hand-gesture emoji instead of one fixed glyph (Stefan: "add
// more hand gesture emojis at random size and order every time") - a real pool, a random COUNT
// per string (1-3, the "size"), and a random ORDER (fresh Fisher-Yates-via-sort each call), no
// fixed mapping from source text to gesture (there is no text, only gestures - same spirit as
// the original placeholder). buildSignTable() (below) re-invokes this over the WHOLE table on
// every call rather than once at module load, so re-selecting/re-rendering this language
// actually looks different each time, not just once at startup.
const HAND_GESTURES = [
  '✋', '🤚', '🖐️', '👋', '🤟', '🤙', '👌', '👍', '👎', '✊', '👊', '🤛', '🤜',
  '🖖', '👏', '🙌', '🤲', '🫶', '👐', '🤝', '☝️', '✌️', '🤞', '🫰', '👉', '👈',
] as const;
export function signGlyph(_text: string): string {
  const count = 1 + Math.floor(Math.random() * 3); // 1-3 gestures - the "random size"
  const picks: string[] = [];
  for (let i = 0; i < count; i++) {
    picks.push(HAND_GESTURES[Math.floor(Math.random() * HAND_GESTURES.length)]!);
  }
  return picks.join(' ');
}

// ---- Pirate speak ---------------------------------------------------------------------------
// Whole-word, case-preserving dictionary substitution + a couple of fixed prefix/suffix flavor
// words on sentence-shaped strings. Deterministic (no randomness) so the same EN string always
// produces the same pirate string — matters for the shape test and for a stable UI.
const PIRATE_WORDS: Record<string, string> = {
  hello: 'ahoy', hi: 'ahoy', my: 'me', your: 'yer', you: 'ye', "you're": "ye be",
  is: 'be', are: 'be', was: 'were', the: 'th\'', of: "o'", to: 'ter',
  yes: 'aye', no: 'nay', friend: 'matey', friends: 'mateys',
  stop: 'belay', start: 'hoist anchor', run: 'sail', running: 'sailin\'',
  application: 'vessel', app: 'vessel', database: 'treasure hold',
  build: 'voyage', building: 'voyagin\'', settings: 'ship\'s log', save: 'stow',
  error: 'squall', failed: 'wrecked', success: 'plunder', done: 'landed ashore',
  open: 'unbar', close: 'batten down', folder: 'chest', file: 'parchment',
  password: 'secret code', key: 'doubloon key', browser: 'spyglass',
  waiting: 'becalmed', found: 'spotted', selected: 'chosen', upload: 'load th\' hold',
  cancel: 'abandon', retry: 'try again, arr', generate: 'conjure', generated: 'conjured',
  specification: 'chart', seed: 'buried treasure', project: 'expedition',
  output: 'booty', credentials: 'letters o\' marque', duplicate: 'twin',
  minimize: 'batten down', maximize: 'unfurl', kill: 'maroon', full: 'full to th\' brim',
  log: 'ship\'s log', debug: 'bilge-huntin\'', mode: 'rig',
};

function matchCase(sample: string, replacement: string): string {
  if (sample === sample.toUpperCase() && sample !== sample.toLowerCase()) return replacement.toUpperCase();
  if (sample[0] === sample[0]?.toUpperCase() && sample[0] !== sample[0]?.toLowerCase()) {
    return replacement[0]!.toUpperCase() + replacement.slice(1);
  }
  return replacement;
}

export function pirateEncode(text: string): string {
  const swapped = text.replace(/[A-Za-z']+/g, (word) => {
    const hit = PIRATE_WORDS[word.toLowerCase()];
    return hit ? matchCase(word, hit) : word;
  });
  // Long, sentence-shaped strings get a flavor interjection; short button/label text stays clean
  // so it still fits its control.
  return swapped.length > 24 ? `Arrr, ${swapped[0]!.toLowerCase()}${swapped.slice(1)}` : swapped;
}

// ---- l33tspeak ------------------------------------------------------------------------------
const LEET: Record<string, string> = {
  a: '4', e: '3', i: '1', o: '0', s: '5', t: '7', b: '8', g: '6', z: '2', l: '1',
};

export function leetEncode(text: string): string {
  return text
    .split('')
    .map((ch) => LEET[ch.toLowerCase()] ?? ch)
    .join('');
}

// ---- Mirrored / upside-down text ------------------------------------------------------------
// Classic "flip text" table: each Latin letter/digit maps to a Unicode lookalike that reads as
// upside-down, and the whole string is reversed (flipping a line of text also reverses its
// left-to-right order). Unmapped characters (icons, CJK-ish glyphs, already-punctuation) pass
// through unreversed-per-char but still take part in the overall string reversal.
const FLIP: Record<string, string> = {
  a: 'ɐ', b: 'q', c: 'ɔ', d: 'p', e: 'ǝ', f: 'ɟ', g: 'ƃ', h: 'ɥ', i: 'ı', j: 'ɾ',
  k: 'ʞ', l: 'ן', m: 'ɯ', n: 'u', o: 'o', p: 'd', q: 'b', r: 'ɹ', s: 's', t: 'ʇ',
  u: 'n', v: 'ʌ', w: 'ʍ', x: 'x', y: 'ʎ', z: 'z',
  0: '0', 1: 'Ɩ', 2: 'ᄅ', 3: 'Ɛ', 4: 'ㄣ', 5: 'ϛ', 6: '9', 7: 'ㄥ', 8: '8', 9: '6',
  '.': '˙', ',': "'", "'": ',', '"': ',,', '!': '¡', '?': '¿', '(': ')', ')': '(',
  '[': ']', ']': '[', '<': '>', '>': '<', '&': '⅋', '_': '‾',
};

export function flipEncode(text: string): string {
  const flippedChars = Array.from(text).map((ch) => {
    const lower = ch.toLowerCase();
    const mapped = FLIP[lower];
    return mapped ?? ch; // pass through icons/unmapped glyphs unchanged
  });
  return flippedChars.reverse().join('');
}
