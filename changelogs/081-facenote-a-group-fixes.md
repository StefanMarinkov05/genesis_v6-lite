# 081 — FaceNote "A" task group: order validation, status display, image-upload hint, follow button

**Type:** Feature / change (bundle of well-scoped fixes; several turned out to have a real-bug
component once investigated — see per-item notes)
**Model:** Sonnet, medium effort — this is exactly the "Group A1 + A3" batch scoped as
Sonnet-appropriate in the prior grouping/model-assignment pass; Group A2 (Identity English leaks,
marked Sonnet-*high*) and Groups A4/A5 (image pipeline redesign, Спецификации consolidation —
marked Opus-design-first) were deliberately deferred, not attempted here.

## Summary

Five items from Stefan's task batch, all touching the generated FaceNote app (not the GUI):

1. **Order checkout now requires receiver name + delivery address.** `feature-templates/orders.json`
   had both fields as `nullable: true` with no validation — genuinely optional despite the form
   already collecting them. Removed nullable, added `[Required]` with Bulgarian error messages
   (matching the existing `UserId` validation pattern). **Real bug found while fixing**: the
   `Checkout` action manually constructs the `Order` from `[FromForm] string?` parameters — it never
   binds `Order` directly, so the model's own `[Required]` attribute was **never actually checked**.
   Added explicit server-side validation in `CartController.Checkout` (reject
   whitespace-only input, `TempData["Error"]`, redirect back) plus `required` on the two form inputs
   for client-side feedback.

2. **Order status now shows Cyrillic, not the raw enum name.** The spec already had correct
   `[Display(Name="Нова")]`-style attributes on every `OrderStatus` member (`model.sbn` emits these
   correctly) — the bug was in the *views*: `cart_order_details.sbn` and `cart_orders.sbn` used raw
   `@Model.Status`/`@order.Status` interpolation, which prints `.ToString()` (the enum member name),
   not the Display attribute. `admin_table.sbn` already used the correct `Html.DisplayFor` pattern
   elsewhere in the codebase — copied it into both Client-area order views.

3. **Admin/Products/Create has no upload control — by design, not a bug, but undiscoverable.**
   Gallery-style images (`model.Images`, "multiple" mode) are managed via a separate
   `{Model}ImagesController` (Task 14) linked from the Index page's "Снимки" button — there's no
   entity Id yet on Create for images to attach to, so inline upload isn't possible without a much
   bigger rework of the scaffolded Create action. Verified the separate upload page itself
   (`admin_product_images.sbn`) already works correctly. Added `InjectImagesCreateHint` (new
   post-scaffold regex injection, same idempotent-guard pattern as the existing
   `InjectImageRemoveOption`) — a short alert on the scaffolded Create view pointing at the
   post-save "Снимки" step, so the two-step flow is discoverable instead of looking broken.

4. **Admin can no longer delete another admin — already fully implemented, no change needed.**
   Checked `users_controller.sbn`'s `Delete` action directly: it already blocks deleting yourself
   *and* deleting any user in the admin role (`targetRoles.Contains(admin_role)`), each with a
   `TempData["Error"]` message. Nothing to fix here. **Related bug found while checking**:
   `TempData["Error"]` was being *set* by this action (and only this action) but never *read*
   anywhere — the block worked, but the user got silently redirected with zero explanation. Added a
   dismissible Bootstrap alert to `layout.sbn` rendering `TempData["Error"]`/`["Success"]`, so this
   (and the new checkout validation's error) actually reaches the user.

5. **Can't follow people from `Client/Profiles/Public/*` — the button never existed.**
   `FollowsController.Toggle` (the working follow/unfollow endpoint) was fully implemented;
   `user_profile_content_partial.sbn` renders follower/following *lists* but the public profile page
   itself had no Follow/Unfollow control at all. Added `IsOwnProfile`/`IsFollowedByViewer` to
   `UserProfileViewModel`, extended `UserProfileService.BuildAsync` with an optional `viewerUserId`
   parameter to compute them, passed the current viewer's id from `ProfilesController.Public`
   (`_userManager.GetUserId(User)`, correctly null for anonymous), and added the button to
   `profiles_public.sbn` (hidden on your own profile, hidden when logged out).

## Verified

Full `run --all` against the BG fixture, clean `out/` first (no stale state to skip stages).
`verify` **PASSED** (build, EF migrate, boot, HTTP smoke checks including a real admin login,
English-leak scan). Beyond the green pipeline, directly inspected the generated output for every
item: `Order.cs`'s `required string` + `[Required]` fields, the enum's `[Display]` attributes,
`Html.DisplayFor` in both order views, the `Checkout` validation, the layout's `TempData` alert
blocks, the `images-create-hint` div on `Admin/Products/Create.cshtml`, the admin-delete-block logic
in `UsersController.cs`, and the full follow-button wiring across `UserProfileViewModel.cs`,
`UserProfileService.cs`, `ProfilesController.cs`, and `Profiles/Public.cshtml` — all present and
correct, not just assumed from a green build.

Two tool-level tests broke from the `BuildAsync` signature change (asserted the old 2-parameter
form) — updated both to the new 3-parameter signature and the new `IsOwnProfile`/`IsFollowedByViewer`
assignment lines. `dotnet test`: 111/111 passing.

**Not done**: the EN fixture variant wasn't separately regenerated (shares the same
`feature-templates/orders.json`/templates, so the fix applies identically — the new Required error
messages are Bulgarian-only, consistent with `orders.json`'s pre-existing Bulgarian-only validation
text on every other field, not a new regression).

## Affected files

- `feature-templates/orders.json` (Required validations)
- `templates/views/cart_order_details.sbn`, `cart_orders.sbn` (`Html.DisplayFor`)
- `templates/controllers/cart_controller.sbn` (server-side checkout validation)
- `templates/views/cart_index.sbn` (`required` form attributes)
- `templates/views/layout.sbn` (TempData Error/Success alerts)
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs` (`InjectImagesCreateHint`)
- `templates/models/user_profile_view_model.sbn`, `templates/controllers/user_profile_service.sbn`,
  `templates/controllers/profiles_controller.sbn`, `templates/views/profiles_public.sbn` (follow button)
- `tests/Genesis.Tests/CoreTests.cs` (two assertions updated to match)
