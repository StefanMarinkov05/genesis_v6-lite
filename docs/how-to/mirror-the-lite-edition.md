<!-- last-verified: 80a78e3 -->
# How to set up and maintain the lite mirror

**Diataxis type:** how-to. The lite edition is this repo without `design/music/` (~1 GB of
recordings): small to clone, no Git LFS, and free of third-party audio. It is never edited by hand:
`.github/workflows/lite-mirror.yml` rebuilds it from every push to `master`.

## What differs

Exactly what `scripts/lite-tree.py` changes, nothing else:

- `design/music/**` media files are dropped; the three folders keep their `.gitkeep` (the GUI's
  music player already handles an empty folder).
- `.gitattributes` loses its Git LFS rules.
- `README.md` is replaced by **`README.lite.md`** — the lite repo's own README, kept in this repo
  so both live side by side. Edit it by hand; it is the one deliberate duplicate in the docs (two
  audiences, two front pages), so when a README change applies to both, make it in both.
- The handbook is **English only**: `docs/handbook/{bg,de,trump,trump-bg}/` and every PDF in
  `dist/` except `dps-nn-handbook-v2.pdf` are dropped. The English
  chapters and the build scripts (which carry the LaTeX header, cover and filters) stay, so
  `build-handbook2.py --lang en` (and `xx`, which transliterates the English text) still builds there.
- `lite-mirror.yml` itself is left out (it only runs in the full repo anyway).

History is the lite repo's own: one ordinary commit per sync, carrying the source commit's subject
and author plus a `Mirrored from …@<sha>` line. `master` is never force-pushed.

## One-time setup

1. Create the lite repo **empty** (no README, license or .gitignore — the first sync creates them):
   ```bash
   gh repo create StefanMarinkov05/genesis_v6-lite --public \
     --description "ДПС-НН (dps-nn) - lite edition: the generator without the music"
   ```
2. Make a deploy key that can write to the lite repo only, and give its private half to the full repo:
   ```bash
   ssh-keygen -t ed25519 -N "" -C lite-mirror -f lite_mirror
   gh repo deploy-key add lite_mirror.pub -R StefanMarinkov05/genesis_v6-lite --allow-write --title lite-mirror
   gh secret set LITE_DEPLOY_KEY -R StefanMarinkov05/genesis_v6 < lite_mirror
   rm lite_mirror lite_mirror.pub
   ```
3. Push `master` of the full repo, or run **Actions → Lite mirror → Run workflow**.

A different lite repo name: change `LITE_REPO` in `lite-mirror.yml` and the links in `README.lite.md`.

## Releases

Pushing a `v*` tag in the full repo mirrors the tag too. The deploy-key push triggers the lite
repo's own `release.yml`, which finds no music and builds `DPS-NN-Lite-Setup-<version>.exe` into
its own draft release. If the tag is not on the latest `master` (or its sync runs before the
`master` sync), the tag gets its own commit beside `master` with the tagged tree, so `master` never
moves backwards.

## Checking the tree locally

```bash
python3 scripts/lite-tree.py /tmp/lite   # then: cd /tmp/lite && git init && dotnet build dps-nn.slnx
```
