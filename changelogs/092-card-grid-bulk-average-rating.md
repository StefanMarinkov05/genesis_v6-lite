# 092 — Card grid: bulk average rating stars per product

**Type:** Feature / change
**Model:** Sonnet, medium effort — standard multi-file feature work following the existing
sorting/rating scaffold-transform patterns, verified end-to-end with a real regenerate.

## Summary

The `Client/Products` card grid (`card_grid.sbn`) never showed a star rating - `AverageRating`
was only ever computed inside the scaffolded `Details` action (`InjectReviewLoadingIntoDetails`),
so the listing page had no rating signal at all. This was the deferred second half of changelog
091's two-part ask.

Added a bulk per-page average-rating computation to the Index action and wired it into the card
grid:

- `templates/controllers/sorting_index_body.sbn`: when `has_rating`, the query gains
  `.Include(x => x.Reviews)`, and after the page of items is fetched, builds
  `ViewData["AverageRatings"]` as a `Dictionary<int, double?>` keyed by the model's primary key,
  averaging each item's non-deleted `Reviews.Stars` (null when a product has no reviews - same
  "null means no data" convention as the Details action's `AverageRating`).
- `templates/views/card_grid.sbn`: each card now renders a 5-star row (gated on `has_rating`)
  reading its own average out of the `AverageRatings` dict, defaulting to 0/5 stars when absent -
  consistent with 091's "always show the row, default to empty" decision for Details.
- `EmitControllersViewsStage.cs`: new `ModelHasRating(spec, modelName)` helper (feature enabled +
  model is the rating feature's primary model). Threaded into both render call sites - the
  `sorting_index_body.sbn` snippet render and the page-template render (`card_grid`/`adminTable`
  alike, harmless no-op for pages that don't use it) - as `has_rating`, rather than duplicating the
  feature-primary-model check inside Scriban (no primary-model lookup is exposed to templates,
  only boolean `features.*` flags).

Only the SORTING-enabled Index shape was touched (`sorting_index_body.sbn`) - FaceNote's Product
grid uses it, and it is the only path this session could regenerate/verify. A card grid whose
controller has sorting disabled would keep the plain scaffolded Index (no bulk Include, no
`AverageRatings`) and thus render 0-star rows even with reviews; the star row itself never breaks
(the dict lookup defaults to null), so this is a silent under-display, not a bug, but is flagged
here as a known gap for a future non-sorting-path pass rather than guessed at blind.

## Verification

Real regenerate against `fixtures/project-spec.facenote.json` (`emit-controllers-views` then
`verify`, both via `dotnet run --project src/Genesis.Cli`): build green, `ProductsController.Index`
confirmed carrying `.Include(x => x.Reviews)` + the `AverageRatings` dictionary build, `verify`
PASSED (build/db/boot/http/manifest/language). Manually booted the generated app and curled
`/Client/Products`: real per-card star counts observed (`★★★★★`, `☆☆☆☆☆`, `★★★★☆` mixed across
cards, matching actual seeded review data) - not just a template-render check. 4 new tests
(2 card_grid, 2 sorting_index_body, has_rating true/false each) - suite 116 → 120 green.

## Affected files

- `templates/controllers/sorting_index_body.sbn`
- `templates/views/card_grid.sbn`
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
- `tests/Genesis.Tests/CoreTests.cs`
