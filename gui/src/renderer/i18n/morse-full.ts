import type { Strings } from './en.ts';
import { fromEn, morseFull as encode } from './encoders.ts';

// Morse (full): the fully-committed bit — EVERY string, including long toast/error prose, is
// real dot-dash Morse. Genuinely unusable as an actual interface if someone selects it and
// tries to read the settings hint; that's the joke. Function-valued entries are wrapped so
// their OUTPUT (e.g. `Stage 3/7 complete`) is encoded too — the interpolated numbers stay as
// literal digits (Morse has digit codes) inside the encoded string.
export const morseFull: Strings = fromEn(encode, /* wrapFn */ true);
