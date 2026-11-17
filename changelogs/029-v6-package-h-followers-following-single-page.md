# v6 Package H (task 17): followers + following on one page with a view model

**Type:** Feature / change
**Model:** Sonnet — standard multi-file feature work following the existing controller/view pattern; task 17 per the v6 breakdown's Sonnet routing.

## Summary
Second v6 package, built next per the dependency-first order (H unblocks the big admin/public
aggregation pages in packages D and G — tasks 7 and 26 both consume the followers/following list).
Followers and following were two separate controller actions rendering a shared `List` view keyed by a
`"Mode"` ViewData flag. They are now a **single page with two side-by-side lists**, driven by a
`FollowersFollowingViewModel`, per task 17 ("keep followers + following on a single page; DTO if needed").

## Techniques
- **`templates/controllers/follows_controller.sbn`** — replaced `Followers()` + `Following()` with one
  `List()` action that loads both follower and followed user-id sets, fetches the relevant users in a
  single `_userManager.Users` query, and projects them (order-preserving) into a
  `FollowersFollowingViewModel { Followers, Following }`. The view model is defined in the same file
  (standard "view model next to controller" MVC convention — avoids new emission wiring). `Toggle`'s
  redirect fallback now points at `nameof(List)` instead of the removed `Following`.
- **`templates/views/follows_list.sbn`** — rewritten as a strongly-typed two-column view bound to
  `FollowersFollowingViewModel` (fully-qualified `...Areas.Client.Controllers.FollowersFollowingViewModel`,
  matching how other views fully-qualify their model). Each side shows a count badge and its own list;
  the Following side has the "Спри да следваш" (unfollow) button, the Followers side offers a "Последвай
  обратно" (follow-back) button only for users not already followed. `FirstName` display is guarded by a
  Scriban `has_first_name` check against `spec.identity.application_user.extra_properties` (same defensive
  pattern as `profiles_public.sbn`) so the template stays valid for specs without that extra property.
- **`templates/views/layout.sbn`** — the two nav links (Последователи / Следвани) collapse into one:
  "Последователи и следвани" → `Follows/List`.
- No change to `EmitControllersViewsStage` (still emits `FollowsController.cs` + `Views/Follows/List.cshtml`).

## Notable interaction
The verify gate's `DiscoverRoutes` (`VerifyAndReportStages.cs:163`) already scans `/Client/Follows/List`
for the follows feature. Before this change that route **404'd** (no `List` action existed — only a shared
view name), which the gate tolerated as best-effort. The new `List()` action makes that route real, so the
language gate now actually fetches and scans a live page there.

## Verification
- `dotnet build` clean; `dotnet test` green — **45/45** (was 43; +2 new in `TemplateEmissionTests`:
  `Follows_controller_emits_single_list_action_and_view_model` and
  `Follows_view_renders_two_lists_bound_to_the_view_model`, which render both templates through the real
  `TemplateEngine` — proving Scriban parses them and the combined shape is emitted, and that the old split
  actions are gone).
- **Not verified here:** the generated `FollowsController.cs` + `List.cshtml` compiling as part of a real
  generated project, and the page rendering in a browser — that needs a full pipeline run into LocalDB
  (`2-generate.bat`), same boundary as Package A+K.

## Affected files
- `templates/controllers/follows_controller.sbn`
- `templates/views/follows_list.sbn`
- `templates/views/layout.sbn`
- `tests/Genesis.Tests/CoreTests.cs`
