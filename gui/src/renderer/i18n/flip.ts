import type { Strings } from './en.ts';
import { fromEn, flipEncode } from './encoders.ts';

// Mirrored / upside-down text (ʇxǝʇ uʍop-ǝpısdn) — the classic Unicode flip-table trick: each
// letter/digit maps to a lookalike glyph and the whole string is reversed, since flipping a
// line of text also reverses its reading order. Generated from EN via encoders.ts.
export const flip: Strings = fromEn(flipEncode, /* wrapFn */ true);
