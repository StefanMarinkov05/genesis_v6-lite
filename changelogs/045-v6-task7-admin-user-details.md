## 045 — Task 7: admin/users/details/{id} full aggregation — Package D

**Type:** feature
**Model:** Opus — per the v6 task-breakdown's own model routing (task 7 = Opus, generator-level emission pattern).
**Date:** 2026-07-10
**Task completed:** v6 Task 7 (admin user detail: followers, following, listings, posts, feedbacks, pfp, data; FK links)

### Reuses the Task 26 shared component

Built directly on the `UserProfileService` + `UserProfileViewModel` + `_UserProfileContent` partial from
changelog 044 — exactly the "share components" the breakdown asked for. No query duplication.

### What changed

- `templates/controllers/users_controller.sbn`:
  - ctor restructured to conditionally inject `UserProfileService` (gated `needs_profile = posts||catalogues`)
    alongside the existing conditional `FileUploadService` — handles all 4 dependency combinations cleanly.
  - New `Details(string id)` action → `_profiles.BuildAsync(id, publicOnly: false)` (admin sees all,
    including private catalogues), passes roles via ViewData.
  - Delete guard's admin-role check now uses `{{ subst admin_role }}` (was raw `{{ admin_role }}`, which
    emitted `$variation:adminRoleName` when rendered pre-expansion) — correct even against a raw spec.
- `templates/views/users_details.sbn` (new) — avatar + name + roles badge, a `<dl>` of user data
  (Id/Email/name/role), management buttons (Edit, Delete guarded against deleting admins), then the shared
  `_UserProfileContent` partial with `ViewData["AdminLinks"] = true` so every user link targets
  `Admin/Users/Details` and record links target their detail pages.
- `templates/views/users_index.sbn` — "Детайли" button per row (gated posts||catalogues).
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs` — emits `Areas/Admin/Views/Users/Details.cshtml`
  in the written-from-scratch Users block, gated identically to the controller's Details action.

### Verification

- 3 new tests; `dotnet test`: **76/76** (73 → 76).
- Details action gated on the same condition as `UserProfileService` existence, so no view/action
  references a missing service. Uses the proven aggregation service (changelog 044).

Not verified here: generated-project compile + browser render — a full 2-generate run verifies tasks
10/26/7 together next.
