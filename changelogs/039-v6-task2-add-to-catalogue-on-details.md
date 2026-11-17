# v6 Task 2: "Add to catalogue" button on product details page

**Type:** Feature
**Model:** Sonnet — per the v6 task-breakdown's own model routing (task 2 = Sonnet).

## Summary
The `card_grid.sbn` already had the catalogue modal button for each card. The product details
page had nothing — users had to navigate back to the grid to catalogue an item. This adds the
same modal picker directly on the details page.

## Changes

### `templates/views/product_details.sbn`
Two additions, both gated on `{{~ if features.catalogues ~}}`:

1. **Button** (in the action row beside "Добави в количката"):
   ```html
   <button class="btn btn-outline-primary mt-2 catalogue-open-details" type="button"
           data-item-id="@Model.Id">Добави в каталог</button>
   ```
   Uses `catalogue-open-details` CSS class (distinct from card_grid's `catalogue-open`)
   to avoid JS selector collisions on pages that could render both in theory.

2. **Modal + `@section Scripts { ... }`** at the end of the file:
   - `#catalogueModalDetails` Bootstrap modal (separate ID from card_grid's `#catalogueModal`)
   - Same three-step JS as card_grid: `loadPicker → fetch /Client/Catalogues/Picker → wire
     toggle + create handlers`. Only the button selector differs
     (`.catalogue-open-details` instead of `.catalogue-open`).
   - Hidden `<form>@Html.AntiForgeryToken()</form>` (same pattern as card_grid) to make the
     CSRF token available to the fetch-based Toggle/Create requests.

## Design note
The JS is duplicated from card_grid rather than shared via a partial/include because Razor
`@section Scripts` is already the established extension point in this project's layout, and
Scriban has no include-loader. A shared `_CatalogueModal.cshtml` partial would be a clean
refactor but is out of scope for this task.

## Verification
- `dotnet test` 60/60. New test: `Product_details_has_catalogue_modal_button_when_catalogues_enabled`
  — asserts `catalogue-open-details`, `catalogueModalDetails`, `/Client/Catalogues/Picker`,
  and "Добави в каталог" are all present in the rendered output.
- **Not verified here:** end-to-end browser test (needs `2-generate.bat` run).

## Affected files
- `templates/views/product_details.sbn`
