# Admin Orders: no manual Create + admin-table FK links (items 9 + 13)

**Type:** Feature / change

**Note:** bundled two items in one commit/version bump - both touch `templates/views/admin_table.sbn`
in the same region and couldn't be cleanly separated into independent patches under this session's
time constraint. Documented as two items below rather than force-merged into one description.

## Item 9 - Orders should only originate from client checkout, not be hand-created by an admin.
Used the existing `removedActions` deviation mechanism (already established for stripping
scaffolded actions) rather than inventing a new one.
- `SpecStages.InjectAdminSupervisorSurface` now sets `removedActions: ["Create"]` on the
  auto-generated `OrdersController` (Admin area) specifically, when injecting it.
- `templates/views/admin_table.sbn`: the "Създай нов запис" button was previously unconditional -
  now hidden when `owner.removed_actions` contains `"Create"`, mirroring the exact pattern already
  used for hiding the Details button. Generic fix - applies to any future model with Create removed,
  not just Order.

## Item 13 - admin Reviews (and every other admin table with a foreign-key relation column) now
links to the related record instead of showing plain text.
- `templates/views/admin_table.sbn`: the FK-relation column (`@item.{{ rel.navigation }}?.{{
  target_display }}`) is now wrapped in a link to the target model's own Admin Details page - looks
  up the REAL target controller name from `spec.controllers` (matching by `model == rel.target` in
  the Admin area) rather than guessing a plural suffix, since naive "+s" pluralization breaks for
  names like "Category" (would produce "Categorys" instead of "Categories"). Falls back to the
  original plain-text rendering if no matching Admin controller is found for the target model.
  Generic fix - applies to every FK relation column in every admin table, not just Review->Product.

## Verification
`dotnet build` + `dotnet test`: 111/111. Not yet live-regenerated (time-constrained session) - will
confirm the Create action/link/view are actually gone from Admin/Orders at the next full verify.

## Affected files
- `src/Genesis.Core/Stages/SpecStages.cs`
- `templates/views/admin_table.sbn`
