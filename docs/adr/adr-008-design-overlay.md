<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# ADR-008: Distinctive design as a token overlay, not per-template styling

Date: 2026-07-06 | Status: Accepted

## Context
The generated apps looked like themed Bootstrap - generic on the two axes that matter most
for distinctiveness (layout and depth). The frontend-distinctive-design skill calls for a
committed concept executed via a token system. But genesis emits N different projects from
one template set; we cannot hand-tune layout per screen without losing reusability.

## Decision
Distinctiveness is delivered as a single CSS **overlay driven by design tokens** (accent,
tinted neutrals, type pairing, radius, and a per-preset depth language), injected into the
layout head. Concept: printed pazar/classifieds broadsheet - editorial type, ruled navbar,
one committed depth language per preset (layered / hairline / offset), one signature
micro-interaction (card image zoom).

## Alternatives considered
- **Per-template bespoke layout**: highest ceiling, but breaks reusability across all
  generated projects and every spec - rejected (would need re-tuning per project).
- **Swap bootswatch theme only** (the prior state): cheapest, but defaults on layout+depth -
  the exact generic tell - rejected.
- **CSS-framework swap (Tailwind)**: large blast radius on every emitted view; the scaffolder
  and Identity pages assume Bootstrap classes - rejected as a one-way door with no payoff here.

## Consequences
Easier: one place to raise the visual bar for every generated project; 6 coherent presets;
adding a 7th preset is a JSON entry. Harder: layout distinctiveness is capped at what tokens +
utility classes can express (no per-screen art direction). Tripwire: if a project needs true
bespoke layout, that is a per-project override on top of the overlay, not a genesis change.
