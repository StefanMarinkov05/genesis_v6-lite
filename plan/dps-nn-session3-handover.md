# dps-nn - Session 3 handover (deferred change-spec)

Written by the Opus session after landing B4, A6, C4-bounded (later also A5, A4).
> UPDATE (later): A5, A4 BUILT. Then A9 (public profile page + author link) BUILT. A1 add-to-cart BUTTON
> BUILT (was a dead stub). A8 author-delete found ALREADY DONE. Genuinely remaining after all sessions:
> A1 client product CRUD (scaffold transform), A8 image upload (per-model image table + migration), and a
> live-run confirm of A7/A11. Those three are the ONLY items left and all need a live compiler/scaffold.
 Everything below was **NOT** built this
session, on purpose. The dividing line is verifiability: the items done this session were either
engine-side plain C# (B4, C4) or extensions of **template-authored** controllers whose exact output I can
read (A6). Everything below either rewrites **scaffolded** controllers (whose output I can't see without a
live SDK-10 `aspnet-codegenerator` run), touches the **core card grid** used by every page, changes the
**strict spec schema**, or **cross-links** many templates. Those need one real scaffold to calibrate against
- which is already step 1-2 of state.md "Next (Stefan, locally)". Shipping them blind would add an
un-bisectable pile on top of an already-uncompiled engine.

Recommended order once you have an SDK: **compile + run existing tests first**, then one real FaceNote run,
then take these in the order listed (cheapest/safest first).

---

## First: verify what's already done (no code, just a run)
- **A7/A11 is basically complete.** `users_controller.sbn` already enforces AdminCannotDeleteSelf
  server-side (Delete action: `id == currentUserId` -> TempData error + redirect). `admin_table.sbn`
  already renders an "Изтриване" Delete button per row, and scaffolded admin controllers get a
  Delete/DeleteConfirmed pair. Action needed: **confirm on a live run** that (a) every admin table's
  Delete posts correctly, (b) the self-delete attempt shows the BG error. If yes, close A7/A11. If a
  specific admin entity lacks delete, it's a scaffold-transform gap - handle like A1 below.

## A5 - public catalogues page (TRACTABLE, template-controller; spec only for schema-safety)
`catalogues_controller.sbn` is template-authored, so the controller side is low-risk. The only real risk is
the `isPublic` flag, which must be added to the strict schema (`additionalProperties:false` -> unknown key =
hard fail) and to `feature-templates/catalogues.json` model.
1. Add `IsPublic` (bool, default false) to the Catalogue model in `feature-templates/catalogues.json`.
2. Allow `features.catalogues.public` (or a model prop) in `spec.schema.json` - **validate the schema edit**
   by running `SpecValidationTests.Fixture_spec_loads_and_validates` after.
3. In `catalogues_controller.sbn`: add `[AllowAnonymous] public async Task<IActionResult> Public()` listing
   `Catalogues.Where(c => c.IsPublic).Include(items...)`; add a `PublicDetails(int id)` guarded by IsPublic.
4. New view `templates/views/catalogues_public.sbn` (+ emit in EmitControllersViewsStage catalogues block).
5. Nav link in `layout.sbn` (public, no auth).
Estimate: ~2h with a compiler. No scaffold dependency.

## A1 - client product CRUD + add-to-cart on cards (HIGH risk: core card grid + scaffold transform)
Two parts:
- **Add-to-cart button on cards**: edit `card_grid.sbn` to render, when `features.orders`, a POST form to
  `Cart/Add` with `asp-route-itemId="@item.Id"` on each card. Low risk (template), but `card_grid.sbn` is on
  EVERY cardGrid page - regressions are broad, so diff carefully and re-run the EN-leak gate (now B4-wide).
- **Client product CRUD**: today client sees a read-only catalog. Giving clients create/edit/delete over
  "their" products means a scaffolded Client-area products controller + ownership checks (see the existing
  ownership pattern in feature controllers) + `[Authorize]`. This is a **scaffold-transform** addition: needs
  the real `aspnet-codegenerator` output for the Client controller to calibrate the regex anchors, exactly
  like the existing `InjectReviewLoadingIntoDetails`/`ApplySoftDelete` transforms. Do it against one live
  scaffold; mirror the loud-on-missing-anchor + idempotent-marker style of `ApplyDeviations`.
Estimate: ~1 day with a compiler + one reference scaffold.

## A4 - catalogue reorder + Position column (MEDIUM: generated-app migration)
1. Add `Position` (int) to the CatalogueItem model in `feature-templates/catalogues.json`.
2. `catalogues_controller.sbn`: `MoveUp(int id)` / `MoveDown(int id)` swapping Position with the adjacent
   item in the same catalogue; order the picker/index by Position.
3. `catalogues_index.sbn`: up/down buttons (POST) per item.
4. Because this adds a column, a migration is regenerated in the generated app - fine (pipeline runs
   `ef migrations`/`database update`), but confirm the seed still inserts valid Positions.
Estimate: ~half day. No scaffold dependency, but has a DB-shape change so verify migrations.

## A8 - post image upload + author delete (MEDIUM: template controller + ImageService wiring)
`posts_controller.sbn` is template-authored (good). Two parts:
- **Author delete**: add `Delete(int id)` that checks `post.AuthorUserId == _userManager.GetUserId(User)`
  before deleting; button in `posts_index.sbn`/`posts_details.sbn` shown only to the author. Low risk.
- **Image upload**: reuse `_ImageUploadPartial` + `ImageService` (already emitted when a model has images).
  Requires the Post model to have an image relation; wire `ImageService.AddImagesAsync` into the Create POST.
  Confirm the per-model `{Model}Image` table pattern is generated for Post.
Estimate: ~half day.

## A9 - public user page + item/author linking (HIGH: cross-links many templates)
A new `Areas/Client/Controllers/ProfilesController` (or extend Users) exposing a read-only public page per
user, then linking to it from: post author names (`posts_index`/`posts_details`), catalogue item ->
product Details, review author names. This is the most cross-cutting item (touches 4-5 templates + a new
controller + nav) and benefits most from doing A1/A8 first so the link targets exist. Do last.
Estimate: ~1 day.

---

### Standing constraints for whoever picks these up
- Match house style: transforms over scaffolded C# are **idempotent** (marker check) + **loud** (throw on
  missing anchor); templates are Scriban with BG-only visible text; every visible string is Bulgarian (the
  B4 gate now scans all routes, so an EN leak on any feature page fails verify).
- After any template change, the pipeline's per-stage `dotnet build` gates + the `verify` gate are your real
  feedback loop. Use them - this repo is authored blind between runs, so a green `verify` is the signal.
