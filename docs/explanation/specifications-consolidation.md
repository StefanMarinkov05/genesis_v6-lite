<!-- last-verified: 33c4fd4265056f5f769086261ff358cad3d31c5d -->
# Specifications page consolidation + the admin filter convention — design spec

*The page is labelled "Спецификации" (Bulgarian for "Specifications") in the generated apps' UI.*

**Status**: design (Group A5 Opus half). Implementation is the Sonnet half. Index entry:
[ADR-012](../adr/adr-012-specifications-consolidation-and-filters.md). This doc is the single
source of truth for the detail.

## Starting point (verified against the real code)

The admin surface currently has **two** kinds of "small model" handling and **no** filtering:

- **Join / lookup models** — `CartItem`, `OrderItem`, `PostComment`, `PostVote`, `CatalogueItem`
  carry `isLookup: true` (set in their `templates/features/*.json`). Changelog 069 consolidated
  them into a single **read-only** tabbed page, `CharacteristicsController` → `Characteristics/Index`
  ("Характеристики"). Their scaffolded `Admin/{Model}` CRUD controllers still exist and are
  reachable directly; they are just hidden from the admin nav (`layout.sbn` skips
  `c.is_lookup`, shows one "Характеристики" link instead).
- **Atomic reference models** — `Brand`, `Category`: no outgoing FK relations, top-level
  reference data. They have their **own** full-CRUD admin controllers (`BrandsController`,
  `CategoriesController`) **and their own nav entries** — exactly the dropdown clutter that
  motivated Характеристики, but never folded in because they need real CRUD, not read-only.
- **Feedbacks** — `Review` (Отзиви): product-scoped (`$primaryModel` FK), `AuthorUserId`,
  `Stars` (1–5), shown on `product_details`. No admin management page, no filtering anywhere.
- **Filtering**: nonexistent. `admin_table.sbn` has sort + FK-links but no filter bar.

## What A5 introduces

A single consolidated admin page, **"Спецификации"**, that supersedes Характеристики and holds
three tab flavours, plus a **generic, cross-cutting filter convention** applied uniformly.

### 1. Model taxonomy — a second consolidation flavour

Add a new `ModelSpec` / `ControllerSpec` flag **`isSpecification`** (bool), parallel to the
existing `isLookup`:

| Flag | Meaning | Tab behaviour on Спецификации |
|---|---|---|
| `isSpecification: true` | atomic reference model, no outgoing FK (Brand, Category) | **full CRUD** tab |
| `isLookup: true` | join/junction model (CartItem, …) | **read-only** tab (as today) |
| Review (rating feature) | feedback | **read-only + filters** tab |

`isSpecification` is set in the spec on Brand/Category (they are fixture-authored models, so this
is a two-line fixture edit + the schema/POCO field). It plumbs through
`SpecStages.InjectAdminSupervisorSurface` onto the auto-generated Admin controller entry, exactly
as `isLookup`/`displayName` already do (changelog 069 pattern).

### 2. Controller architecture — "index-only page + CRUD sub-controllers"

- **`SpecificationsController`** (Admin area): a single `Index()` that builds one combined view
  model — all `isSpecification` lists (for CRUD tabs), all `isLookup` lists (read-only tabs), and
  the feedbacks list — and renders the tabbed page. This is the renamed/evolved
  `CharacteristicsController`.
- **Per atomic-reference model: reuse the existing scaffolded CRUD controller** as the write
  "sub-controller." `BrandsController`/`CategoriesController` already have Create/Edit/Delete with
  FK dropdowns, soft-delete, and BG residue handling — do **not** re-author them. Instead:
  1. Mark them `isSpecification` → hidden from nav (same skip mechanism as `isLookup`).
  2. The Спецификации CRUD tab renders each row with Edit/Delete buttons and a "Създай" button
     pointing at that controller's actions, passing `returnUrl` back to `Specifications/Index`
     so the user lands back on the consolidated page after a write.

This is the cleanest reading of "one index-only page/controller with per-model create/edit/delete
sub-controllers": the consolidated page owns the *index*; the scaffolded controllers own the
*mutations*; nav shows only the consolidated page.

### 3. The generic admin filter convention (the reusable, cross-cutting piece)

Filters are **auto-derived**, not hand-listed per page, so one mechanism covers every target the
task names (Feedbacks, catalogues, cart-items, orders, comments, votes) and Client/Product/Details.

A listing's filter set is derived from its model:

- every `manyToOne` relation → a `<select>` of the target's records (`Id` → display value),
  **using the same real-controller lookup `admin_table.sbn` already does** (never naive-pluralize
  — `Category`→`Categories`);
- the special string FK `AuthorUserId` / any `UserId` → a **user `<select>`** (the "user" filter);
- any enum/`status` property → a `<select>` of its values;
- a discrete rating int (`Stars`, or an int with a `Range 1..N` validation) → a **1–N `<select>`**
  (the "rating" filter).

Mechanics:

- Index actions accept nullable filter params (`?filterProductId=`, `?filterUserId=`,
  `?filterStars=`, …) and apply one `.Where(...)` per supplied value.
- A shared **`_admin_filter_bar.sbn`** partial renders the controls + "Филтрирай" / "Изчисти",
  preserving current values.
- **Integration trap (call out for the implementer):** the filter query params must be threaded
  into the existing sort and pagination links in `admin_table.sbn` (their `asp-route-*`), or
  clicking a column sort / next page silently clears the active filter. This is the single most
  likely regression.

Feedbacks (Спецификации tab) and Client/Product/Details get the same bar: on product details the
reviews list gains a rating/user filter scoped to that product.

## Sequencing — the task is explicit that the rename comes LAST

Renaming "Характеристики" → "Спецификации" first would break references mid-refactor. Order:

1. Add `isSpecification` (schema + POCO + `InjectAdminSupervisorSurface` plumbing); mark
   Brand/Category; hide them from nav.
2. Build `SpecificationsController` + `specifications_index.sbn` as a **new** page/nav entry,
   **alongside** the still-present Характеристики (CRUD tabs for Brand/Category first).
3. Build the generic filter convention (`_admin_filter_bar.sbn` + `admin_table.sbn` filter
   derivation + Index query params + sort/pagination param threading). Apply to Admin
   catalogues / cart-items / orders / comments / votes and to Client/Product/Details.
4. Add the Feedbacks (Review) read-only tab + its filters to Спецификации.
5. **LAST:** fold the read-only `isLookup` join tabs into the Спецификации page and retire
   Характеристики — rename the controller/view/nav label/identifier `Characteristics` →
   `Specifications` and "Характеристики" → "Спецификации" only once the consolidated page renders
   everything. Then delete the old page.

## Traps the implementer must respect (from project history)

- **`||` returns a boolean in Scriban**, not the left operand — `label = c.display_name || c.name`
  silently prints `"true"`. Bit us in changelogs 057 and 069. Use explicit `if/else`.
- **BG residue map corrupts C# identifiers** containing `Email`/`Password` substrings (changelog
  070) — irrelevant to Спецификации prose but relevant if any injected code carries such names.
- **`Create\([^)]*\)` regex can't backtrack past `[Bind("…")]`'s paren** (changelog 064) — matters
  only if you inject into the scaffolded Brand/Category controllers rather than reusing them; the
  design reuses them precisely to avoid scaffolded-controller surgery.
- **FK column target-controller lookup** must reuse `admin_table.sbn`'s real-controller resolution
  for both the link columns and the new filter dropdowns.

## Spec / seed impact

- New `isSpecification` bool on `ModelSpec` + `ControllerSpec` (schema + POCO), set on
  Brand/Category in the fixture(s). No seed-data shape change.
- No new required fields for the filter convention (auto-derived). An optional `filters` opt-out
  list on a controller can be added later if auto-derivation over-includes; not needed for MVP.
