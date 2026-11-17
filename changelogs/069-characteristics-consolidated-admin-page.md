# Consolidated "Характеристики" admin page for lookup models (items 6/7)

**Type:** Feature / change

## Summary
The admin dropdown was cluttered with a full CRUD entry for every small join/lookup model
(CartItem, OrderItem, PostComment, PostVote, CatalogueItem) alongside the real content models.
Stefan's confirmed design: one combined "Характеристики" nav entry -> one page, sectioned by
model, read-only DTO-style tables - not full CRUD scaffolds.

## Techniques
- **New spec field** `ModelSpec.IsLookup` (schema + POCO) - set `true` on the 5 target models in
  their respective `feature-templates/*.json` files.
- `SpecStages.InjectAdminSupervisorSurface` now copies `model.isLookup` onto the auto-generated
  Admin controller entry it creates (same plumbing pattern already used for `displayName` in
  item 1) - the underlying Admin/{Model} controllers still get created (scaffolded CRUD, still
  reachable directly), just carry the flag.
- `templates/views/layout.sbn`'s admin dropdown loop (already had the `isLookup`-skip groundwork
  laid in item 1, dormant until now) actually activates: any controller with `isLookup: true` is
  excluded from its own nav entry, and a single "Характеристики" link appears instead whenever at
  least one lookup controller exists.
- **New hand-written controller** `templates/controllers/characteristics_controller.sbn`: a single
  `[Authorize(Roles = admin)]` Index action that queries all `isLookup` models (discovered
  generically via `spec.models | is_lookup`, not a hardcoded list of 5 - a 6th lookup model added
  later would need zero controller-template changes) with `.Include()` for their manyToOne
  relations, returned via a Scriban-generated `CharacteristicsViewModel` (one strongly-typed list
  property per lookup model).
- **New view** `templates/views/characteristics_index.sbn`: Bootstrap nav-tabs, one tab per lookup
  model, each a simple read-only table - columns generated generically from each model's own
  properties (same "iterate properties, use `displayName` or fall back to the raw name" pattern
  already established in `admin_table.sbn`), FK relation columns render the related record's
  display value. Caught and fixed the same `||`-as-boolean Scriban pitfall from item 1's
  translation-residue work (`{{ p.display_name || p.name }}` would have silently rendered the
  literal string `"true"`) before it ever reached a real build - rewrote both instances as explicit
  `if/else` assignment.
- Emission wired into `EmitControllersViewsStage.cs`, gated on `spec.Models.Any(m => m.IsLookup)`
  so a project with no lookup models never gets a pointless empty page.

## Verification
- `dotnet build` + `dotnet test`: 111/111.
- Full clean `run --all` BG regenerate, verify PASSED. Directly inspected generated output:
  `CharacteristicsController.cs` queries all 5 lookup models; the view renders 5 correctly-labeled
  Bulgarian tabs; the admin dropdown in `_Layout.cshtml` no longer lists any of the 5 individual
  lookup controllers - only the single "Характеристики" link remains.

## Session note
This work was briefly interrupted mid-verification by an abrupt session teardown (unrelated to
this feature) that left `out/FaceNote/FaceNote/plan/state.json` torn (all-null-bytes from a
mid-write kill) and a few orphaned `dotnet.exe` processes holding stale locks. Recovered by
clearing the orphaned processes and doing one more clean `out/` wipe + full regenerate - no code
was lost (all prior commits through item 14 were already safely committed), this was purely a
disposable-output-directory cleanup, not a data-loss incident.

## Affected files
- `spec.schema.json`, `src/Genesis.Core/Spec/ProjectSpec.cs`
- `src/Genesis.Core/Stages/SpecStages.cs`, `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
- `templates/views/layout.sbn` (activates item 1's dormant groundwork)
- `templates/controllers/characteristics_controller.sbn` (new)
- `templates/views/characteristics_index.sbn` (new)
- `feature-templates/cart.json`, `catalogues.json`, `orders.json`, `posts.json` (`isLookup: true`)
