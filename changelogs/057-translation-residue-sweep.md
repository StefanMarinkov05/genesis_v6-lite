# Translation residue sweep (item 1)

**Type:** Feature / change

## Summary
Round-2 bug batch item 1: closed the remaining English residue Stefan found in the generated
FaceNote BG app - home hero text, top nav / admin dropdown labels, FK dropdown field labels, enum
value labels (Status), and IsDeleted/DeletedAt labels. All fixes are generator-side (templates +
spec data), not hand-patches to the disposable `out/` output.

## Techniques
- **New spec fields**: `meta.domainHintBg` (BG hero text), `ModelSpec.displayName` (Bulgarian
  plural noun, drives nav/admin-dropdown labels), `ControllerSpec.displayName` (falls back to the
  raw short name when unset), `ControllerSpec.isLookup` (unused yet - forward groundwork for item
  6/7), enum values restructured from `array of string` to `array of {value, displayName}`.
- `templates/views/landing.sbn` picks `domainHintBg` when `meta.is_bulgarian`, else `domainHint`.
- `templates/views/layout.sbn`: both the public-controller nav loop and the admin dropdown loop now
  render `c.displayName` (falling back to the raw short name) instead of the raw controller name.
  Admin dropdown also gained an inert `isLookup`-skip + placeholder "Характеристики" link, dormant
  until item 6/7 actually sets `isLookup: true` anywhere.
- `templates/models/model.sbn`: enum members now emit `[Display(Name=...)]` per value - confirmed
  this is picked up automatically by ASP.NET's built-in `Html.DisplayFor`/`DisplayNameFor` templated
  helpers already used by every scaffolded view, no further per-view template change needed.
  FK scalar properties already had generic `[Display]` support (just needed `displayName` populated
  in spec data) - navigation properties (`Brand`, `Category`) previously hardcoded
  `[Display(Name="{{ rel.navigation }}")]` (the raw English identifier); now resolved from the
  matching `{Nav}Id` FK property's own `displayName` at render time. `IsDeleted`/`DeletedAt` gained
  `[Display(Name="Изтрит"/"Изтрит на")]`.
- `templates/controllers/cart_controller.sbn`: updated `.enum_values | array.last` to
  `(...).value` for the new object shape (this line's SEMANTICS - flipping Product.Status on
  purchase - gets properly reworked in item 19 once Status is quantity-derived; this commit only
  keeps it syntactically correct against the new shape).
- `src/Genesis.Core/Stages/SpecStages.cs`'s `InjectAdminSupervisorSurface` now copies the model's
  `displayName` onto the auto-generated Admin CRUD controller it creates.
- `templates/identity/identity-bg-map.json`: added entries for "Manage your account", "Profile",
  "Username", "Phone number" etc. **Confirmed these are currently inert** - the Identity Manage
  pages aren't scaffolded as physical files by the current `IdentityStage` (`--files
  "Account.Register;Account.Login;Account.Logout"` only), they're served by the built-in
  `Microsoft.AspNetCore.Identity.UI` package. Item 17 (checkpoint 5) needs to explicitly add the
  Manage page group to the scaffold `--files` list before these translations - and item 17's planned
  structural edits (remove 2FA/Personal data, add "Публичен акаунт" link) - can take effect.

## Bug caught mid-implementation (Scriban `||` does not return an operand)
First pass used `label = c.display_name || short` (JS/Python-style "value-or-fallback") in three
places (layout.sbn x2, model.sbn's nav-property label). Scriban's `||` returns a **boolean**, not
either operand - this silently rendered the literal string `"true"` everywhere a value was expected,
caught via live regeneration (not just unit tests, which don't render templates against real spec
data end-to-end). Rewrote all three as explicit `if X; label = X; else; label = fallback; end`.
Audited every other `||` in the touched templates - all remaining uses are genuine boolean
conditions (inside `if`), unaffected.

Separately, an early controller-displayName python edit compared `short` (plural, e.g. "Products")
against a name map keyed by singular model names ("Product") - silently matched nothing. Fixed by
keying off `ControllerSpec.model` (which already holds the correct singular model name) instead.

## Verification
- `dotnet build` + `dotnet test`: 111/111.
- Full clean `run --all` regenerate of BG FaceNote, verify stage PASSED (build/db/boot/http/manifest/
  language). Live-checked in the actual generated output: nav labels ("Продукти", "Марки",
  "Категории", "Каталози", "Поръчки" etc. - all Bulgarian now), home hero Bulgarian text, Product
  model's FK `[Display]` attributes (Марка/Категория on both the scalar FK and the nav property),
  enum member `[Display]` attributes (Наличен/Запазен/Продаден), IsDeleted/DeletedAt labels.
- EN fixture (`fixtures/project-spec.facenote-en.json`) re-synced from the updated BG fixture after
  every raw-fixture edit in this item, per the established "keep both in sync" convention.

## Affected files
- `spec.schema.json`, `src/Genesis.Core/Spec/ProjectSpec.cs` (schema + POCO changes)
- `src/Genesis.Core/Stages/SpecStages.cs` (`InjectAdminSupervisorSurface`)
- `templates/views/landing.sbn`, `templates/views/layout.sbn`
- `templates/models/model.sbn`, `templates/controllers/cart_controller.sbn`
- `templates/identity/identity-bg-map.json`
- `fixtures/project-spec.facenote.json`, `fixtures/project-spec.facenote-en.json`
- `feature-templates/cart.json`, `catalogues.json`, `follows.json`, `orders.json`, `posts.json`,
  `rating.json` (model + FK + enum `displayName` population)
- `tests/Genesis.Tests/CoreTests.cs` (one stale assertion fix, unrelated to this item's own changes)
