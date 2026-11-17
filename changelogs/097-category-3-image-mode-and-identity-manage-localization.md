# 097 — Category 3 batch: images.mode:"single" UI gap + Group A2 (Identity/Manage localization)

**Type:** Bug fix / feature
**Model:** Sonnet, medium effort (image-mode) then high effort thinking-on (A2) — per the task's
own sizing; A2 explicitly flagged as historically fragile, traps read from
`docs/explanation/techniques.md` before touching anything.

## Part 1 — `images.mode: "single"` UI gap

`admin_product_images.sbn` (the only admin image-management view, emitted for every image-bearing
model) was hardcoded to a multi-file-upload gallery UI regardless of the model's configured
`images.mode`. `ImageService.Save{Model}ImageAsync` already correctly implemented single-mode
semantics server-side (delete existing before saving new), but the UI never reflected it.

Investigation found the scope smaller than expected: the client-facing carousel
(`product_details.sbn`) and the card-grid thumbnail (`card_grid.sbn`) **already** degrade
gracefully to a single plain image with no gallery/carousel chrome when there's only one image —
no changes needed there. Only the admin view needed a real fix.

**Fix**: `admin_product_images.sbn` now branches on `model.images.mode`. `"single"` renders a
"current photo, replace it" UI (single file input, no `multiple` attribute, no gallery grid,
"Премахни снимката" instead of a per-image delete-among-many). `"multiple"` keeps the existing
gallery unchanged. New test `Single_mode_image_view_shows_replace_ui_not_gallery` (hand-built
single-image model, since the real fixture's only image-bearing model — Product — is always
`"multiple"`). Verified with a real regenerate: verify PASSED, the real `"multiple"`-mode Product
path confirmed byte-identical to before.

## Part 2 — Group A2: Identity/Account/Manage English leaks

Read `docs/explanation/techniques.md`'s traps first, as instructed. Two governed this area:

- **The BG residue-map trap**: `ApplyReplacementMapAsync` already excludes the whole `Manage/`
  subtree (a blind map would corrupt `ManageNavPages.Email`/`EmailModel`-style identifiers —
  confirmed in changelog 070). This meant the only prior localization touching `Manage/` was two
  narrow, hand-anchored transforms: `RestructureManageNav` (nav link cleanup) and
  `InjectPfpFormIntoManageIndex` (profile-picture form injection) — everything else in these
  pages (titles, labels, buttons, watermarks, validation messages) was still 100% scaffolded
  English.
- **Two separate `_ViewImports.cshtml` layers**: relevant context, not directly touched here.

**Root finding that shaped the whole fix**: the scaffolded Manage pages' `<label asp-for="...">`
tags are **empty** in markup (`<label asp-for="Input.PhoneNumber" class="form-label"></label>`) —
their visible text comes from `[Display(Name="...")]` in the C# code-behind. This meant
ADR-002b's own proven technique for Login/Register/Logout (wholesale hand-written **markup**,
scaffolded `.cshtml.cs` left untouched) transfers cleanly: explicit Bulgarian label text in the
new markup overrides `Display(Name)` automatically, so labels/buttons/headings/watermarks needed
zero code-behind changes at all.

### What shipped

- **5 new wholesale templates** (`templates/identity/manage_{index,email,change_password}_page.sbn`,
  `manage_nav_partial.sbn`, `manage_layout_partial.sbn`), wired into the same emission loop
  `BuildStages.cs` already uses for Login/Register/Logout. The old `RestructureManageNav` /
  `InjectPfpFormIntoManageIndex` surgical transforms are folded directly into the new
  `_ManageNav`/`Index` templates and their function bodies **removed** (now redundant — the
  wholesale template already contains their effect from the start).
- **`InjectBgValidationMessages` extended**: scan widened from `Account/` (`TopDirectoryOnly`) to
  also cover `Account/Manage/` (`AllDirectories`) — the scaffolder only creates subdirectories for
  requested modules, so nothing else gets swept in. Added `NewEmail` to the display-name table.
  Added a new `knownEnglishMessages` replacement pass for the two attributes that already carried
  an explicit (English) `ErrorMessage` from the scaffolder — `StringLength`/`Compare` on
  `ChangePasswordModel` — which the existing "fill in the blank" logic intentionally skips
  (it only adds a *missing* message, never overwrites one that's already present).
- **New `InjectBgStatusMessages`**: the 6 `TempData StatusMessage` C# string-literal assignments
  across Index/Email/ChangePassword's code-behind ("Your profile has been updated", etc.) are a
  different category from DataAnnotations — plain string literals, not attributes — handled by a
  small, separate, exact-known-literal replacement (same discipline as the residue-map trap
  teaches: never a fragment, never blind).

### A real, pre-existing bug found and fixed (not introduced by this session)

Widening the validation-message scan to `Manage/` surfaced a genuine bug in the **existing**
lookahead logic: `InjectBgValidationMessages` looks ahead a fixed 3 lines from a bare attribute to
find the property name it decorates. `ChangePasswordModel.NewPassword` stacks
`Required`+`StringLength`+`DataType`+`Display` before its own property line — 4 lines away, one
past the old window — producing `[Required(ErrorMessage = "Полето  е задължително.")]` (blank
display name, double space). **The exact same bug already existed in the shipped
`Register.cshtml.cs`** (its `Password` property has the identical attribute depth) — confirmed by
grepping the pre-fix output, not assumed. Fixed by widening the lookahead window from 3 to 9 lines;
`propMatch` only ever matches a real `public ... {` declaration, so a wider window can't grab the
wrong property, it just stops at the first one found. Verified fixed in both `ChangePassword.cshtml.cs`
and `Register.cshtml.cs` in the real regenerated output.

### Verified

Two full clean `run --all` regenerates (first surfaced the pre-existing bug above; second
confirmed the fix). Final run: **verify PASSED** (build/db/boot/http/manifest/language), 121/121
tests. Directly inspected every real generated Manage file — all labels, headings, buttons,
watermarks, and validation/status messages are Bulgarian. Swept for residual English with a
targeted grep; found two accepted, pre-existing-pattern residuals:
- `[Display(Name = "Phone number")]` etc. left in code-behind — **zero visible effect** (explicit
  markup labels already override it); confirmed `Login.cshtml.cs`'s own `[Display(Name = "Remember me?")]`
  has had the identical harmless residual all along, so this isn't a new gap.
- The outgoing **email body/subject** text (`"Please confirm your account by ... clicking here"`,
  `"Confirm your email"`) sent via `_emailSender.SendEmailAsync` is still English — this is a
  genuinely different category (mail content, not a rendered browser page) that the original
  "Identity/Account/Manage" framing didn't clearly include. **Not fixed here** — flagged as a real,
  separate follow-up in `tasks.md` rather than silently expanding scope or silently ignoring it.

## Affected files

- `templates/views/admin_product_images.sbn`
- `templates/identity/manage_index_page.sbn`, `manage_email_page.sbn`,
  `manage_change_password_page.sbn`, `manage_nav_partial.sbn`, `manage_layout_partial.sbn` (new)
- `src/Genesis.Core/Stages/BuildStages.cs` (emission wiring; `RestructureManageNav`/
  `InjectPfpFormIntoManageIndex` removed; `InjectBgValidationMessages` extended;
  `InjectBgStatusMessages` new; lookahead-window bug fix)
- `tests/Genesis.Tests/CoreTests.cs` (1 new test, image-mode)
- `tasks.md`
