<!-- last-verified: 33c4fd4265056f5f769086261ff358cad3d31c5d -->
# Image pipeline (redesign) — design spec

**Status**: design (Group A4 Opus half). Implementation is the Sonnet half. Index entry:
[ADR-011](../adr/adr-011-image-pipeline-cache-and-fallbacks.md). This doc is the single source
of truth for the detail.

## Why this exists — three concrete defects in the current pipeline

All three are verified against the real code, not assumed:

1. **Downloads repeat on every regenerate.** `SeedStage.DownloadImageAsync` short-circuits with
   `if (File.Exists(targetPath)) return;`, but `targetPath` lives under `ctx.ProjectDir` —
   which since changelog 019 (Session 11/12) is a fresh `out/run_<timestamp>/<Project>/…` folder
   **per run**. So the "cache" is inside disposable output and never survives a clean regenerate;
   every run re-hits Pexels/Pixabay/Picsum for the identical keyword set.

2. **Raw external URLs are served, worst on Admin/Users.** `identity_seeder.sbn` bakes
   `ProfilePicturePath = "https://picsum.photos/150/150?random={i}"` into runtime seed data — a
   live external hit on every avatar render. Worse, 4 of the 5 avatar render sites do
   `"/" + ProfilePicturePath`, producing the broken path `/https://picsum.photos/…` (only
   `users_edit.sbn` guards with `StartsWith("http")`). **Seeded avatars are effectively broken
   today** at layout nav, public profile, user details, and both profile-content partial sites.

3. **The generic fallbacks don't exist.** `/images/avatar-default.png` is referenced by 5 views
   but is **never emitted** (the repo `images/` folder holds only a README) — so the fallback is
   a 404. There is no product fallback at all; on download failure `DownloadImageAsync` writes a
   1×1 transparent PNG (visible as a broken tile, not a real placeholder).

## The four design pillars

### 1. A persistent, cross-run image cache (download-once, for real)

Introduce a repo-root cache directory **outside** the `out/` tree, so it survives the
per-run timestamped folders:

- Location: `{cwd}/.image-cache/` — the workspace: the repo root in a checkout, `Documents\DPS-NN`
  when installed (moved off `GenesisRoot`, which is read-only when installed — ADR-014).
  Add `.image-cache/` to `.gitignore` (local, disposable, but persistent across runs).
- Key: `{provider}-{slug(keyword)}.jpg` where `slug` lowercases + strips non-alphanumerics
  (Picsum already seeds by keyword, so keyword→image is stable and cacheable).
- New `DownloadImageAsync` order of resolution:
  1. `imagePackDir` override (unchanged — local pack still wins).
  2. **Cache hit** → `File.Copy(cachePath, targetPath)`, no network.
  3. Miss → download → write to **cache** → copy cache→target.
- Keep the existing per-target `if (File.Exists(targetPath)) return;` for intra-run idempotency.

The cache is permanent until manually cleared. No max-age / `--refresh-images` flag — keyword→
image is intentionally stable; adding invalidation is scope the workload doesn't need.

### 2. Shipped generic fallbacks — as SVG, not binary PNG

Author real fallback assets in the repo `images/` drop-folder, **as SVG text files**, not
binaries:

- `images/avatar-default.svg` — neutral silhouette avatar.
- `images/product-default.svg` — neutral product placeholder.

SVG is chosen deliberately: it renders in `<img>`, is scalable, tiny, text-diffable, and
version-controllable — a generator repo should not carry hand-authored binary PNGs when a text
asset does the job better. (This also removes the "how do we author a PNG in this pipeline"
problem entirely.)

A new **static-asset copy step** places them into every generated project:
`CopyStaticAssets(ctx)` copies `{genesisRoot}/images/*.svg` → `{projectDir}/wwwroot/images/`.
Run it in `ScaffoldStage` (right after `dotnet new mvc` creates `wwwroot`), idempotent
(overwrite OK). Update the 5 render sites' fallback references from `avatar-default.png` to
`avatar-default.svg`.

### 3. Profile pictures become local, never URLs

- `identity_seeder.sbn`: **stop seeding the picsum URL.** Seed `ProfilePicturePath = null`
  (or `""`). Every seeded user then renders the shipped generic `/images/avatar-default.svg`.
  This is exactly the "generic PFP fallback" the task asks for — distinct-per-user real faces
  are deliberately **out of scope** (privacy-fraught with random real photos, and over-
  engineering; a later enhancement can layer distinct local avatars on top of this correct base).
- Uploaded PFPs already store a **relative** path (`FileUploadService.SaveImageAsync` →
  `"uploads/avatars/<guid>.jpg"`). So once URL-seeding is gone, **every** `ProfilePicturePath`
  value is relative, and the render sites become uniformly correct.
- **Unify the 5 render sites** on one form: `string.IsNullOrEmpty(p) ? "/images/avatar-default.svg" : "/" + p`.
  The `StartsWith("http")` special-case in `users_edit.sbn` becomes dead (no value is a URL
  anymore) — remove it so all five sites are identical and there is no second code path to drift.
  (Two paths computing one concept with different rules is the exact bug class that bit sessions
  5/8/33 — collapse it.)

### 4. Product fallback on missing / failed image

- `DownloadImageAsync`: on download failure, **stop writing the 1×1 PNG.** Leave the target
  absent and let the render layer fall back.
- Render layer (card grid, product details, admin table `*Path` column): when a product has no
  image / the file is missing, render `/images/product-default.svg`. The admin table already
  renders relative `*Path` props as local `<img src="/@item.Path">` — extend it (and the card
  grid / product details) to emit the product-default fallback when the value is empty.

## Spec / seed impact

- **No new required spec fields.** `images.storedIn` / `images.mode` and the seed `imageKeyword`
  requirement are unchanged.
- **Seed data**: PFP leaves the seed surface entirely (it was always identity-seeder-generated,
  not in `seed-response.json`). Product image rows are unchanged.
- **Repo assets**: adds `images/avatar-default.svg`, `images/product-default.svg`, and a
  gitignored `.image-cache/`.
- **Verify**: the extra `wwwroot/images/*.svg` copy and the cache are inert to the language gate
  and manifest checks. No `VerifyStage` change expected, but confirm the manifest's expected-file
  list isn't accidentally tightened.

## Implementation checklist (for the Sonnet half)

1. `SeedStage.DownloadImageAsync` — cache-dir resolution + copy-through; drop the 1×1 fallback.
2. `.gitignore` — add `.image-cache/`.
3. `images/avatar-default.svg`, `images/product-default.svg` — author the two assets.
4. `ScaffoldStage` — `CopyStaticAssets` step.
5. `identity_seeder.sbn` — seed `ProfilePicturePath = null`.
6. The 5 avatar render sites — unify on the `.svg` fallback form; drop the `http` guard.
7. Card grid / product details / admin `*Path` column — product-default fallback on empty.
8. Full `run --all` BG regenerate + a real browser eyeball of an avatar and a product with no
   image (per the standing "render-tested-only hides bugs" rule from changelog 046).
