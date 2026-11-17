# Product ownership auto-assign + client-side sell flow (item 18)

**Type:** Feature / change

## Summary
`Product.UserId` already existed in the spec (required string, `displayName: "Продавач"`) but
nothing populated it - the Admin Create/Edit forms showed it as a raw required text box the admin
had to hand-type a GUID into. Separately, a prior session had deliberately stripped Create/Edit/
Delete from the Client-area Products controller ("public catalogue is read-only; management is in
Admin" - see the removed `removalReason`). Stefan's ask reverses that: any logged-in user should be
able to list, edit, and delete their own products.

## Techniques
- **New generic transform** `EmitControllersViewsStage.InjectOwnerAutoAssign` (post-scaffold regex
  surgery, same family as the existing `StripSystemFieldsFromViews`/`InjectReviewLoadingIntoDetails`):
  - Triggers for any model with a raw ownership `UserId` string scalar (`OwnerProperty` helper -
    `Name == "UserId" && Type == "string" && !ForeignKey`, i.e. not a formal relation nav).
  - **Create**: stamps `entity.UserId = _userManager.GetUserId(User)!;` right inside the
    `ModelState.IsValid` branch, before `_context.Add(...)`.
  - **Edit**: re-fetches and re-stamps the ORIGINAL `UserId` onto the freshly-bound object before
    `_context.Update(...)` - it was stripped from `[Bind]` (see below), so the incoming object's
    `UserId` is null/default, and `_context.Update` would otherwise blank the real value in the DB.
  - **Client-area only**: also adds a `Forbid()` ownership guard (owner-or-admin) to Edit and
    `DeleteConfirmed` - the same idiom already hand-written in `posts_controller.sbn`'s Delete
    action, just generated instead of hand-written since these controllers are scaffolded.
  - Injects `UserManager<ApplicationUser>` into the controller's constructor when not already
    present (same injection snippet already used by `InjectReviewLoadingIntoDetails`).
- `StripSystemFieldsFromViews`'s `fields` list now also includes `"UserId"` when the model has an
  `OwnerProperty` - reuses the EXISTING generic [Bind]-list + form-field stripping mechanism
  unchanged, just widened its input set. No new stripping logic needed.
- `fixtures/project-spec.facenote.json`: Client `ProductsController`'s `removedActions` cleared
  (was `["Create","Edit","Delete"]`), `removalReason` dropped - the scaffolder now keeps those
  actions and their views.
- **Seller display**: `InjectReviewLoadingIntoDetails`'s injected Details-action code (already
  loads reviews/authors for Product's rich Details page) now also resolves the listing's seller
  (`_userManager.FindByIdAsync(product.UserId)`) into `ViewData["SellerName"]`.
  `product_details.sbn` shows "Обявено от {name}" linking to the seller's public profile.

## Bug caught mid-implementation
First version of the Create-action regex used `Create\([^)]*\)` to match up to the method's closing
paren - but the scaffolded signature is `Create([Bind("...")] Product product)`, and `[Bind(...)]`
has its OWN closing paren before the method's. `[^)]*` (a character class explicitly excluding `)`)
cannot backtrack past that inner paren, so the whole pattern silently failed to match and Create's
auto-assign never fired - caught by inspecting the actual generated Admin controller after a live
run (`grep -c "// owner auto-assign"` returned 1, not 2 as expected), not by build/test (which
can't detect a silently-skipped regex injection). Fixed by switching to a lazy dot-all
`Create\([\s\S]*?\)` that can backtrack past the inner paren to find the real method close.

## Verification
- `dotnet build` + `dotnet test`: 111/111.
- Full clean `run --all` BG regenerate, verify stage PASSED. Directly inspected generated output:
  Admin `ProductsController.cs` has 2 owner-auto-assign injections (Create+Edit); Client
  `ProductsController.cs` has Create/Edit/Delete actions with the Forbid ownership guard on
  Edit+DeleteConfirmed; `Areas/Client/Views/Products/{Create,Edit,Delete}.cshtml` all exist;
  `UserId` confirmed absent from the Create/Edit form HTML (stripped); product Details page shows
  "Обявено от {seller name}" linking to the seller's public profile.

## Affected files
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
- `fixtures/project-spec.facenote.json`, `fixtures/project-spec.facenote-en.json`
- `templates/views/product_details.sbn`
