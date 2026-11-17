import type { Strings } from './en.ts';
import { en } from './en.ts';

// Standard Galactic Alphabet ("SGA" / the Minecraft enchanting-table script). Per the GUI
// architecture doc, SGA is NOT a translation — it is a *font swap over the English strings*.
// So the string table is exactly EN; selecting it additionally toggles the `.lang-sga` class
// on the window, which swaps the display/body font-family to the SGA face.
//
// The SGA glyph FONT itself is owned by the ENCHANT theme (design-specs.md §176: "Home of the
// Standard Galactic Alphabet display font"), which is a separate phase-3 theme task. Until that
// font asset lands, `.lang-sga` falls back to the normal faces, so SGA currently renders as
// plain English — the font-swap SEAM is complete and correct; only the glyph asset is deferred
// to the theme task that owns it. This is the honest boundary, not a stub.
export const sga: Strings = en;
