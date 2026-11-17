# v6 Task 6: Admin users table — user ID column + admin-delete-admin guard

**Type:** Feature / Error fix
**Model:** Sonnet — per the v6 task-breakdown's own model routing (task 6 = Sonnet).

## Summary
Two small admin/users improvements: surfacing the user ID in the table (makes it easier to
cross-reference log entries / seed references), and extending the delete guard to block
deleting any administrator account, not just self-delete.

## Changes

### `templates/views/users_index.sbn`
- Added an `ИД` column as the first column in the users table.  The full GUID is kept in the
  `title` tooltip; the cell shows `@user.Id[..8]…` (first 8 chars) wrapped in `<code>` so it
  reads as an identifier, not a name.

### `templates/controllers/users_controller.sbn`
Extended the `Delete` action with a second guard:
```csharp
var targetRoles = await _userManager.GetRolesAsync(user);
if (targetRoles.Contains("{{ admin_role }}"))
{
    TempData["Error"] = "Не можете да изтриете администраторски акаунт.";
    return RedirectToAction(nameof(Index));
}
```
The admin role name is resolved from `spec.identity.roles` via a Scriban loop (safe across
specs with different role names). The self-delete guard (already present) still fires first;
the admin-guard fires after `FindByIdAsync` (avoiding an unnecessary role lookup if the user
doesn't exist). The delete itself now calls `DeleteAsync` directly (removed the `is not null`
wrapper that was redundant given the early return).

## Verification
- `dotnet test` 60/60. New test: `Users_controller_blocks_deleting_admin_accounts` — asserts
  `GetRolesAsync`, `targetRoles.Contains(`, and the Bulgarian error message are all present.

## Affected files
- `templates/views/users_index.sbn`
- `templates/controllers/users_controller.sbn`
