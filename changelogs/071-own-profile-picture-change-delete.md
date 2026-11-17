# Own profile picture change/delete (item 21)

**Type:** Feature / change

## Summary
Last item of the round-2 batch: a logged-in user can now upload a new profile picture or remove
their current one from the Identity Manage page.

## Techniques
- **New actions on the already-hand-written `ProfilesController`** rather than touching the
  scaffolded Identity `IndexModel` Razor Page code-behind: `ChangePicture` (`IFormFile?`, saved via
  the already-injected `FileUploadService`) and `RemovePicture` (nulls the path) - both
  `[Authorize]`, both always operate on `_userManager.GetUserAsync(User)` (the CURRENT user),
  ignoring any route/form parameter that could name someone else. Gated on `has_pfp` (the same
  `ProfilePicturePath` extra-property check used elsewhere), so a project without a profile-picture
  field emits neither the actions nor the form.
- **New transform** `InjectPfpFormIntoManageIndex`: appends a small self-contained upload/remove
  form block to the scaffolded `Manage/Index.cshtml` markup, posting to the MVC `ProfilesController`
  actions above rather than the page's own Razor Page model - avoids a much riskier C# injection
  into scaffolder-generated Razor Page logic for a feature that doesn't need to share state with
  the rest of the page.

## Bug caught before it ever built
First draft had both new actions `RedirectToAction("Index", "Manage", new { area = "Identity" })`
after completing - wrong API entirely: Identity's Manage/Index is a **Razor Page**, not an MVC
controller action, so `RedirectToAction` doesn't apply to it at all. Caught by re-reading the code
before running it (not by a failed build), fixed to `RedirectToPage("/Account/Manage/Index", new {
area = "Identity" })`.

## Verification
- `dotnet build` + `dotnet test`: 111/111.
- Full clean `run --all` BG regenerate, verify PASSED on the first attempt (previous item 17 work
  in this same session had already resolved the Manage-page-specific pitfalls - residue-map
  exclusion, `_ViewImports` cascading, pipeline stage ordering - so this one composed cleanly on
  top of that foundation). Inspected generated output directly: the upload/remove forms render in
  `Manage/Index.cshtml`, both `ChangePicture`/`RemovePicture` actions exist on `ProfilesController`.

## Session milestone
This closes the round-2 23-item batch (21 tracked items after two were resolved as "already
working, no code needed" - item 4's receiver/address and the OrderDetails view were confirmed
already correctly wired during investigation). All 21 landed across individual commits (VERSION
6.3.1 through 6.3.15), each verified live via a full pipeline regenerate against real generated
output, not just unit tests.

## Affected files
- `templates/controllers/profiles_controller.sbn`
- `src/Genesis.Core/Stages/BuildStages.cs` (`InjectPfpFormIntoManageIndex`)
