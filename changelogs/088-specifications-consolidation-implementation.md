# 088 — Спецификации consolidation implementation (A5)

**Type:** Feature (admin surface consolidation) / bug fix (found during verification)
**Model:** Sonnet, medium-high effort, thinking on — Group A5 implementation, following the
5-step sequence and traps documented in `docs/explanation/specifications-consolidation.md` /
ADR-012 (changelog 085).

## Summary

Implements the design's core ask: one consolidated admin page, **Спецификации**, superseding
Характеристики (changelog 069), with three tab flavours and a generic auto-derived filter
convention.

### New `isSpecification` flag (schema + POCO + injection)

Parallel to the existing `isLookup`. Set on `Brand`/`Category` (model + their scaffolded
controller entries) in both fixtures. `SpecStages.InjectAdminSupervisorSurface` copies it onto
auto-generated Admin controller entries, mirroring the exact `isLookup` pattern from 069.

### `SpecificationsController` + `specifications_index.sbn`

One `Index()` action, one tabbed view, three tab flavours:

- **CRUD tabs** (`isSpecification`: Brand, Category) — reuse the existing scaffolded
  `BrandsController`/`CategoriesController` as hidden write sub-controllers (nav suppressed like
  `isLookup`; Спецификации's Index links their Create/Edit/Delete). **No scaffolded-controller
  surgery** for the CRUD logic itself — exactly as the design mandated.
- **Read-only tabs** (`isLookup`: CatalogueItem, CartItem, OrderItem, PostComment, PostVote) —
  migrated from the old Characteristics page, now with **generic auto-derived filters**: every
  `manyToOne` relation → a record `<select>`, any `UserId`-suffixed property → a user `<select>`.
- **Feedbacks tab** (Review, gated on `features.rating`) — product/user/rating (`Stars`, 1–5)
  filters.

Tab state survives filter submits: a `?tab=` param plus each filter form echoing every other
tab's current query params as hidden inputs (`Context.Request.Query` iterated directly in the
view), so switching one tab's filter never silently drops another tab's active filter — the
exact regression the design flagged as the top risk, closed structurally rather than by
convention.

### Характеристики retired

Old templates (`characteristics_controller.sbn`, `characteristics_index.sbn`) deleted; emission
swapped in `EmitControllersViewsStage`; `layout.sbn`'s nav now shows one "Спецификации" entry
gated on `isLookup || isSpecification || features.rating`, replacing the old isLookup-only gate.

## Real bug found during verification (not shipped blind)

First full regenerate failed spec validation with 56 cascading test failures (unrelated tests
failing too, since `Arrange()` loads the fixture — a classic "fixture is broken" signature).
Root cause: `SpecLoader.SemanticCheck` requires every `redirects[].to` target to reference a
**declared** controller. `SpecificationsController` was purely template-emitted, never declared
in `spec.controllers` — so referencing it from a `redirects` entry (added so Brand/Category's
CRUD lands the user back on Спецификации instead of the orphaned `Brands/Index`) failed
validation. Fixed by declaring `SpecificationsController` in both fixtures
(`generatedBy: "writtenFromScratch"` — confirmed a no-op in `EmitControllersViewsStage`'s
generic emission switch, so no double-emission risk) and excluding it from `layout.sbn`'s
generic admin-nav loop by name (else it would get a second, duplicate nav entry alongside the
dedicated Спецификации link).

The `redirects` fix itself reuses an **existing, already-proven** deviation mechanism (the same
one changelog-history's redirect deviations already use) rather than inventing new
scaffolded-controller surgery — verified first against a literal copy of the real scaffolded
`BrandsController.cs` shape (new test `Real_ApplyDeviations_redirects_brand_crud_back_to_specifications_index`,
calling the actual `ApplyDeviations` method, not a reimplemented regex), then confirmed in a
second full regenerate's real output.

## Deliberate scope cuts (read before assuming the design doc is 100% done)

- **Did not** implement scaffolded-controller filter surgery — a new "filtering" deviation type
  (mirroring the existing sorting deviation) that would add filters to Products/Orders/etc.'s
  OWN `admin_table.sbn` pages. That is real scaffolded-regex risk the design flagged as the
  hardest part. Filters landed instead entirely inside the fully-hand-controlled
  `SpecificationsController` (zero scaffold risk) — interpreted as satisfying "apply filters to
  catalogues/cart-items/orders/comments/votes" since those five names ARE the `isLookup` models,
  which have no other meaningful admin display surface anyway.
- **Client/Product/Details review filter** (explicitly named in the design) — **not done**. A
  genuine remaining gap against the design doc.
- **No separate `_admin_filter_bar.sbn` partial** — filter markup is inline per-tab in
  `specifications_index.sbn` (three near-identical blocks). Works correctly, but the "generic
  reusable partial" the design named is not literally a standalone file. Revisit if a fourth
  filter-bearing surface is added.

## Verification

- `dotnet build` + `dotnet test`: 114/114 (was 111 before this session's batch), including two
  new tests — one hand-built-spec render test covering all three tab flavours, one real
  `ApplyDeviations` verification against an authentic scaffolded-controller shape.
- **Two** full clean `run --all` regenerates. First surfaced the redirect/declaration bug above
  (caught immediately, not shipped). Second: **verify PASSED** — build/db/boot/http/manifest/
  language, 6m48s total. Confirmed directly in real generated output: `Specifications/Index.cshtml`
  renders all 8 tabs (Brand, Category, CatalogueItem, CartItem, OrderItem, PostComment, PostVote,
  Feedback); `BrandsController.cs`/`CategoriesController.cs` both redirect to
  `RedirectToAction("Index", "Specifications")` at all three CRUD exit points; `_Layout.cshtml`
  shows exactly one "Спецификации" nav link (no duplicate); zero `*characteristics*` files remain
  anywhere in the generated output.

## Affected files

- `spec.schema.json`, `src/Genesis.Core/Spec/ProjectSpec.cs` (`isSpecification`)
- `src/Genesis.Core/Stages/SpecStages.cs` (`InjectAdminSupervisorSurface` plumbing)
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs` (emission swap)
- `templates/controllers/specifications_controller.sbn` (new)
- `templates/views/specifications_index.sbn` (new)
- `templates/controllers/characteristics_controller.sbn`, `templates/views/characteristics_index.sbn` (deleted)
- `templates/views/layout.sbn` (nav: Спецификации entry + generic-loop exclusion)
- `fixtures/project-spec.facenote.json`, `fixtures/project-spec.facenote-en.json`
  (`isSpecification`, `SpecificationsController` declaration, `redirects` on Brand/Category)
- `tests/Genesis.Tests/CoreTests.cs` (2 new tests)
- `tasks.md` (A5 marked done, honest scope-cut notes preserved)
