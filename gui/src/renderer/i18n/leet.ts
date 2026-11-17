import type { Strings } from './en.ts';
import { fromEn, leetEncode } from './encoders.ts';

// l33tspeak — the classic character substitution (A→4, E→3, I→1, O→0, S→5, T→7, B→8, G→6, Z→2,
// L→1), applied to every string including function outputs. Generated from EN via encoders.ts.
export const leet: Strings = fromEn(leetEncode, /* wrapFn */ true);
