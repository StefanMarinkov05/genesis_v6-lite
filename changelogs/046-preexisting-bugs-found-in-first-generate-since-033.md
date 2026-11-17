## 046 — Four pre-existing generation bugs caught by the first full run since changelog 033

**Type:** error
**Model:** Opus — same session's real-generate verification pass, root-causing four distinct bug classes.
**Date:** 2026-07-10

### Context

Verifying the Opus batch (tasks 10/26/7) required a full `run --all` generate — the first real end-to-end
run since changelog 033. Everything between (the Sonnet/Haiku batch: tasks 5, 13, 24a, 6, 8, 19, 2, 18,
25, 20, 21, 12, 14) was only test-verified at the Scriban-render level, never generated into a compiling
app. The run surfaced **four latent bugs, none from the Opus tasks** — all from that untested batch. Fixed:

### 1. `card_grid.sbn` — RZ1031: C# in `<option>` attribute area (from task 24a)

The sort dropdown emitted `<option value="asc" @(cond ? "selected" : "")>`. A bare `@(...)` in an option
tag-helper's attribute-declaration area is illegal Razor. **Fix:** `selected="@(boolCond)"` — Razor
renders the boolean attribute correctly (present when true, omitted when false). All three option blocks
(sortBy field options + both sortDir options) updated.

### 2. `layout.sbn` — CS0234: wrong context namespace (from task 5 cart badge)

`@inject FaceNote.Data.FaceNoteContext NavCartContext` — but the DbContext is emitted in the **root**
namespace `FaceNote` (per dbcontext.sbn `ns_open meta.root_namespace`), not `.Data`. **Fix:** inject
`{{ meta.root_namespace }}.{{ meta.project_name }}Context` (no `.Data`).

### 3. `catalogues_public.sbn` — CS1061: `Catalogue.User` doesn't exist (from task 25)

The owner-link used `cat.User.FirstName`, but `Catalogue` has only a string `UserId` (no navigation to
ApplicationUser). **Fix:** the controller's `Public()` action resolves owners into
`ViewData["Owners"]` (a `Dictionary<string, ApplicationUser>` via `_userManager.Users`, mirroring the
follows/profile pattern), and the view looks up `owners[cat.UserId]`. FirstName access guarded by a
Scriban `has_fn` check for specs without that property.

### 4. `seed_data.sbn` — runtime KeyNotFoundException on nullable props (surfaced by task 10, latent since task 16)

`SeedData.EnsureSeededAsync` read every property with `row.GetProperty("X")`, which **throws** when a
nullable property is absent from the seed JSON. Post.ImagePath (task 10) triggered it; Review.Text /
Review.ImagePath (task 16) were the same latent bug (their seed rows also omit the optional fields).
The generated app crashed on boot during seeding → verify's "did not boot within 180s". **Fix:**
seed_data.sbn now emits `row.TryGetProperty("X", out var xEl) ? xEl.… : null` for any `prop.nullable`
property (string/int/decimal/DateTime/bool), leaving required properties on the strict `GetProperty` path.

### Result

`run --all` now **verify PASSED: build, db, boot, http, manifest, language** — the FaceNote app compiles,
migrates, seeds, boots, serves every route, and passes the Bulgarian-language gate. First fully-green run
since 033, now covering the entire Sonnet/Haiku batch AND Opus tasks 10/26/7. Tests: 76/76.

### Lesson

The "test-verified at render level, DB run deferred" boundary that the Sonnet/Haiku batch ran under hides
exactly this class of bug (Razor tag-helper rules, cross-file namespace assumptions, absent-key JSON reads)
— none are visible without a real compile+boot. A full generate should follow any batch of template changes,
not just accumulate render-tested deltas.
