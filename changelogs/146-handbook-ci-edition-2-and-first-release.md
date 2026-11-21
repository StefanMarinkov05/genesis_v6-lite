# Handbook CI builds edition 2; first release tag v6.6.6

**Type:** Feature / change
**Model:** Opus - small CI change, but it gates the first public release of both repos, so the release path was traced end to end first.

## Summary

Neither `genesis_v6` nor `genesis_v6-lite` had a release, because no `v*` tag had ever been pushed:
`release.yml` (Windows installer, draft release) and `lite-mirror.yml` (copies the tag so the lite
repo's own `release.yml` builds the Lite installer) both start from a tag. Before the first tag,
`handbook.yml` was switched from the retired edition 1 to the English edition 2, so the PDF that is
attached when a release is published matches the one in `dist/` (author: keep only edition 2,
changelog 145). Then `v6.6.6` (= `VERSION`) was tagged on master.

## Techniques

- `handbook.yml`: `python3 scripts/build-handbook2.py --lang en` -> `dist/dps-nn-handbook-v2.pdf`
  (artifact `dps-nn-handbook-v2`, release asset); `scripts/build-handbook2.py` added to the path
  filter. Same dependencies as before: edition 2 English uses IBM Plex, the GUI logo and the
  shared LaTeX/Mermaid machinery in `scripts/build-handbook.py`. Translations stay local builds.
- Release flow unchanged (`docs/how-to/build-the-installer.md`): tag -> draft releases in both
  repos -> author reviews and publishes -> `handbook.yml` attaches the PDF.

## Affected files

- `.github/workflows/handbook.yml`
- `CLAUDE.md` (handbook build row)
- `docs/handbook/14-quality.md`, `docs/handbook/de/14-quality.md`, `docs/handbook/trump/14-quality.md`
  (the `handbook.yml` row); bg and trump-bg untouched (author's rule)
- `dist/dps-nn-handbook-v2.pdf`, `-galactic`, `-de`, `-trump` rebuilt
