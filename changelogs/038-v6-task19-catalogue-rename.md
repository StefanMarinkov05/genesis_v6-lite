# v6 Task 19: Catalogue rename (edit own catalogue name)

**Type:** Feature
**Model:** Sonnet — per the v6 task-breakdown's own model routing (task 19 = Sonnet).

## Summary
Users can now rename their own catalogues. Delete was already owner-gated in the controller
and visible in the UI. Rename was entirely missing.

## Changes

### `templates/controllers/catalogues_controller.sbn`
Two new actions at the bottom of `CataloguesController`:
- `GET EditName(int id)` — owner check (UserId == current user), returns the `Catalogue` to
  a rename form view; returns `NotFound` for other users' catalogues.
- `POST EditName(int id, string name)` — owner check, trims name, saves, redirects to `Index`.
  No-op if name is blank (preserves old name).

### `templates/views/catalogues_index.sbn`
Added a "Преименувай" link button to each catalogue card's header actions row:
```html
<a class="btn btn-sm btn-outline-secondary" asp-action="EditName" asp-route-id="@cat.Id">Преименувай</a>
```
Placed between the TogglePublic form and the Delete form to preserve the existing button order.

### `templates/views/catalogues_edit_name.sbn` (new file)
Simple rename page: `@model Catalogue`, one text input pre-filled with `@Model.Name`,
"Запази" submit and "Отмени" link back to `Index`. `autofocus` on the input for UX.

### `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
Added emission of `Areas/Client/Views/Catalogues/EditName.cshtml` inside the `catalogues`
feature block, right after `PublicDetails.cshtml`.

## Verification
- `dotnet test` 60/60. New tests:
  - `Catalogues_controller_has_edit_name_action`
  - `Catalogues_edit_name_view_renders_rename_form`

## Affected files
- `templates/controllers/catalogues_controller.sbn`
- `templates/views/catalogues_index.sbn`
- `templates/views/catalogues_edit_name.sbn` (new)
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
