**Type:** Feature / change
**Model:** Sonnet — standard multi-file feature work following an established pattern (schema
field → C# stage logic → Scriban template), plus a real operational bug caught and fixed during
manual regeneration (stale smart-resume DB reuse).

## Summary

`images.mode: "multiple"` (spec.schema.json) already drove the full runtime/admin gallery UI
(carousel, per-model `{Model}Image` table, admin upload gallery — shipped in changelog 042), but
the SEED pipeline itself always downloaded and inserted exactly ONE image per product regardless
of `mode` — an explicitly logged-but-never-fixed gap (`plan/state.md` Session 22: "2nd-image-per-
product explicitly deferred"). This closes that gap: seeded products with `images.mode:
"multiple"` now get 2-4 real images each; `"single"` mode is unchanged (still exactly 1).

## Changes

- **`src/Genesis.Core/Stages/SeedStage.cs`**:
  - `BuildSeedSchema`: the per-row `imageKeyword` (single required string) is now
    `imageKeywords` (required array of strings), sized `1..1` for `mode:"single"` and `2..4` for
    `mode:"multiple"` — enforced by JSON Schema `minItems`/`maxItems`, so a manual or LLM seed
    response can't under/over-supply images for either mode.
  - LLM seed-generation prompt updated to ask for varied angles/keywords per array entry (e.g.
    "red sneakers", "sneakers side view") rather than one generic keyword repeated.
  - The image-download loop now iterates each row's `imageKeywords` array, downloading one file
    per entry. Filenames changed from `{model}-{row}.jpg` to `{model}-{row}-{img}.jpg` — both this
    loop and `seed_data.sbn`'s runtime insert independently compute the identical pattern from the
    same seed-data.json, so they stay in sync without passing state between them (same design as
    before, just with an added image-index segment).
- **`templates/models/seed_data.sbn`**: the per-row `{Model}Image` insert is now a loop over the
  row's `imageKeywords` array length (read from `Data/seed-data.json` at runtime, not fixed),
  inserting one row per image with the matching `{model}-{row}-{img}.jpg` path.
- **Fixtures updated to the new shape** (both would otherwise fail the tightened schema on next
  use): `fixtures/seed-response.facenote.json` (15 Product rows, `imageKeyword` → `imageKeywords:
  [kw, kw+" closeup"]`) and `prompts/in/seed-response.json` (24 Product rows, same conversion,
  gitignored/local-only but kept correct so the manual-response reuse path still works).
- **`tests/Genesis.Tests/CoreTests.cs`**: updated the one assertion checking for the literal
  `"imageKeyword"` substring in the generated schema to `"imageKeywords"`.

## Regeneration + a real bug caught along the way

Deleted `out/` and regenerated FaceNote (BG fixture) end-to-end via
`dotnet run --project src/Genesis.Cli -- run --all --spec fixtures/project-spec.facenote.json`.

First attempt silently reused the OLD database: `logs/FaceNote/db.json`'s smart-resume record
(`dbName: FaceNote_20260712124423`) happened to still be on disk from the deleted run, and since
the new run's connection string derivation didn't change the DB name check landed as a match,
migrations were skipped ("миграции: вече приложени... — пропускане"), and `SeedData.cs`'s
`AnyAsync()` early-return guard then skipped seeding entirely against the stale, already-populated
DB — meaning the freshly-generated code silently ran against year-old single-image data. Caught by
directly querying `ProductImages` after the first run and finding the OLD `product-N.jpg` paths
instead of the new `product-N-M.jpg` ones. Fixed by dropping the stale LocalDB database and
deleting `logs/FaceNote/db.json` before re-running — the second run applied migrations fresh and
verified correctly: **48 `ProductImage` rows across 24 products (exactly 2 each)**, all real
downloaded JPEGs (34-103KB), matching the new `product-{row}-{img}.jpg` naming on disk.

This is a real operational trap worth remembering for future "delete and regenerate" requests:
deleting `out/` alone does NOT force a fresh database if the project name (hence derived DB name in
this case) collides with a still-recorded `logs/<Project>/db.json` smart-resume entry — that log
file (or the actual DB) needs clearing too.

## Affected files

`src/Genesis.Core/Stages/SeedStage.cs`, `templates/models/seed_data.sbn`,
`fixtures/seed-response.facenote.json`, `prompts/in/seed-response.json` (gitignored),
`tests/Genesis.Tests/CoreTests.cs`.
