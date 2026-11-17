import type { Strings } from './en.ts';
import { fromEn, pirateEncode } from './encoders.ts';

// Pirate speak — whole-word dictionary substitution (hello→ahoy, your→yer, stop→belay…) with
// case preserved and a stock "Arrr," prefix on longer sentence-shaped strings. Deterministic
// (no randomness), generated from EN via encoders.ts so every string in the shape is covered.
// Unmapped words (most of the vocabulary — this is flavor, not a real dialect) pass through
// as plain English, which is the honest limit of a small dictionary; it's still recognizably
// "pirate" on every button that matters (Start, Stop, Settings, Save…).
//
// wrapFn is deliberately FALSE (unlike hex/flip/leet): function-valued entries interpolate a
// CALLER-SUPPLIED value — error.title(stage) receives the real pipeline stage name, e.g. "seed"
// — and PIRATE_WORDS happens to map "seed" → "buried treasure". A whole-word dictionary swap
// applied to that composed string would silently turn "stage 'seed' failed" into "stage 'buried
// treasure' failed", destroying the exact information an error screen exists to convey. This is
// the same "never blind-replace inside a value/identifier" lesson as changelog 070's BG
// residue-map bug, caught here before shipping instead of after. Character ciphers (hex/morse/
// flip/leet) don't have this failure mode — they have no word-boundary semantics to collide
// with a real identifier, so they safely wrap function outputs.
export const pirate: Strings = fromEn(pirateEncode, /* wrapFn */ false);
