# Handbook: the Bulgarian Trump edition, "Изданието Тръмп" (a translation of 142)

**Type:** Docs
**Model:** Opus. All 26 chapters of the Trump edition are translated into Bulgarian in the same
voice, with local jokes added and every technical fact held at the post-141 state.

## Summary

The author asked for a second Trump handbook in Bulgarian. It translates the English Trump edition
(changelog 142) and uses `MDGA_bg.png` as the cover logo. Extra funny phrases and references,
including Trump's, may be added for a Bulgarian audience. The no-people / no-parties / no-ideologies
rule from 142 still applies. The author also said: **do not read the bg chapter 23 when
transferring**. So chapter 23 was translated only from `trump/23`, and `bg/23` was never opened (the
fill script asserts this).

## What was made

- **`docs/handbook/trump-bg/00–25`**, all 26 chapters. They were written as `handbook2/trump-bg/`;
  a concurrent restructure moved `handbook2/` to `docs/handbook/` mid-write.
  - Prose and titles are translated in the voice. The product is **ДПС-НН** in prose, the English
    edition calls it **Genesis**, and the executable is `dps-nn` (the ch01 "Names" table explains
    this).
  - Terminology follows the bg edition's glossary (етап, конвейер, компилационна порта, отметка,
    дизайн-пресет, отклонение, ос на разнообразието, работна папка, инсталационен корен…). The ch24
    glossary is re-sorted alphabetically in Bulgarian, with code-named terms at the end. It still
    has 37 terms.
  - **Facts follow `trump/` (post-141), not `bg/`.** The bg edition still predates 141 on the verify
    strictness, `[Authorize]`, cart/personalization and the preset.
  - Tables are translated. Code fences are copied verbatim from `trump/` and Mermaid diagrams
    (Bulgarian labels) from `bg/`, both through `<!-- CODE:n -->` / `<!-- MERMAID:n -->`
    placeholders that a one-off script filled. Block counts were checked per chapter before the
    fill.
  - Jokes added for a Bulgarian audience (sayings, food, everyday life; no people, parties or
    ideologies):
    - sayings: „Тихата вода брег рони“, „Дадена дума — хвърлен камък“, „Не казвай гоп…“;
    - food: баница, шопска салата, кисело мляко, боб чорба;
    - everyday life: панелки, the old Лада, the neighbour at the peephole (SmartScreen), the
      таксиметров шофьор без апарат (the old `GetHashCode()`), the сватба без покана (verify),
      Витоша / Мусала, отличен шест.
  - The running nicknames are translated: **Сънливия No-Op** and **Нискоенергийния `GetHashCode()`**.
- **`scripts/build-handbook2.py`**: a new `--lang trump-bg` variant.
  - `dir="trump-bg"`, title ДПС-НН, ЧАСТ / ГЛАВА labels, TOC „Съдържание — само победители“.
  - Top label „ДА НАПРАВИМ ДИПЛОМНИТЕ ПРОЕКТИ ОТНОВО ВЕЛИКИ“, and `BADGE_TRUMP_BG`
    („ИЗДАНИЕТО ТРЪМП“ plus three gold stars).
  - `STRIPE_TRUMP_BG`: the tricolour (red / green / white, 0.52 cm each) under the same gold band
    and seven white stars as `STRIPE_TRUMP`.
  - `PARTS_TRUMP_BG` gives the part titles in the voice.
  - Reused from 142: `PALETTE_TRUMP`, `MERMAID_TRUMP` and `TRUMP_FONTS` (IBM Plex, Playfair Display
    and Montserrat all carry Cyrillic).
  - Logo `gui/src/renderer/public/logo/MDGA_bg.png`, judged by file name only.
  - Added to the `TEXT_TRANSFORMS = []` tuple; the docstring lists the variant.
- **`dist/dps-nn-handbook-trump-bg.pdf`**: 110 pages, 26 docs, 9 diagrams, **0 Missing character**.
  - I checked the cover's top and bottom crops (the logo middle was not viewed) and the TOC, ch09
    and ch13 pages.
  - The English `dps-nn-handbook-trump.pdf` was rebuilt too (98 pages, 0 Missing character), because
    `trump/01` changed after 142's build: its "Sir" riff was moved out from between "has
    everything:" and the feature list.

## Known / not done

- The cover's "Version 6.6.6 (sha)" line is English in every edition, bg included. It is hard-coded
  in `scripts/build-handbook.py`.
- The concurrent restructure (in progress, not this change) is moving `feature-templates/` →
  `templates/features/` and `images/` → `templates/static/`. Every handbook edition, this one
  included, still names the old paths, so a follow-up should sweep all editions once that lands.
- Still stale in en/de (from 142): en 01:127, 15:103, 24 (preset and verify rows), 25:16.
