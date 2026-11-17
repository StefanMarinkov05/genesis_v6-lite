# First fully-green end-to-end pipeline run + the two real bugs it caught

**Type:** Error fix (2) + verification milestone
**Model:** Sonnet — real-run bug fixes within an established pattern (emission-order mirroring, timeout tuning).

## Summary
Ran the real generator end-to-end (`run --all --resume` against the FaceNote fixture into LocalDB) to
clear the accumulated verification debt from Packages A/K, H, and E (tasks 15/16). All prerequisites were
present on this machine (dotnet-ef 10.0.9, aspnet-codegenerator, MSSQLLocalDB). The run caught two real
bugs that build+test-on-the-generator could NOT see, both now fixed - after which the pipeline went
**fully green**: `verify PASSED: build, db, boot, http, manifest, language`, with all 5 seed users in
the completion banner. This is the first time the pipeline has ever reached (and passed) the verify stage.

## Bug 1 - `FileUploadService` missing at the IdentityStage build gate

**Where:** generated `Program.cs` line 35 -> `error CS0246: 'FileUploadService' could not be found`, thrown
during IdentityStage (which builds the project to run aspnet-codegenerator).

**How it happened:** changelog 031 made `program.sbn` register `AddScoped<FileUploadService>()`. But
`IdentityStage` emits `Program.cs` and then builds/scaffolds BEFORE `EmitControllersViewsStage` (which is
where `FileUploadService.cs` is emitted). `IdentityStage` already pre-emits `ImageService.cs` for exactly
this reason ("Program.cs is the convergence point: every symbol it mentions must exist by this stage's
build gate") - I added a new symbol to Program.cs without adding the matching pre-emission.

**Why build+test missed it:** the generator's own test suite compiles the *generator*, not a *generated
project*. Only a real run builds the generated app, where cross-stage emission ordering matters.

**Fix:** `BuildStages.cs` IdentityStage now also pre-emits `Services/FileUploadService.cs`, gated on the
same condition as program.sbn (`ProfilePicturePath present || rating enabled`), right beside the existing
ImageService pre-emit.

## Bug 2 - verify boot timeout too tight for a 5-user cold seed

**Where:** VerifyStage -> `TimeoutException: verify: app did not boot within 60s`.

**How it happened:** first app boot runs in-context seeding - creating each seed user goes through
Identity's deliberately-slow PBKDF2 password hashing, plus inserting every feature model's rows. Package K
took seed users 2 -> 5, pushing cold-boot past the 60s budget. Confirmed it was slowness, not a crash: a
manual `dotnet run` of the generated app reached "Now listening on: http://localhost:5050" with no errors.

**Fix:** `VerifyAndReportStages.cs` boot window raised 60s -> 180s via a named `BootWaitSeconds` constant
with a comment explaining the seed-cost reason. A genuinely crashed app still fails (its GETs keep
throwing for the whole window); a healthy-but-slow boot just gets the room. Re-running verify passed in 26s.

## Verification (the whole point)
Fully-green run against `fixtures/project-spec.facenote.json`:
- **build green** - every template change from A/K/H/E compiles in a real generated project.
- **migration applied** - EF migrations (incl. the new `Review.ImagePath` column) succeed; first time the
  pipeline has ever cleared the ef stage (the sessions 13-18 saga).
- **db + boot + http** - app boots, serves 200s across the discovered route surface.
- **language** - the whole-route Bulgarian gate passes on real rendered HTML (validates the task-1
  displayName hard gate + localization end-to-end).
- Banner shows all **5 seed users** (admin + georgi/maria/ivan/user) with working credentials.

## Note - archival MOVE restored
A successful `run --all` MOVES the used spec + seed into `prompts/out/schemas|seeds/` as history
(Session 12 behavior). That deleted `fixtures/project-spec.facenote.json` and
`prompts/in/seed-response.json` from their canonical locations (which would break `Paths.Fixture` in the
tests). Restored both from the archive; `dotnet test` back to 50/50. This MOVE-on-the-canonical-fixture
behavior is a workflow hazard flagged to Stefan for a follow-up (copy-not-move, or exclude the active
fixture).

## Affected files
- `src/Genesis.Core/Stages/BuildStages.cs`
- `src/Genesis.Core/Stages/VerifyAndReportStages.cs`
