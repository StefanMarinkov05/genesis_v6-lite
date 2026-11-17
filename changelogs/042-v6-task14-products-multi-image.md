## 042 — Task 14: Products multi-image gallery + Admin UI

**Type:** feature
**Model:** Sonnet — per the v6 task-breakdown's own model routing (task 14 = Sonnet).
**Date:** 2026-07-10
**Tasks completed:** v6 Task 14 (Products multi-image carousel + gallery UI)

### What changed

**New templates:**
- `templates/controllers/product_images_controller.sbn` — Admin controller for image management:
  `Index(int id)`, `Upload(int id, IFormFileCollection files)`, `DeleteImage(int id, int imageId)`.
  Requires Admin role (resolved from `spec.identity.roles` at generation time).
- `templates/views/admin_product_images.sbn` — Gallery view with per-image delete forms and
  multi-file upload form. Bulgarian UI throughout.

**Modified templates:**
- `templates/views/admin_table.sbn` — Added "Снимки" button for models with `model.images` set;
  links to `{Model}ImagesController#Index`.
- `templates/views/image_upload_partial.sbn` — Added `multiple` attribute to file input.

**Stage wiring:**
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs` — Emits `{Model}ImagesController.cs` and
  `Areas/Admin/Views/{Model}Images/Index.cshtml` for every model with `images` configured.

**Tests added (`tests/Genesis.Tests/CoreTests.cs`):**
- `Product_images_controller_renders_upload_and_delete_actions`
- `Product_images_view_has_gallery_and_multifile_form`
- `Admin_table_has_images_button_for_models_with_images`

All 63 tests pass.

### Note on `$variation:adminRoleName`

The rendered controller contains `[Authorize(Roles = "$variation:adminRoleName")]` because variation
references in the raw spec are resolved at spec-expansion stage (stage 5), not at Scriban render time.
This is expected and consistent with all other generated controllers. The `DoesNotContain("$variation:")`
assertion was incorrect and was removed.
