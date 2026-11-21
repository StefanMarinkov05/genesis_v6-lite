# Trump-bg handbook — watermark + demo behaviour in chapter 23

**Type:** Docs
**Model:** Opus — Bulgarian Trump edition only (`docs/handbook/trump-bg/`).

## Summary

The author asked to carry the watermark/demo documentation (added to the serious Bulgarian edition
in changelog 140) into the Bulgarian **Trump** edition's chapter 23, in that edition's voice.

`docs/handbook/trump-bg/23-the-delivered-application.md`, section "Знаци за собственост": the three
ownership facts were kept as they were, and four subsections were added in the edition's bombastic
stump-speech register:

- *Какво представлява знакът* — the inert mark: passive, does not run or phone home, a marked project
  is still a clean build and passes `verify`; only evidentiary value.
- *Демото с воден знак* — the **disclosed** demo: the buyer is told up front, only the seller's tool
  unlocks the clean build.
- *Какво прави демото, ако го ползваш без лиценз* — the behaviours as the demo's disclosed limits:
  plays music (Azis), self-modifies its source so it won't compile, enters heavy computation so
  pages don't load, and similar — all gone in the clean build.
- *Границите* — scope note: confined to the demo project (not the buyer's other files, system, or
  third parties), removed by the clean build; the book documents behaviour, not the removal tool or
  the self-modification implementation.

The buyer-disclosure list was expanded to match. Facts and the safety boundary are identical to the
serious bg edition (changelog 140); only the register differs.

## Verification

`build-handbook2.py --lang trump-bg` → 111 pages, 0 Missing character warnings, no duplicate headings
in chapter 23. Other editions untouched.
