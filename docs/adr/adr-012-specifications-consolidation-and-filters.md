<!-- last-verified: 33c4fd4265056f5f769086261ff358cad3d31c5d -->
# ADR-012: Specifications page consolidation (two flavours) + a generic admin filter convention

*The page is labelled "Спецификации" (Bulgarian for "Specifications") in the generated apps' UI.*

**Status**: accepted (design); not yet implemented

**Context**: The admin surface handles small models inconsistently. Join/lookup models
(`isLookup`) were consolidated read-only into "Характеристики" (changelog 069), but atomic
reference models (Brand, Category) keep their own full-CRUD controllers **and** their own nav
entries — the exact clutter Характеристики was meant to remove, left out because they need real
CRUD. Feedbacks (Review) have no admin page. Nothing anywhere is filterable. A5 asks to (a) fold
Brand/Category into one consolidated CRUD page, (b) add Feedbacks + user/product/rating filters
there and on Client/Product/Details, (c) apply the same filter pattern across the admin listings,
and (d) rename Характеристики → "Спецификации" last.

**Decision**: introduce a single consolidated admin page, **Спецификации**, that supersedes
Характеристики and carries three tab flavours: full-CRUD tabs for **atomic reference** models (a
new `isSpecification` flag, parallel to `isLookup`), read-only tabs for **join/lookup** models
(existing `isLookup`), and a read-only **Feedbacks** tab. Atomic-reference CRUD **reuses the
existing scaffolded `{Model}Controller` as a hidden write "sub-controller"** (nav suppressed like
`isLookup`; the consolidated Index links Create/Edit/Delete at it with a `returnUrl` back to
Спецификации) — no scaffolded-controller surgery. Filtering is a **generic auto-derived
convention**: manyToOne relations → record `<select>`, `UserId`/`AuthorUserId` → user `<select>`,
enum/status → value `<select>`, discrete rating int → 1–N `<select>`; rendered by a shared
`_admin_filter_bar.sbn`, applied as `.Where` on nullable Index params, and **threaded through the
existing sort/pagination `asp-route-*`** so a sort click doesn't drop the filter. The rename is
performed **last**, once the consolidated page renders everything, to avoid breaking references
mid-refactor.

**Full design**: `docs/explanation/specifications-consolidation.md` (taxonomy table, controller
architecture, filter mechanics, the explicit 5-step sequence, and the Scriban/regex/BG-residue
traps). This ADR is the index entry; that doc is the single source of truth.

**Consequences**: one admin surface for every small model, mixed read-only/CRUD by declared tab
flavour; a single reusable filter mechanism instead of per-page bespoke filters; scaffolded CRUD
stays authoritative for mutations (no fragile regex rewrites). Tripwires: (1) filter params must
survive sort/pagination links or filtering silently breaks; (2) the `||`-returns-boolean Scriban
pitfall (changelogs 057/069) recurs in any new label logic — use explicit `if/else`; (3) do the
rename only after the consolidated page is complete.
