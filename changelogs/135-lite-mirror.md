**Type:** Tooling / CI (public-release prep phase 5)
**Model:** Opus — mirror workflow + tree transform; rehearsed locally against a bare repo.

## Summary

The lite edition (this repo without `design/music/`, ~1 GB of third-party recordings) is now an
automated mirror: `.github/workflows/lite-mirror.yml` rebuilds it with `scripts/lite-tree.py` on
every push to `master` and every `v*` tag, and pushes it to `StefanMarinkov05/genesis_v6-lite`
with a write-only deploy key. The lite repo itself is **not created yet** — one-time setup in
`docs/how-to/mirror-the-lite-edition.md`.

## Techniques

- **Tree transform, not history rewrite:** `lite-tree.py` copies every git-tracked file except
  `design/music/**` media (keeps the `.gitkeep`s), drops the LFS rules from `.gitattributes`, adds a
  "Lite edition" note to the README and points its Releases link / installer name at the lite repo.
  The lite repo gets its own linear history - one commit per sync with the source subject, author
  and `Mirrored from …@<sha>` - instead of `git filter-repo` output that would need force-pushes.
- **Tags:** mirrored so the lite repo's own `release.yml` builds the Lite installer (deploy-key
  pushes trigger workflows; `GITHUB_TOKEN` pushes would not). A tag sync only pushes the tag, so an
  older tagged tree never moves lite `master` backwards.
- Guarded with `if: github.repository == 'StefanMarinkov05/genesis_v6'`; fails with a pointer to the
  how-to when the `LITE_DEPLOY_KEY` secret is missing; `concurrency` serializes syncs.

## Verified

- `lite-tree.py`: 400 of 452 tracked files (52 media dropped), 62 MB; as its own repo the tree
  builds (`dotnet build`, 0 warnings) and `stage-genesis.mjs` stages it as `edition: lite`.
- Workflow git logic rehearsed against a local bare repo: first sync into an empty repo commits;
  a repeat is a no-op; a new source commit mirrors as one commit; a tag on an older commit gets its
  own commit and leaves `master` untouched. Result: 0 media files, 30 MB pack.

## Not verified

The workflow on GitHub (needs the lite repo, the deploy key and a push), and the deploy-key-triggered
`release.yml` run in the lite repo.

## Affected files

`.github/workflows/lite-mirror.yml` (new), `scripts/lite-tree.py` (new),
`docs/how-to/mirror-the-lite-edition.md` (new), `scripts/build-handbook.py`, `README.md`,
`CLAUDE.md`, `plan/state.md`.
