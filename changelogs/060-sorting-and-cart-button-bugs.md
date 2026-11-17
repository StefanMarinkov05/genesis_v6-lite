# Sorting direction bug + add-to-cart/catalogue mutual exclusion (item 2)

**Type:** Error fix

## Where
`templates/controllers/sorting_index_body.sbn:37`, `templates/views/card_grid.sbn:91`.

## How it happened
- `sorting_index_body.sbn`: `ViewData["SortDir"] = descending ? "asc" : "desc";` inverted the
  requested direction back into ViewData instead of echoing it - clicking "desc" stored "asc",
  so the UI's selected-direction indicator (and any state depending on it) never reflected reality.
- `card_grid.sbn`: the add-to-cart block was gated on `features.orders && !(page.card | array.contains
  "addToCatalogueButton")` - treating "add to catalogue" and "add to cart" as mutually exclusive
  per page, so a page with both features enabled (Products has both) only ever showed one button.

## Why
Both were one-line logic mistakes from when these fragments were originally written - not design
tradeoffs.

## What it affects
Products Index page: sort-direction indicator arrows/state, and the missing "Добави в количката"
button whenever a page also has catalogue buttons enabled.

## How to fix
- `sorting_index_body.sbn`: `ViewData["SortDir"] = sortDir;` (echo the actual requested value).
- `card_grid.sbn`: dropped the `&& !(...)` clause - both buttons now render independently based on
  their own feature flags.

## Verification
`dotnet build` + `dotnet test`: 111/111. Not yet live-regenerated (time-constrained session) -
will be confirmed visually at the next full pipeline verify.
