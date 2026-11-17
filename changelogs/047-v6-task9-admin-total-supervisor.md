## 047 — Task 9: Admin as total supervisor (every model, full CRUD) — Package D

**Type:** feature
**Model:** Opus — per the v6 task-breakdown's own model routing (task 9 = Opus, generator-level architectural change, highest risk).
**Date:** 2026-07-10
**Task completed:** v6 Task 9 (admin sees/manages every model, not a hand-picked subset)

### Approach: inject the admin surface into the expanded spec (generator-level)

Rather than hand-listing admin controllers per spec, the admin area is now **data-driven over every
model**. New `SpecExpandStage.InjectAdminSupervisorSurface(node)` runs right after feature expansion and,
for every model that doesn't already have an Admin controller, appends to the expanded spec:
- a `ControllerSpec` `{ name: <Plural>Controller, area: Admin, model: <M>, generatedBy: scaffold }`
- an `adminTable` `PageSpec` `Areas/Admin/Views/<Plural>/Index.cshtml` with edit/details/delete row buttons.

Because these are injected as **first-class spec entries before the lock is written**, every downstream
consumer picks them up with zero extra wiring:
- the existing scaffold + transform pipeline (ScaffoldController → InjectArea → soft-delete → BG residue
  → strip system fields → deviations) generates each controller + full CRUD views;
- the layout's admin dropdown (which already iterates `spec.controllers` where area==Admin) lists them;
- `admin_table.sbn` renders each Index; `ModelForPage`/`ControllerForPage` resolve them by model name;
- verify's `DiscoverRoutes` covers them (best-effort authed fetch).

New `SpecExpandStage.Pluralize` handles controller/route names (Category→Categories, Post→Posts,
CatalogueItem→CatalogueItems, Box→Boxes).

### Result (verified end-to-end)

For FaceNote, 10 admin controllers were auto-added and scaffolded on top of the spec's own 3:
Catalogues, CatalogueItems, CartItems, Orders, OrderItems, Posts, PostComments, PostVotes, Reviews, Follows.
`run --all` → **verify PASSED: build, db, boot, http, manifest, language**. All 10 scaffolded controllers +
their CRUD views compile, the app boots with them, and the admin dropdown lists every model.
(emit-controllers-views grew to ~314s due to the extra scaffolds — expected.)

### Notes / boundaries

- Models already managed in Admin by the spec (Product/Brand/Category) are left untouched (no duplicate).
- `{Model}Image` side-tables are companion classes emitted inside model.sbn, not `spec.models` entries,
  so they're not scaffolded (image management stays in the dedicated ProductImages UI, task 14).
- Cosmetic (pre-existing, not a regression): admin dropdown labels use the English model plural (route
  segment), same as the spec's original Products/Brands/Categories links. A model→Bulgarian-label map for
  the admin nav would be a separate enhancement (models carry per-property displayNames, not a model name).
- 2 unit tests (Pluralize forms; injection skips covered models, adds controller+page for the rest). 79/79.
