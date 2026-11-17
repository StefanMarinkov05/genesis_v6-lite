# 087 — Image pipeline implementation (A4)

**Type:** Feature / bug fix
**Model:** Opus, high effort, thinking on — Group A4 Sonnet-designated implementation half,
picked up directly at Opus per Stefan's explicit request for one substantial task after a limit
reset. Implements the design in `docs/explanation/image-pipeline.md` / ADR-011 (changelog 085)
verbatim, per its own checklist.

## Summary

Implements all four pillars of the ADR-011 redesign, closing three real, verified-broken defects:
downloads that never survived a regenerate, profile pictures broken at 4 of 5 render sites, and a
fallback image that was referenced but never shipped.

1. **Persistent cross-run image cache.** `SeedStage.DownloadImageAsync` now resolves a cache path
   under `{genesisRoot}/.image-cache/` (keyed `{provider}-{slugified-keyword}.jpg`) BEFORE hitting
   any network provider or local pack. Cache hits copy straight to the target with zero network
   calls. This lives outside `out/`, which since changelog 019 is a fresh timestamped folder every
   run — the old `File.Exists(targetPath)` check could never actually cache across regenerates.
   `.image-cache/` added to `.gitignore`.
2. **Shipped generic fallbacks, as SVG.** New `images/avatar-default.svg` and
   `images/product-default.svg` (hand-authored, simple neutral silhouettes, no binary asset).
   `ScaffoldStage.CopyStaticAssets` copies every `images/*.svg` into each generated project's
   `wwwroot/images/` right after `dotnet new mvc` creates `wwwroot`, before the stage's final
   build — overwrite-safe so asset updates always propagate on the next run.
3. **Local-only profile pictures.** `identity_seeder.sbn` no longer seeds
   `ProfilePicturePath = "https://picsum.photos/..."` — the assignment is removed entirely, so
   every seeded user's PFP stays `null` and falls back to the shipped `avatar-default.svg`. This
   also fixes a **real, live bug**: 4 of 5 avatar render sites did `"/" + ProfilePicturePath`,
   which for a full URL produced the broken path `/https://picsum.photos/...` — seeded avatars
   were non-functional before this change. All 6 avatar-rendering sites (`layout.sbn`,
   `profiles_public.sbn`, `users_details.sbn`, `user_profile_content_partial.sbn` ×2,
   `users_edit.sbn`) now uniformly fall back to `/images/avatar-default.svg`; `users_edit.sbn`'s
   now-dead `StartsWith("http")` special-case (the one site that guarded correctly) was removed
   so all sites share one identical form.
4. **Product fallback on missing/failed image.** `DownloadImageAsync` no longer writes a 1×1 grey
   JPEG placeholder on download failure — it leaves the target file absent and lets the render
   layer show the real fallback. Three render sites updated: `card_grid.sbn` (was: silently no
   `<img>` at all when a product has zero images), `product_details.sbn`'s carousel (was: a plain
   gray "Няма снимки" text box), and `admin_table.sbn`'s generic `*Path` column renderer (was: an
   em-dash) — all three now show `/images/product-default.svg`.

## Verification

- `dotnet build` (whole solution) and `dotnet test`: 112/112 green, unchanged count — every
  modified template (`card_grid.sbn`, `product_details.sbn`, `admin_table.sbn`, `users_edit.sbn`)
  is already exercised by existing render tests, confirming Scriban syntax validity across all
  edits with zero new failures.
- Full clean `run --all` regenerate against the FaceNote fixture (cleared `out/FaceNote` +
  `.lock.json` first) — **exit 0, verify PASSED** (build/db/boot/http/manifest/language). Verified
  directly against the real generated output:
  - `.image-cache/`: 24 provider images persisted at repo root (survived the `out/` wipe). A
    generated `wwwroot/images/products/product-1.jpg` is **byte-identical** (md5 match) to its
    `.image-cache/pexels-running-shoes.jpg` source — proving copy-from-cache, so the next
    regenerate skips the download entirely.
  - `wwwroot/images/`: `avatar-default.svg` + `product-default.svg` copied in (log line "copied
    static image fallbacks to wwwroot/images/").
  - `Data/IdentitySeeder.cs`: zero `picsum` refs, zero `ProfilePicturePath` assignments.
  - `avatar-default.svg` referenced in all 5 avatar views (_Layout, _UserProfileContent,
    Users/Details, Users/Edit, Profiles/Public); `product-default.svg` in Products Index/Details +
    3 admin `*Path` tables (Posts, PostComments, Reviews).

## Affected files

- `.gitignore` (`.image-cache/`)
- `images/avatar-default.svg`, `images/product-default.svg` (new)
- `src/Genesis.Core/Stages/BuildStages.cs` (`ScaffoldStage.CopyStaticAssets`)
- `src/Genesis.Core/Stages/SeedStage.cs` (`DownloadImageAsync` cache + no-placeholder-on-failure)
- `templates/identity/identity_seeder.sbn`
- `templates/views/layout.sbn`, `profiles_public.sbn`, `users_details.sbn`,
  `user_profile_content_partial.sbn`, `users_edit.sbn`, `card_grid.sbn`, `product_details.sbn`,
  `admin_table.sbn`

## Design reference

`docs/explanation/image-pipeline.md` + ADR-011 (changelog 085) — this entry implements that
design's checklist items 1-7 verbatim; item 8 (build/test) and item 9 (full regenerate) are this
entry's own verification.
