# PostComment image support (item 14, narrowed scope)

**Type:** Feature / change

## Summary
Original plan assumed BOTH PostComment and Review needed image support added from scratch.
Investigating before implementing found **Review already has full working image support**
(`ImagePath` field, `IFormFile` upload wired in `reviews_controller.sbn`, correct display in
`product_details.sbn`) - only **PostComment** genuinely lacked it. Narrowed scope accordingly
rather than redoing work that already existed.

## Techniques
- Added `PostComment.ImagePath` (nullable string, `displayName: "Снимка"`) to
  `feature-templates/posts.json` - same simple single-path pattern already used by Post and Review
  (not the multi-image `{Model}Image` table pattern Product uses), since a comment needs at most
  one attached photo.
- `templates/controllers/posts_controller.sbn`'s `Comment` action now accepts `IFormFile? image`
  and saves it via the ALREADY-injected `FileUploadService` (Post's own image upload already needed
  this dependency, so no new DI wiring required) into `uploads/comments/`.
- `templates/views/posts_details.sbn`: comment form gained `enctype="multipart/form-data"` + a file
  input; the comment list now shows the attached image (with the correct leading `/` - this session
  already fixed the class of bug where stored paths were rendered without one).

## Verification
- `dotnet build` + `dotnet test`: 111/111.
- Full clean `run --all` BG regenerate, verify PASSED. Inspected generated output directly:
  `PostComment.ImagePath` on the model, the controller action's signature and save call, the
  comment form's multipart encoding + file input, and the image display in the comment list.

## Affected files
- `feature-templates/posts.json`
- `templates/controllers/posts_controller.sbn`
- `templates/views/posts_details.sbn`
