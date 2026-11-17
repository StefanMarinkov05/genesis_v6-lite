# Image display bugs + admin Posts Edit/Details fixes (items 10, 11, 15 + a dropped item)

**Type:** Error fix / Feature

## Root cause found for items 10 + 15 (raw image URLs not displaying)
`FileUploadService.SaveImageAsync` returns a path WITHOUT a leading slash (documented in its own
comment: `"uploads/avatars/<guid>.jpg"`). Every render site needs to prepend `/` to make it
root-relative; several didn't, so the browser resolved the `<img src>` relative to the CURRENT
PAGE URL instead (e.g. `/Admin/Posts/uploads/posts/abc.jpg` - a 404). This is exactly why "the
image is not properly displayed" on admin Posts (item 10) - and the same bug existed in 6 other
spots app-wide (item 15's "images not displayed, don't give url" is the same class of bug):
`templates/views/admin_table.sbn` (the generic image column - fixes item 10 AND any other model
with an image-path column shown in an admin table), `layout.sbn` (nav avatar), `profiles_public.sbn`,
`users_details.sbn`, `user_profile_content_partial.sbn` (x2, Followers/Following avatar lists),
and `image_gallery_partial.sbn` (currently unreferenced by any view, fixed anyway for correctness).
All fixed by prepending `/` (or building the string with `"/" + path` where it's inside a ternary
with an already-absolute default path).

## Item 11 - admin Posts Edit: remove-image option instead of a raw URL textbox
- **New helper** `LegacyImagePathProperty(model)`: detects a raw stored-path string field (name
  ends with "Path" - the same heuristic `admin_table.sbn` already used to detect an image column).
- `StripSystemFieldsFromViews`'s field-splitting (already built for item 19's Status) extended
  again: the image-path field is now also form-only-stripped - removed from `[Bind]` and the
  Create/Edit text input, but stays visible as an actual `<img>` on Details/Index.
- **New transform** `InjectImageRemoveOption`: adds a "Премахни текущата снимка" checkbox to Edit
  only (Create has no existing image to remove) and a `bool removeImage = false` parameter to the
  Edit POST action - preserves the existing stored path unless the checkbox was ticked, in which
  case it's nulled. Composes correctly with `InjectOwnerAutoAssign`/`InjectQuantityDerivedStatus`'s
  own edits to the same action body (all three now stack in the generated Product/Post Edit
  actions where applicable, verified live).
- **Known limitation, noted not fixed**: nulling the DB column doesn't delete the physical file
  from `wwwroot` - an orphaned file is a minor storage cost, not a functional bug, and doing real
  deletion needs `IWebHostEnvironment` injected into a scaffolded controller that doesn't have it.
  Deferred given the session's scope.

## Dropped-item recovery: admin Posts Details now shows comments
Stefan's original numbered list had "Admin/Posts/Details - add postcomments here" as its own item;
it was lost during the planning session's renumbering into checkpoints and never made it into the
tracked task list. Caught by re-checking the generated output against the original request list,
not by Stefan re-reporting it. **New transform** `InjectPostCommentsIntoAdminDetails`, narrowly
gated to the Post admin controller specifically (same style as `InjectReviewLoadingIntoDetails`'s
feature+model gate rather than a generic "surface every child collection" mechanism, since only
this one case was requested): adds `.Include(x => x.Comments)` to the Details query and a comments
list to the admin Details view.

## Verification
- `dotnet build` + `dotnet test`: 111/111 (one unrelated flaky parallel-timing test, passed in
  isolation on rerun - same known flake as earlier this session).
- Full clean `run --all` BG regenerate, verify PASSED. Directly inspected generated output: admin
  Posts Index image now has the leading slash; `ImagePath` absent from Create/Edit forms; the
  remove-image checkbox renders in Edit; the Edit POST action has the `removeImage` parameter and
  correctly preserves-or-nulls the path; admin Posts Details includes and renders the comments list.

## Affected files
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
- `templates/views/admin_table.sbn`, `layout.sbn`, `profiles_public.sbn`, `users_details.sbn`,
  `user_profile_content_partial.sbn`, `image_gallery_partial.sbn`
