import type { Strings } from './en.ts';
import { fromEn, hexEncode } from './encoders.ts';

// Hex — Stefan's call: "if 0s and 1s are too much, replace with hex." Every string (including
// function outputs) becomes its UTF-8 bytes as space-separated hex pairs. Fully committed —
// hex pairs are at least still scannable in a monospace font, which raw binary for a full
// sentence would not have been.
export const hex: Strings = fromEn(hexEncode, /* wrapFn */ true);
