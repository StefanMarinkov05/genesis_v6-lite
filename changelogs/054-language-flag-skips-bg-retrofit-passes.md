# meta.language flag: "en" skips the Bulgarian retrofit passes

**Type:** Feature / change
**Model:** Sonnet — standard, well-scoped generator-logic gating following an existing pattern (the FileUploadService/UserProfileService emission-sync discipline already established in changelog 033); the one architectural judgement call (schema scope) was confirmed with Stefan via AskUserQuestion rather than decided solo.

## Summary
`spec.meta.language` now accepts `"en"` alongside `"bg"` (was `bg`-only). When `"en"`, the generator
skips every retrofit pass that force-translates the scaffolder's own output into Bulgarian, leaving
its native English output in place. FaceNote's own fixture already had `language: "bg"` set (both at
`meta` and `identity.errorDescriber` level) - confirmed, no fixture change needed.

## Scope boundary - read this before assuming "en" means a finished English app
Before touching any code, counted how much Bulgarian text actually lives in the codebase: **54
template files contain hardcoded Cyrillic strings** (`card_grid.sbn` alone has 935 Cyrillic
characters) - button labels, headers, comments, baked directly into hand-written `.sbn` templates,
NOT sourced from the spec. Those are a completely different thing from the ~4 **retrofit passes**
this change actually gates, which translate *scaffolded* (aspnet-codegenerator-produced) code that
would otherwise be in English by default.

Presented this gap to Stefan explicitly (a request that assumed "skip the Bulgarian translations" was
a small, simple switch) before implementing, since the two readings produce very differently-sized
work. Confirmed scope: gate only the retrofit passes now; the 54-template rewrite is a distinct,
much larger effort, deliberately NOT attempted here (same "don't half-ship a big item, defer with a
written boundary" discipline as A1/A8/A9 in earlier sessions).

## What "en" actually skips
1. `EmitControllersViewsStage.ApplyBgResiduePass` - the `bg-term-map.json` retrofit rewrite of the
   scaffolder's own "Back to List"/"Edit"/etc. residue. Skipped: scaffolder's native English stays.
2. `IdentityStage`'s two Identity-page localization layers (ADR-002b) - the `identity-bg-map.json`
   static replacement AND the wholesale Login/Register/Logout markup template emission. Both are
   retrofit overrides of scaffolded (English) Identity pages; skipped entirely for a non-Bulgarian
   project rather than attempting a parallel English variant of the same override.
3. `IdentityStage.InjectBgValidationMessages` - Bulgarian `ErrorMessage=` injection into scaffolded
   Identity `.cshtml.cs` InputModels. Skipped: the framework's own English messages are correct as-is.
4. `BgIdentityErrorDescriber` emission + its `.AddErrorDescriber<...>()` DI wiring in `program.sbn`
   (`{{ if meta.is_bulgarian }}`). Both gates read the SAME flag so they can't drift out of sync -
   emitting the DI line without the class is a CS0246 build break, the exact bug class this project
   has been bitten by before (FileUploadService, changelog 033).
5. `VerifyStage`'s "zero English string leaks" gate (the Bulgarian-only-UI enforcement mechanism
   itself) - skipped entirely for a non-Bulgarian project, since running it would hard-fail every
   single page of an app whose whole point is to be in English. Writes an explanatory
   `SKIPPED - ...` line to the report instead of silently omitting it.
6. `SeedStage`'s seed-generation LLM instructions - the hardcoded "in Bulgarian (Cyrillic)" rule is
   now the only branched rule (realism/variety/refs/scenario-coverage rules stay language-agnostic).

## What was deliberately NOT touched
- The 54 templates with hardcoded Cyrillic (out of scope per the confirmed boundary above).
- `SpecBuildStage`'s "Bulgarian UI language" LLM instruction - this stage drafts a spec that doesn't
  exist yet, so there's no `spec.Meta.Language` to read at that point; would need a new CLI input this
  session didn't add. Flagged, not silently skipped.
- Feature-template/model `errorMessage` content (spec-authored data, not generator code) - an English
  project's own spec/fixture would need to author English error messages itself; not a parser concern.

## Single source of truth
`MetaSection.IsBulgarian` (case-insensitive `Language == "bg"`) is the ONE property every gate reads -
both the C# stages and `program.sbn` (via `meta.is_bulgarian`) - so there's exactly one place to get
this wrong, not five independently-drifting checks.

## Real bug caught by the mandatory full pipeline run (not just unit tests)
`VerifyStage.ManifestCheck` still unconditionally expected `BgIdentityErrorDescriber.cs` on disk
regardless of language - a real English `run --all` failed verify's manifest check over a file that
was now correctly never emitted. Caught by actually running the pipeline end-to-end for a cloned
English fixture, not by trusting the 9 new unit tests (which never exercised the manifest check).
Fixed by adding the same `spec.Meta.IsBulgarian` gate to the manifest's expected-file list.

## Verification
- `dotnet build` clean; `dotnet test` **106/106** (97 baseline + 9 new: schema accepts `"en"`,
  FaceNote fixture confirmed `"bg"`, `MetaSection.IsBulgarian` case-insensitivity across 6 language
  values + the unset-defaults-true case).
- **Two full real `run --all` pipelines**, both to `verify PASSED`:
  - FaceNote (bg, unchanged fixture): `verify PASSED: build, db, boot, http, manifest, language` -
    zero regression on the production path.
  - A cloned `FaceNoteEn` fixture (same models/features, `language: "en"`): first attempt failed
    verify on the manifest bug above; after the fix, `verify PASSED: build, db, boot, http, manifest
    (language gate skipped - non-Bulgarian project)`.
  - Spot-checked the EN output directly: `Infrastructure/Identity/` directory does not exist at all,
    `Program.cs` has zero `AddErrorDescriber` occurrences, Login page shows "Log in" (not "Вход"),
    scaffolded Details view still shows "Back to List"/"Edit" (untranslated), and `[Required]` stays
    bare with no injected `ErrorMessage=` - contrasted directly against the BG output showing the
    opposite on every one of these points.
  - Smoke-test artifacts (the cloned fixture, its `out/FaceNoteEn` project, its archive entries)
    deleted afterward - this was a verification-only clone, not a permanent second fixture.

## Affected files
- `spec.schema.json` (`meta.language` enum widened to `["bg", "en"]`)
- `src/Genesis.Core/Spec/ProjectSpec.cs` (`MetaSection.IsBulgarian`)
- `src/Genesis.Core/Stages/BuildStages.cs` (`IdentityStage`: 3 gates)
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs` (`ApplyBgResiduePass` early return)
- `src/Genesis.Core/Stages/VerifyAndReportStages.cs` (manifest check + English-leak gate skip)
- `src/Genesis.Core/Stages/SeedStage.cs` (seed-LLM instruction branch)
- `templates/project/program.sbn` (`AddErrorDescriber` + `using` gated on `meta.is_bulgarian`)
- `tests/Genesis.Tests/CoreTests.cs` (9 new tests)
