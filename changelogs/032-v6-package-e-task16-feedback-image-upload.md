# v6 Package E (task 16): feedback (reviews) can attach an image

**Type:** Feature / change
**Model:** Sonnet — reused an existing helper, well-scoped extension; task 16 per the v6 breakdown's Sonnet routing.

## Summary
Second slice of Package E, immediately reusing the `FileUploadService` keystone from changelog 031.
Reviews (the star-rating feedback attached to a product) can now include an optional image. Entirely
template-controlled (reviews_controller + product_details are written-from-scratch templates), so it's
build/test-verifiable here. Un-defers the task-16 item that changelog 031 listed as pending.

## Techniques
- **`feature-templates/rating.json`** - `Review` gains `ImagePath` (`string`, **nullable**, displayName
  "Снимка"). Nullable is deliberate: `SeedStage.BuildSeedSchema` only marks non-nullable props as
  required, so the existing hand-built seed's Review rows (which have no `ImagePath`) stay valid - and
  the new displayName keeps it clean under the task-1 hard gate. Confirmed by re-running `spec expand`
  (lock validates) + the seed validator (all checks pass).
- **`templates/controllers/reviews_controller.sbn`** - injects `FileUploadService`; `Create` now takes
  `IFormFile? image`, saves it via `SaveImageAsync(image, "uploads/reviews")` (same security-vetted
  helper: Guid name, allowlist, size cap), and stores the returned path in `Review.ImagePath`. Null
  file -> null path, so the image is genuinely optional.
- **`templates/views/product_details.sbn`** - the review form gains `enctype="multipart/form-data"` +
  a file input (`name="image"`); each rendered review now shows its attached image when present. No
  controller-side change needed for display: `InjectReviewLoadingIntoDetails` already loads whole
  `Review` entities into ViewData, so `ImagePath` comes along automatically.
- **DI gate broadened** so `FileUploadService` exists wherever it's now consumed:
  - `templates/project/program.sbn`: `needs_upload = has_pfp || features.rating` drives both the
    `using {root}.Services;` and the `AddScoped<FileUploadService>()` registration.
  - `EmitControllersViewsStage.cs`: emits `Services/FileUploadService.cs` when `hasPfp ||
    FeatureEnabled(spec, "rating")` - kept explicitly in sync with program.sbn's condition (noted in a
    comment, since a drift there = a missing-service DI crash at startup).

## Verification
- `dotnet build` clean; `dotnet test` green - **50/50** (was 48; +2 render tests: reviews_controller
  accepts the optional upload and stores `ImagePath`; product_details' feedback form is multipart and
  displays a review image).
- `spec expand` on the fixture: lock re-validates (displayName gate happy with `ImagePath`).
- Seed validator (BuildSeedSchema rules vs the expanded lock + hand-built seed): all checks still pass -
  the nullable column didn't invalidate the existing Review rows.
- **Not verified here:** generated project compile + a real review-with-image round-trip in a browser
  (needs `2-generate.bat` into LocalDB). A real run WILL auto-generate a migration for the new
  `Review.ImagePath` column (has-pending-model-changes -> auto migration), same standing boundary.

## Affected files
- `feature-templates/rating.json`
- `templates/controllers/reviews_controller.sbn`
- `templates/views/product_details.sbn`
- `templates/project/program.sbn`
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
- `tests/Genesis.Tests/CoreTests.cs`
