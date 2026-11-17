## 044 — Task 26: Public user profile (full aggregation) — Package G

**Type:** feature
**Model:** Opus — per the v6 task-breakdown's own model routing (task 26 = Opus, L-effort aggregation page).
**Date:** 2026-07-10
**Task completed:** v6 Task 26 (public profile: photo, pics, followers, following, listed products, feedbacks, posts)

### Design: shared aggregation component (reused by Task 7)

The breakdown says build Task 26 (public profile) and Task 7 (admin user detail) to **share components**.
So the aggregation is one reusable stack, feature-gated at Scriban time so absent features emit nothing:

- **`Models/UserProfileViewModel.cs`** (`templates/models/user_profile_view_model.sbn`) — holds `User` +
  feature-gated `Products`, `Posts`, `Feedbacks`, `Catalogues`, `Followers`, `Following`.
- **`Services/UserProfileService.cs`** (`templates/controllers/user_profile_service.sbn`) — one
  `BuildAsync(userId, publicOnly)` that fills the VM. Single source of truth for both pages (no query
  drift). `publicOnly` limits catalogues to public ones on the anonymous page.
- **`Views/Shared/_UserProfileContent.cshtml`** (`templates/views/user_profile_content_partial.sbn`) —
  renders every section. Branches on `ViewData["AdminLinks"]`: user links target `Admin/Users/Details`
  in admin mode, else `Client/Profiles/Public`. Placed in root `Views/Shared` so both areas resolve it.

The **"listing model"** (user-owned products) is auto-detected as the model with both `images` and a
`UserId` property — Product for FaceNote. If none exists, the products section simply isn't generated.

### Rewired

- `templates/controllers/profiles_controller.sbn` — now injects `UserProfileService`, calls
  `BuildAsync(id, publicOnly: true)`, returns the VM (was: two inline ViewData queries).
- `templates/views/profiles_public.sbn` — header (avatar + name) + `<partial name="_UserProfileContent">`.

### Wiring / gate sync (three places, per changelog 033 discipline)

`UserProfileService` DI + VM/service emission gated on `posts || catalogues`:
- `templates/project/program.sbn` — `needs_profile_service`, plus the `using ...Services` gate widened.
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs` — emits VM + service + partial in the profiles block.
- `src/Genesis.Core/Stages/BuildStages.cs` — pre-emits VM + service before the Program.cs build gate.

### Verification

- Followers/following resolution mirrors the proven `follows_controller.sbn` pattern
  (`_userManager.Users.Where(u => ids.Contains(u.Id))`) — known-compiling.
- 6 new tests; `dotnet test`: **73/73** (67 → 73).
- The service queries DbSets (`Posts`/`Reviews`/`Catalogues`/`Follows`) that exist only after feature
  expansion — emit runs on the expanded spec, so they're present at real generation. Unit tests render
  the raw fixture (feature flags true, Product qualifies as listing), validating Scriban + key output.

Not verified here: generated-project compile + browser render (a full 2-generate run at end of the
Opus batch will confirm tasks 10/26/7 together).
