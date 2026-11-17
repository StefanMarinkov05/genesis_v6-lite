## 043 — Task 10: Blog posts photos (Package C)

**Type:** feature
**Model:** Opus — per the v6 task-breakdown's own model routing (task 10 = Opus, L-effort whole feature surface).
**Date:** 2026-07-10
**Task completed:** v6 Task 10 (restore blog posts — photos, comments, likes)

### Finding first

Comments and likes/votes were **already fully built** in the posts feature stack:
- `posts_controller.sbn` already has `Vote(+1/-1 with toggle-off)`, `Comment`, owner/admin-guarded soft
  `Delete`, and `Index`/`Details` that `Include` Votes + Comments.
- `posts_details.sbn` already renders the score, up/down vote forms, the comment list, and a comment form.

The **only** missing piece was photos in posts. So Task 10 reduced to adding a single image per post,
implemented exactly like Task 16 (feedback image, changelog 032) — reusing the shared `FileUploadService`.

### What changed

- `feature-templates/posts.json` — Post gains `ImagePath` (string, **nullable**, displayName "Снимка").
  Nullable ⇒ existing hand-built seed Post rows (no ImagePath) stay valid under BuildSeedSchema (only
  non-nullable fields are required); displayName keeps it clean under the Task-1 hard gate.
- `templates/controllers/posts_controller.sbn` — injects `FileUploadService`; `Create` now takes
  `IFormFile? image`, removes `ImagePath` from ModelState, saves via `SaveImageAsync(image, "uploads/posts")`.
- `templates/views/posts_create.sbn` — multipart form + optional file input.
- `templates/views/posts_details.sbn` — renders the full post image when present.
- `templates/views/posts_index.sbn` — feed card shows a clickable thumbnail when present.

### Gate sync (the recurring FileUploadService hazard)

`FileUploadService` DI/emit is gated in **three** places that must agree (changelog 033 drift lesson).
All broadened from `has_pfp || rating` to also include `posts`:
- `templates/project/program.sbn` — `needs_upload = has_pfp || features.rating || features.posts`
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs` — emit condition
- `src/Genesis.Core/Stages/BuildStages.cs` — pre-emit (Program.cs build-gate) condition

### Verification

- 4 new tests (Posts controller upload, create form, index/details image render, program DI registration).
- `dotnet test`: **67/67 pass** (63 → 67).
- `spec expand` on the FaceNote fixture clears all feature expansion + semantic validation (incl. the
  displayName gate on the expanded lock with the new Post.ImagePath) — pauses only at the manual LLM
  review stage, as expected.

Not verified: real DB run + browser upload (same boundary as tasks 15/16 — needs a full 2-generate).
