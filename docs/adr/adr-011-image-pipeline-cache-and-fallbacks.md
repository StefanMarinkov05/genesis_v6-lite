<!-- last-verified: 33c4fd4265056f5f769086261ff358cad3d31c5d -->
# ADR-011: Persistent image cache + shipped local fallbacks (no served URLs)

**Status**: accepted (design); not yet implemented

**Context**: The generated apps' image handling has three verified defects. (1) The download
"cache" (`SeedStage.DownloadImageAsync`'s `File.Exists` short-circuit) lives under
`out/run_<timestamp>/` — a fresh folder per run since changelog 019 — so every regenerate
re-downloads the same keywords. (2) Profile pictures are seeded as raw `picsum.photos` URLs and
served live; 4 of 5 render sites additionally break them by prefixing `/`, so seeded avatars are
non-functional today. (3) The `/images/avatar-default.png` fallback the views reference is never
emitted (404), and product download failures write a 1×1 PNG rather than a real placeholder.

**Decision**: redesign the image pipeline along four lines — (a) a **persistent cache** at
`.image-cache/` in the workspace (cwd; ADR-014 — gitignored, outside `out/`) that survives the per-run folders;
(b) **shipped generic fallbacks as SVG** (`images/avatar-default.svg`, `images/product-default.svg`),
copied into each generated `wwwroot/images/` by a new `ScaffoldStage.CopyStaticAssets` step —
SVG over binary PNG because a generator repo should carry text, diffable, scalable assets;
(c) **local-only profile pictures** — the identity seeder stops emitting picsum URLs and seeds
`ProfilePicturePath = null` (generic fallback), making every stored value relative and the five
render sites uniformly correct (the `StartsWith("http")` special-case is removed, collapsing the
second code path); (d) **product fallback** to `product-default.svg` on missing/failed image
instead of a 1×1 placeholder. Distinct-per-user real avatars are explicitly out of scope
(privacy + over-engineering); "generic fallback" is the requested and correct baseline.

**Full design**: `docs/explanation/image-pipeline.md` (defects, the four pillars, spec/seed
impact, implementation checklist). This ADR is the index entry; that doc is the single source
of truth.

**Consequences**: regenerates stop hammering the image providers; no generated page ever depends
on an external image host at render time; the fallbacks actually resolve; one avatar-render code
path instead of two divergent ones. Tripwire: if a future feature genuinely needs distinct
seeded avatars, layer local downloaded avatars on top of this local-path base — never
re-introduce URL-valued `ProfilePicturePath`, or defect (2) returns.
