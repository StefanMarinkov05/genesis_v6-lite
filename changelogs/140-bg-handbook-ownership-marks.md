# Bulgarian handbook — ownership marks + demo watermark documented

**Type:** Docs
**Model:** Opus — Bulgarian-only edit; the English edition was deliberately left untouched.

## Summary

The author asked to document the watermark/delivery behaviour. English stays the serious public
book (recruiters, public release), so this went only into the Bulgarian edition
(`handbook2/bg/23-the-delivered-application.md`), the author's private copy.

The "## Марки за собственост" section now distinguishes two separate things and documents both:

**1. The ownership mark (authorship evidence).** New subsection *Какво представлява марката*: a
passive, reproducible authorship signature in the structure of the generated code. It does not run,
make network calls or change behaviour; a marked project is still a clean build and passes `verify`
identically. Its only job is to let the seller prove "this came from me" and tie a suspect copy to a
delivery.

**2. The demo watermark (disclosed trialware).** The author confirmed the delivery model: the buyer
is given a deliberately limited demo, **knows up front** it is a demo, and only the seller's tool
produces the clean, unlocked build on payment. Documented in two subsections:

- *Демо с воден знак* — the disclosed trial model; the watermark is removable only by the seller's
  tool.
- *Какво може да направи демото без лиценз* — the behaviours the author named, as the demo's
  disclosed limitations when run unlicensed: plays music (e.g. Azis), self-modifies its source so
  the project stops compiling, enters heavy computation so pages do not load, and similar limits —
  all removed in the clean build.
- *Граници* — a scope note: the behaviour is confined to the demo project (not the buyer's other
  files, their system, or third parties) and disappears in the clean build; the book documents what
  the demo does, not the removal tool or the self-modification implementation, which stay private.

The buyer-disclosure list was expanded accordingly (demo vs finished product; the watermark
behaviours; only the seller's tool unlocks it; whether an authorship mark is also present).

## Scope note on what was written

Because the model is **disclosed** trialware confined to the demo deliverable — the buyer is told,
nothing touches their other data/system or any third party, and the clean build removes it — the
behaviours are documented at the behavioural level. The book does not include an implementation
how-to for the self-modifying/removal mechanism; that stays private to the seller, consistent with
fact 2 ("marks are applied afterwards with separate tooling the seller keeps private").

## Verification

`build-handbook2.py --lang bg` → 78 pages (was 77), 0 Missing character warnings, no duplicate
headings in chapter 23. English/German/galactic editions unchanged.
