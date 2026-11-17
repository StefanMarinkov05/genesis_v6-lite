**Type:** Docs / tooling (public-release prep phase 3)
**Model:** Opus — public-facing writing + a new build tool; claims checked against the code.

## Summary

README rewritten as the public front page (what it is, highlights, quick start, how it works,
layout, docs, license), the CLI detail it used to hold moved to a new `docs/reference/cli.md`, and
a new `scripts/build-handbook.py` + `.github/workflows/handbook.yml` typeset all of `docs/` into one
**Architecture Handbook PDF** (Markdown + Mermaid stay the single source). No UI changes.

## Techniques

- **README:** written for recruiters and users alike. Every claim was checked against the code and
  three were fixed before commit: ADR-004 variation is about *code-style* axes, not layout (the
  visual side is ADR-008); C# is `LangVersion latest`, not "13"; "byte-for-byte reproducible"
  overstated ADR-004 (migrations/IDs differ). The old "Known first-build risk spots (written
  without a compiler)" section was dropped — obsolete since the project builds and tests in CI.
- **Key-free quick start, verified:** on a fresh clone `run --all` pends at `seed` (exit 3) because
  a single `seeds/` candidate is deliberately not auto-applied. README/tutorial now say to copy
  `seeds/facenote-rich-seed.json` → `prompts/in/seed-response.json`; ran `seed` with no key:
  `Data/seed-data.json` built from the file, no API call (then the expected LocalDB stop on Linux).
- **`docs/reference/cli.md`** (new): commands, options, exit codes, env vars, spec/seed pickers,
  history archive, logs, .bat scripts — checked against `--help` and the code.
- **Handbook build** — five parts (Getting started, Architecture, Reference, How-to, ADRs), 27
  docs, ~60 pages. In the script, not the docs:
  - Mermaid → vector PDF via mermaid-cli 11.17.1; long LR chains (≥ 6 edges) drawn top-down in
    print (unreadable at portrait width), the source keeps LR for GitHub.
  - Doc-to-doc links → in-book cross-references; links to repo files → GitHub URLs.
  - Pipe-table columns re-weighted by content (the docs' equal `|---|` squeezed prose columns),
    with each column at least as wide as its longest unbreakable token.
  - Lua filter makes long inline code breakable after `/ _ . - ,`; fvextra wraps code blocks,
    including un-highlighted `verbatim`. `✅` → "yes" (DejaVu Serif lacks the glyph).
  - Overflow verified with injection-guard's safe-read: its "text outside the visible page"
    findings are exactly the margin overruns — 4 on the first build, 0 on the final one.
- **CI:** `handbook.yml` on docs changes to master (artifact), on `release: published` (attached via
  `gh release upload`), and manually. Pinned pandoc 3.13 .deb (same as local), TeX Live from apt.
- Locally Puppeteer's browser download was disabled and Playwright's bundled Chromium reused
  (`PUPPETEER_EXECUTABLE_PATH`) — no Chrome installed.
- `.gitignore`: `/build/`, `/dist/`.

## Affected files

`README.md`, `docs/reference/cli.md` (new), `docs/01-tutorial-genesis.md`, `CLAUDE.md`,
`scripts/build-handbook.py` (new), `.github/workflows/handbook.yml` (new), `.gitignore`, `plan/state.md`.

## Not verified

The workflow itself (no `actionlint` here; it first runs on push). The CI path lets Puppeteer
download its own browser, which was deliberately not done on this machine.

## Found, not fixed

- `templates/log-bg-map.json` maps `" ms"` → `" мс"` as a plain substring, so a captured
  `dotnet msbuild` line in an error log reads `dotnet мсbuild`. Log-only.
