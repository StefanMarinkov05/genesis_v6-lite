# 085 — Designs: image-pipeline redesign (A4) + Спецификации consolidation (A5)

**Type:** Design / docs (no code or template changes)
**Model:** Opus, high effort, thinking on — the Opus design halves of Groups A4 and A5. The
Sonnet implementation halves are not started here; each design is written to be executed without
re-deriving context.

## Summary

Two design deliverables, each following the ADR-010 pattern (a thin ADR index entry + a full
`docs/explanation/*.md` single-source-of-truth doc), produced only after mapping the real code —
both designs are grounded in verified defects, not assumptions.

### A4 — image pipeline (`docs/explanation/image-pipeline.md` + ADR-011)

Diagnosed three real defects first: (1) the download "cache" (`SeedStage.DownloadImageAsync`'s
`File.Exists` short-circuit) lives under the per-run `out/run_<timestamp>/` folder (changelog 019),
so it never survives a regenerate; (2) profile pictures are seeded as raw `picsum.photos` URLs and
**4 of 5 render sites prepend `/`, so seeded avatars are broken today**; (3) `/images/avatar-default.png`
is referenced by 5 views but never emitted (404), and product download failures write a 1×1 PNG.
Design: a persistent `{genesisRoot}/.image-cache/` outside `out/`; shipped **SVG** fallbacks
(`avatar-default.svg`/`product-default.svg`) copied into each `wwwroot/images/` by a new
`ScaffoldStage.CopyStaticAssets`; local-only PFP (identity seeder seeds `null` → generic fallback,
making every path relative and collapsing the 5 render sites to one code path); product-default on
empty. Distinct-per-user real avatars explicitly ruled out (privacy + over-engineering).

### A5 — Спецификации consolidation (`docs/explanation/specifications-consolidation.md` + ADR-012)

A single consolidated admin page superseding "Характеристики", holding three tab flavours:
full-CRUD tabs for atomic reference models (new `isSpecification` flag parallel to `isLookup`),
read-only tabs for join models (existing `isLookup`), and a Feedbacks tab. Atomic-reference CRUD
**reuses the existing scaffolded `{Model}Controller` as a hidden write sub-controller** (nav
suppressed, consolidated Index links Create/Edit/Delete with a `returnUrl`) — no scaffolded-
controller surgery. A generic **auto-derived filter convention** (manyToOne→record select,
`UserId`/`AuthorUserId`→user select, enum→select, discrete rating int→1–N select) rendered by a
shared `_admin_filter_bar.sbn`, threaded through the existing sort/pagination `asp-route-*` (the
top regression risk). Explicit 5-step sequence with the **rename performed last**; documented the
Scriban `||`-returns-boolean, BG-residue-identifier, and `Create\(…\)`-regex traps.

## Verification

Design-only — no build/test impact. Both designs were written against the real code (SeedStage,
identity_seeder.sbn, the 5 avatar render sites, admin_table.sbn, characteristics_controller.sbn,
InjectAdminSupervisorSurface, the rating/Review model, the schema `images` block), with each claim
traced to a file/line rather than assumed. Implementation verification (full `run --all`
regenerate + browser eyeball) belongs to the Sonnet halves.

## Affected files

- `docs/explanation/image-pipeline.md` (new), `docs/adr/adr-011-image-pipeline-cache-and-fallbacks.md` (new)
- `docs/explanation/specifications-consolidation.md` (new), `docs/adr/adr-012-specifications-consolidation-and-filters.md` (new)
- `docs/explanation/architecture-decisions.md` (index the two new ADRs)
- `CLAUDE.md` (ADR count 11→13; two new router rows)
- `tasks.md` (A4/A5 Opus design halves marked done)
