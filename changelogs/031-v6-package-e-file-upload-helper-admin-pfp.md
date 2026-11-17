# v6 Package E (task 15 + keystone): shared FileUploadService + admin profile-picture upload

**Type:** Feature / change
**Model:** Sonnet — standard feature work, self-contained template + controller pair; task 15 per the v6 breakdown's Sonnet routing.

## Summary
Started Package E (images & uploads) with the piece the breakdown says to "build once and reuse across
3/15/16": a shared single-file upload helper. Wired it into task 15 (admin can upload a user's profile
picture). Deliberately scoped to the **template-controlled** surface (written-from-scratch UsersController
+ its views), which is fully build/test-verifiable here. The scaffolded-controller half of Package E
(task 14, product multi-image POST handling) is NOT attempted - it rewrites scaffolded Create/Edit
actions and needs a live aspnet-codegenerator scaffold to calibrate regex anchors, exactly like A1/A8
were deferred in earlier sessions.

Also fixes a latent bug found while wiring this: `UsersController.Edit` returned `View(user)` but **no
Edit view was ever emitted** - the admin edit page would have thrown "view 'Edit' not found" at runtime.

## Techniques
- **`templates/controllers/file_upload_service.sbn`** (new) - `FileUploadService`: general
  `IFormFile -> saved relative path` helper, distinct from the per-model `ImageService` (which manages
  `{Model}Image` tables). Security-baseline principles applied directly (uploads are an attack surface):
  filename generated from `Guid` (never the client-supplied name -> no path traversal / overwrite),
  extension allowlist (`.jpg/.jpeg/.png/.webp/.gif`), `Content-Type` starts-with `image/` check, 5 MB
  cap, and a code-controlled `subfolder` (never user input). Returns null on invalid/missing file so
  callers can no-op cleanly. No `using`s needed - relies on the web SDK implicit usings, same as
  `ImageService`.
- **`templates/views/users_edit.sbn`** (new) - the previously-missing admin user Edit form. `@model
  ApplicationUser`, `enctype="multipart/form-data"`, current-avatar preview (handles both a full URL like
  the seeded ui-avatars value and a relative uploaded path), file input `name="profilePicture"`. Every
  field is guarded by a Scriban `has_pfp` / `has_first_name` check against
  `spec.identity.application_user.extra_properties`, so the view is valid for specs lacking those props.
- **`templates/controllers/users_controller.sbn`** - when `has_pfp`: inject `FileUploadService`, `Edit`
  POST now takes `IFormFile? profilePicture`, saves it via the helper, and sets `ProfilePicturePath`
  only when a new file was uploaded. Critically, the blind `user.{prop} = input.{prop}` copy loop now
  **skips ProfilePicturePath** - otherwise the empty form field would wipe the existing avatar on every
  save. Constructor is `has_pfp`-branched so pfp-less specs keep the original single-dependency ctor.
- **`templates/project/program.sbn`** - computes `has_pfp`; the `using {root}.Services;` now emits when
  `has_images || has_pfp`, and `FileUploadService` is DI-registered when `has_pfp`.
- **`src/Genesis.Core/Stages/EmitControllersViewsStage.cs`** - emits `Services/FileUploadService.cs`
  when any `ApplicationUser.ProfilePicturePath` exists, and emits `Areas/Admin/Views/Users/Edit.cshtml`
  in the written-from-scratch UsersController block (closing the missing-view bug). The Users Index
  already links to `Edit`, so the page is now reachable and renders.

## Verification
- `dotnet build` clean; `dotnet test` green - **48/48** (was 45; +3 new in `TemplateEmissionTests`:
  the service is security-conscious (Guid/allowlist/size), the Edit view wires a multipart pfp upload,
  and the controller saves the upload without blanking `ProfilePicturePath`). All render through the
  real `TemplateEngine`, proving Scriban parses them and the wiring is present.
- **Not verified here:** the generated project compiling + a real upload round-tripping in a browser -
  needs a full `2-generate.bat` run into LocalDB. Same standing boundary as Packages A/K/H.

## Deferred (rest of Package E)
- **Task 14** (product multiple-images upload UI): rewrites scaffolded Create/Edit POST actions -
  needs a live scaffold to calibrate anchors (A1/A8 class). Not attempted blind.
- **Task 16** (feedback image/video): needs a media-path property on the Review model + migration +
  reviews_controller/view wiring - now unblocked by this helper, but a separate change.
- **Task 12** (real people photos for seed avatars): seeding currently uses ui-avatars initials; a
  Haiku-level swap to a photo source, not done here.

## Affected files
- `templates/controllers/file_upload_service.sbn` (new)
- `templates/views/users_edit.sbn` (new)
- `templates/controllers/users_controller.sbn`
- `templates/project/program.sbn`
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
- `tests/Genesis.Tests/CoreTests.cs`
