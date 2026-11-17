# v6 Tasks 13 + 24a: Sort dropdown fixes — selected state + descending direction

**Type:** Error fix + Feature
**Model:** Sonnet — per the v6 task-breakdown's own model routing (tasks 13, 24a = Sonnet).

## Summary
Two coupled sort bugs fixed together because the same `ViewData["SortDir"]` semantic change
required updates in both the controller template and both view templates.

## Bug 1 — card_grid sort dropdown resets to "Подреди по..." after every submit (task 13)

**Symptom:** selecting "Цена" from the Products sort dropdown sorted the data correctly, but on
the redirected page the dropdown snapped back to the blank "Подреди по..." option. The user
couldn't tell what sort was active.

**Root cause:** `card_grid.sbn`'s `<option>` elements had no `selected` attribute, so the
browser always showed the first option after reload.

**Fix:** `card_grid.sbn` sort options now emit:
```html
<option value="{{ f }}" @(ViewData["SortBy"]?.ToString() == "{{ f }}" ? "selected" : "")>{{ f_label }}</option>
```

## Bug 2 — admin_table sort header always resets direction to ASC when switching columns (task 13)

**Root cause:** `ViewData["SortDir"]` was set by `controller.sbn` as the *next* direction
(toggle), not the *current* direction. The admin_table header passed this toggle value as-is
to the next request, so clicking a *new* column inherited the previous column's toggled direction
instead of starting fresh at ASC.

**Fix:** changed `ViewData["SortDir"]` semantic to *current* direction:
```csharp
ViewData["SortDir"] = descending ? "desc" : "asc";  // current, not next
```
`admin_table.sbn` now computes the toggle inline per-column:
```razor
asp-route-sortDir="@(ViewData["SortBy"]?.ToString() == "{{ p.name }}" && ViewData["SortDir"]?.ToString() == "asc" ? "desc" : "asc")"
```
Same column ascending → click goes descending; any other case → always ASC first. Also added
↑/↓ indicator on the active column header and ⇅ on inactive sortable ones.

## Feature — descending sort in card grid (task 24a)

**`card_grid.sbn`** now has a second `<select name="sortDir">` for direction (↑ Нарастващо /
↓ Намаляващо), appearing beside the sort-by dropdown whenever `owner.sorting.enabled`. The
controller already handled `sortDir` param; the view was just never sending it. The selected
option is preserved via the same `ViewData["SortDir"]` current-direction convention above.
Column widths adjusted (search: col-md-4, sortBy: col-md-3, sortDir: col-md-2).

## Verification
- `dotnet test` 60/60. New tests:
  - `Card_grid_sort_dropdown_preserves_selected_field`
  - `Card_grid_has_sort_direction_select_when_sorting_enabled`

## Affected files
- `templates/controllers/controller.sbn`
- `templates/views/card_grid.sbn`
- `templates/views/admin_table.sbn`
