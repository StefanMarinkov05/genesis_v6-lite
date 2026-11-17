**Type:** Feature / change
**Model:** Sonnet — standard, well-scoped data/tooling work against an existing contract (the seed JSON schema built by `SeedStage.BuildSeedSchema`).

## Summary

There was no seed file to pick in the GUI/CLI seed picker (`seeds/` did not exist), so every run fell
into the LLM/manual exchange. Adds a large, deterministic, Bulgarian seed for
`fixtures/project-spec.facenote.json` plus matching dummy product images, both produced by one script.

## Techniques

- `scripts/gen-rich-seed.py` writes `seeds/facenote-rich-seed.json` with every model the FaceNote
  spec enables (13 models, ~1.2k rows: 70 products with 2-4 image keywords each, 60 posts, 240
  comments, ~180 votes, 48 catalogues / ~160 items, carts, 38 orders / ~100 items, 26 follows, 130
  reviews). Owner fields use `$seedUser:0..5` (the spec defines exactly 6 seed users, so follows are
  capped at the 30 possible pairs).
- Images are drawn with Pillow (Cyrillic labels, per-category colours, a different pattern per view)
  and written into the persistent `.image-cache/` under the `pexels-`, `picsum-` and `pixabay-`
  prefixes. `SeedStage.DownloadImageAsync` consults that cache before any network call (ADR-011), so
  runs need no `--image-pack`, no API keys and no downloads.
- Post / PostComment / Review `ImagePath` values point at `images/products/product-{row}-{img}.jpg`,
  the files the seed stage already writes, because nothing else copies images into `wwwroot`.
- The script self-checks before writing: a Python mirror of `BuildSeedSchema` (incl. minimum row
  counts and `additionalProperties:false`), referential integrity of every `refKey`/`parentRef_*`,
  unique descriptive strings, order totals, image-path existence, and the `VerifyStage.EnglishLeaks`
  heuristic over every string value.

## Affected files

- `scripts/gen-rich-seed.py` (new)
- `seeds/facenote-rich-seed.json` (new, generated)
- `.image-cache/*` (generated, gitignored)

Not verified against the real pipeline: `dotnet` is not installed on the machine this was written on,
so `dotnet test` / a full `run --all` still need to be run on a machine with the SDK.
