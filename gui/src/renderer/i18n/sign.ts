import type { Strings } from './en.ts';
import { fromEn, signGlyph } from './encoders.ts';

// Sign language — a random run of hand-gesture emoji per string (signGlyph, encoders.ts), not
// fixed text. buildSign() is a FUNCTION, not a pre-built constant, specifically so index.ts's
// stringsFor() can call it fresh every time this language is (re)selected - re-rolling the
// gestures each time instead of freezing one random pick for the whole app session.
export function buildSign(): Strings {
  return fromEn(signGlyph, /* wrapFn */ true);
}
