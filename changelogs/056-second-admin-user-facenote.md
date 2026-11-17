# Second admin seed user for FaceNote

**Type:** Feature / change
**Model:** Sonnet — mechanical, pattern-copy data addition (one more seed-user entry + matching seed rows), fully self-validated by replicating `SeedStage.BuildSeedSchema`'s exact rules in a script rather than by running the pipeline, per the explicit "don't run them" instruction.

## Summary
Added a second admin seed user (`petar@facenote.bg`) to the FaceNote spec + seed data, without
running the pipeline (explicitly requested). Appended at the END of the seed-user list (index 5)
rather than inserted earlier, specifically to avoid renumbering any of the existing `$seedUser:0..4`
references scattered across the seed data - a purely additive, lower-risk edit.

## What changed
- `fixtures/project-spec.facenote.json` + its archived mirror `prompts/out/schemas/facenote-schema.json`:
  `identity.seedUsers.users` grows from 5 to 6, new entry `{ "email": "petar@facenote.bg", "role":
  "$variation:adminRoleName" }` - now 2 admins (`admin@facenote.bg`, `petar@facenote.bg`) + 4 regular
  users, matching the existing Bulgarian-first-name convention (georgi/maria/ivan) for the name choice.
- `prompts/in/seed-response.json` + its archived mirror `prompts/out/seeds/facenote-seed.json`:
  - 2 new `Catalogue` rows owned by `$seedUser:5` ("Работно бюро", "Пътуване и почивка") - REQUIRED,
    not just for realism: `SeedStage`'s auto-fill sets Catalogue's row minimum to
    `max(4, seedUserCount * 2)`, which jumps from 10 (5 users) to 12 (6 users) the moment the spec
    gains a 6th user - the exact same coupling Session 19 hit going from 2→5 users. Left uncorrected,
    the next real `seed` run would find the existing seed-response invalid (`MinItems` violation) and
    block on a fresh manual LLM paste.
  - 1 new `CatalogueItem` row linking one of the new catalogues to an existing product (light realism
    touch, not schema-required - the other new catalogue is left empty, matching the existing data's
    own inconsistency where not every catalogue already has an item).

## Self-validation (no pipeline run, per instruction)
Replicated `SeedStage.BuildSeedSchema`'s actual rules in a script and ran it against the edited
files directly - not a guess, the same rules the real seed stage would enforce:
- Seed user count: 6, 2 of them admin-role. ✓
- Catalogue row count (12) meets the new scaled minimum (`max(4, 6*2)` = 12). ✓
- Every Catalogue row has all 4 required fields (`refKey`/`Name`/`UserId`/`IsPublic`) and refKeys
  are unique. ✓
- Every `CatalogueItem` row's `parentRef_Catalogue`/`parentRef_Product` resolves to a real refKey. ✓
- Highest `$seedUser:N` reference anywhere in the seed data (5) is within range for 6 users. ✓
- All 4 touched files re-parsed as valid JSON after editing. ✓
- `dotnet build`/`dotnet test` unaffected (JSON data changes only, no source touched): still
  **111/111** green.

## Known follow-up needed before the next real generate (flagged, not fixed here)
`fixtures/project-spec.facenote.lock.json` and `prompts/out/schemas/facenote-schema.lock.json` are
now STALE - they were expanded from the pre-edit raw spec and still show only 5 seed users.
`run --all`'s own spec-expand pre-check only re-runs expansion when `plan/state.json` doesn't
already mark it done, so simply re-running `run --all --resume` would silently keep using the STALE
lock file and never pick up the new admin. Deliberately did NOT hand-edit the lock files myself:
correctly reproducing them requires the resolved role name AND the deterministic seed password
(`ResolvedVariation.SeedPassword`), and guessing that algorithm by hand without being able to run
and verify it is exactly the kind of unverified edit this project's discipline warns against.
**Next real run needs an explicit `dps-nn spec expand --spec fixtures/project-spec.facenote.json`
first** (or an equivalent state reset) to regenerate the lock file fresh from the now-6-user raw spec.

## Affected files
- `fixtures/project-spec.facenote.json`
- `prompts/out/schemas/facenote-schema.json`
- `prompts/in/seed-response.json`
- `prompts/out/seeds/facenote-seed.json`
