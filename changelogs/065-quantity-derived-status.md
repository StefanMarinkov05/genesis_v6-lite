# Product Status derived from Quantity (item 19)

**Type:** Feature / change

## Summary
Product's `Status` enum was hand-set by whoever filled the Create/Edit form. Stefan wants it
computed automatically from a new `Quantity` field instead - never asked for directly.

## Techniques
- Added `Product.Quantity` (int, `displayName: "Наличност"`, `Range(0, 1000000)`) to the fixture.
- **New generic helper** `DerivedStatusProperty(model)`: a model has derived status when it has
  both a `Quantity` int and a `Status` enum property.
- `StripSystemFieldsFromViews` extended again: `Status` is now stripped from Create/Edit **forms
  and `[Bind]` only** - unlike `UserId`/audit fields, it must stay VISIBLE on Details (both admin
  and client), just not hand-settable. Required splitting the existing single `fields` list into a
  "form-only" subset vs. the "form + client-display" subset the method already had.
- **New transform** `InjectQuantityDerivedStatus`: computes `entity.Status = entity.Quantity > 0 ?
  {Model}Status.{first enum value} : {Model}Status.{last enum value}` right after the (now
  Status-less) bound object enters the valid branch, in both Create and Edit - same first/last
  convention `cart_controller.sbn` already used for its purchase-flow flip. Runs AFTER
  `InjectOwnerAutoAssign` and correctly detects/preserves that method's own injected line when
  both fire on the same controller (Product has both an owner and a derived status).
- `templates/controllers/cart_controller.sbn`: the purchase-flow status flip now checks whether the
  primary model ALSO has `Quantity` - if so, decrements it (`Math.Max(0, bought.Quantity -
  item.Quantity)`) and re-derives `Status` from the new quantity, instead of unconditionally
  jumping straight to the "sold" enum value. Falls back to the old direct-flip behavior for any
  future orders-enabled model without `Quantity` (backward compatible).
- Seed data: added `Quantity` values to all 24 Product rows in the local (gitignored)
  `prompts/in/seed-response.json` used by live pipeline runs, AND separately to the git-tracked
  unit-test canned fixture `fixtures/seed-response.facenote.json` (15 rows, discovered via a test
  failure - it's a distinct, older canned copy used only by `SeedSchemaTests`, not the file the
  live pipeline reads).

## Verification
- `dotnet build` + `dotnet test`: 111/111 (one seed-schema test genuinely caught the new required
  `Quantity` field missing from the canned test fixture - fixed by adding it, not by weakening the
  test).
- Full clean `run --all` BG regenerate, verify PASSED. Directly inspected generated output:
  `[Bind("Id,Title,Description,Price,Quantity,BrandId,CategoryId")]` (Status correctly absent,
  Quantity present) on both Admin+Client Create/Edit; `product.Status = product.Quantity > 0 ? ... :
  ...` injected in all 4 action bodies (2 controllers x Create+Edit); Status still renders via
  `Html.DisplayFor`/`DisplayNameFor` on Details; cart checkout decrements `bought.Quantity` and
  re-derives `bought.Status` on purchase.

## Affected files
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
- `templates/controllers/cart_controller.sbn`
- `fixtures/project-spec.facenote.json`, `fixtures/project-spec.facenote-en.json`
- `fixtures/seed-response.facenote.json` (git-tracked); `prompts/in/seed-response.json` +
  `prompts/out/seeds/facenote-seed.json` also updated locally but gitignored, not part of this commit
