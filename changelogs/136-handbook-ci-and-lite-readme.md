**Type:** Fix (CI) + Docs + Handbook design
**Model:** Opus - handbook workflow fix reproduced in a clean container; handbook redesigned; the lite repo gets its own README.

## Summary

1. **Handbook PDF workflow failed on its first real run** (the PR #7 merge, in both repos):
   `! LaTeX Error: File 'lmodern.sty' not found.` Pandoc's LaTeX template loads `lmodern.sty`
   (inside `\ifPDFTeX`, but the package lookup still happens), and the workflow installed
   `fonts-lmodern` - that package has only the fonts, not the `.sty`. The `lmodern` package
   ships it. `handbook.yml` now installs `lmodern`. Local builds never hit this because this
   machine has a full TeX Live.
2. **Two READMEs.** The lite repo was getting a patched copy of `README.md` (a note under the
   title plus rewritten links). Now there are two hand-written files side by side:
   - `README.md`, the full edition, gains a **The music** section: what `design/music/` is,
     how to clone without it (`GIT_LFS_SKIP_SMUDGE=1`), and a pointer to the lite repo and its
     installer.
   - `README.md` also opens with the GUI logo (`gui/src/renderer/public/logo/logo.png`, full width).
   - `README.lite.md` is the lite repo's front page: "lite edition" in the title, a note that
     it is mirrored and that issues/PRs belong in the full repo, its own CI badge and Releases
     link, the `DPS-NN-Lite-Setup` installer, and a license line with no third-party
     recordings.
   - `scripts/lite-tree.py` copies `README.lite.md` to the lite repo's `README.md` (and drops
     both originals) instead of patching. It fails if `README.lite.md` is missing.
   - The `--lite-repo` option and the README regex rewrite are gone; `lite-mirror.yml` no
     longer passes it.

3. **Handbook redesign** (`scripts/build-handbook.py`):
   - **Cover:** navy NOVA gradient, the project logo (`gui/src/renderer/public/logo/logo.png`),
     title, author, version.
   - **Type:** IBM Plex Serif / Sans / Mono, all with Cyrillic; replaces DejaVu.
   - **Palette:** the GUI's default NOVA theme (navy, cyan accent; a darker cyan for text on white).
   - **Structure:** navy part pages, numbered chapters with a cyan rule, sans headings that never
     hyphenate, styled contents, running heads, and a faded logo watermark at the bottom of every
     content page.
   - **Code:** every block sits in a tinted box with a cyan edge and a language tag (SHELL, JSON,
     POWERSHELL), with syntax colours from a palette-matched pandoc theme; inline code is coloured
     mono.
   - **Other blocks:** quotes become note panels; table header cells are bold; Mermaid diagrams
     use the same palette.
   - **Markdown:** a list right after a paragraph line now renders as a list, as on GitHub
     (`lists_without_preceding_blankline`).
   - **Workflow:** `handbook.yml` installs `fonts-ibm-plex` and `texlive-pictures`.
4. **New chapter: `docs/explanation/project-overview.md`**, the first chapter of the handbook and
   linked from both READMEs. It has a technical spec sheet, the 9-stage flow, a one-line-per-ADR
   decision table, a repository map, and a briefing for agents (mental model, invariants, how
   work is recorded, how to verify, where to look).
5. **README "Personalization"** (both READMEs) explains how to add wallpapers/themes
   (`themes.ts` + `styles.css` + i18n labels), swap the logos and icons, add music (runtime
   folders, installed location) and edit the footer.

## Techniques

- **A `\color` at the start of a table cell pushes the cell down a line.** `\code` starts with
  `\leavevmode`.
- **titlesec rejects a `tikzpicture` in a heading format** ("Entered in horizontal mode"). The
  navy part page is drawn with `\AddToHookNext{shipout/background}`.
- **fontspec letter-spacing drops plain spaces in some contexts.** Spaced caps use `~`.
- **Fonts load by file with an explicit `Path=`** (found via `kpsewhich`, else `fc-list`), so the
  same header works with TeX Live's Plex locally and the `fonts-ibm-plex` package on CI.

- **Reproduce CI in a container, not from package lists.** I ran the workflow's own install
  step and the exact pandoc call in a clean `ubuntu:24.04` container. Snap Docker can't mount
  files from `/tmp`; put helper scripts under the repo's gitignored `build/`.
- README duplication is deliberate: two audiences and two front pages. `mirror-the-lite-edition.md`
  says so: a README change that applies to both must be made in both.

## Verified

- `lite-tree.py` output: the lite `README.md` is byte-identical to `README.lite.md`;
  `lite-mirror.yml` is still dropped.
- The manual (no API key) LLM flow still works: `spec build` without a key exits 3 and writes
  `prompts/out/spec-build-request.md`; a reply pasted into `prompts/in/spec-build-response.json`
  plus `--resume` writes the spec. As before, the convention questions (`timestamps.createdAt?
  [y/n]`) need an interactive terminal.
- The dev GUI (`npm run dev` path, unpackaged) still uses `dotnet run --project src/Genesis.Cli
  --` with the repo root as both install root and workspace. It lists the fixtures/seeds from
  the repo and preselects the FaceNote sample.
