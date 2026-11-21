# Handbook edition 2 — every claim checked against the code

**Type:** Docs
**Model:** Opus — every chapter was read against the source, the templates, the fixture and the CI
files. Sonnet mirrored the corrections into bg/de from a change list.

## Summary

The author asked for "every claim in the handbook" to be true for Genesis. The first round fixed
the weaknesses reported in changelog 138: chapter cross-references, the module graph, the missing
testing sections in ch14 and the ch23 ownership-marks text. The second round read chapters 00–25
and the docs behind 17–21 against the code. Every false or overstated claim was rewritten to match
what the code does. Where the code is the thing that is weak, the book now says so in Chapter 22,
and Chapter 25 proposes the code change. No generator code was changed.

## What was wrong, by kind

| Claim in the book | What the code does | Chapters fixed |
|---|---|---|
| `verify` fetches "every route", "every page renders and is gated" | Only `GET /` and the admin login are hard failures. A cardGrid/adminTable/feature route that answers non-200 is logged (`language scan skipping`) and left out of the scan. Nothing checks that anonymous users are refused admin pages. | 01, 02, 08, 09, 14, 15, 22, 23, 24; README, README.lite, project-overview, control-flow |
| A wishlist feature (ch02 story) | No such feature. The story now uses `rating`, `catalogues` (personal list) and `orders` | 02 |
| Seven features exercised together; `features.cart`; public profiles from `personalization` | `cart` cannot be used: with `orders` it fails ("duplicate models entry 'CartItem'"), alone it fails validation (no `Cart/Index` page). `personalization` merges an empty fragment and nothing reads it. Profiles come from `posts`/`catalogues`; the avatar comes from `ProfilePicturePath`. The fixture enables six features. | 03, 10, 22, 23; generated-app-anatomy |
| "The same spec always yields the same app" | `ResolveDesignPreset` uses `GetHashCode()` (process-randomised) unless `meta.style.preset` is pinned | 01, 12, 15, 24, 25 |
| Namespaces `Genesis.*` | Folders `src/Genesis.*`, assemblies/namespaces `GenesisV6.*` | 01; README, README.lite |
| Two seeded accounts, both in the banner | FaceNote seeds six; the banner shows one admin + one user; all six are in `logs/<P>/seed-credentials.txt`; a spec may set a password | 03, 04, 16, 23, 24 |
| Client-side search | Search is a server-side query-string filter over string fields | 03, 23 |
| Every admin table paginated and sortable | Only where the spec configures sorting/pagination | 03 |
| Seed scenario coverage is "required by the contract" | It is a prompt/skill rule, not validated | 03 |
| Threaded comments, mutual-follow handling | Flat comments with an optional image; follower/following lists, self-follow refused | 23 |
| Missing `displayName` is caught by the leak gate | Semantic validation rejects it before generation | 05, 22 |
| Schema error texts (`unknown key`, `Array expected`, `Additional properties are not allowed`) | JsonSchema.Net says `All values fail against the false schema` / `Value is "string" but should be "array"` | 06, 09 |
| The GUI failure screen shows `agent-report.json` | It parses the `agent: stopped - <reason>` stdout line | 07, 15, 18 |
| `LangVersion latest` in both projects; electron-vite "packaging targets"; only CLI and agent import Stages | Only the core csproj; three build targets; only the CLI (the agent gets a delegate) | 08, 12, 13 |
| `template` covers the Specifications page; error describer on `bg` alone | Specifications is `writtenFromScratch`; the describer also needs `identity.errorDescriber` | 05, 10 |
| `spec-expand` is purely deterministic | It also has an optional LLM `expand-review` pass whose accepted suggestions are only logged | 11 |
| Component map missing files | `InteractiveSelection`, `ParallelEmit`, `DotnetHost` added; `plan/state.json` lives in `out/<P>/plan/` | 13 |
| GUI tests cover "log parsing"; `handbook.yml` builds "this book" | The tests cover banner parsing, run-control state, music parser…; CI builds edition 1 from `docs/` | 14 |
| A 16-language GUI | Six translated languages + Standard Galactic + six joke modes = 13 entries | 16; gui-architecture.md (also listed TR) |
| Generated apps make no remote calls | The layout loads Bootswatch/Bootstrap from jsDelivr and the preset's Google Fonts | 16 |
| LLM exchange hashes travel with the project | They are in the workspace-root `dps-nn.log.jsonl` | 23 |
| Corrections "listed in Chapter 25"; two proposals still open | They are in changelogs 137–139; the anatomy doc and seed contract now exist | 00, 25 |

Confirmed true and left alone: nine stages / nine `IStage` classes (five files, runner drives
seven), `Quantity` on Product (a stock field), the 11 axes and `SHA256(seed:axis)`, the 5–10 model
scope rule, the leak heuristic, `SqlServer`-only provider, the 19 files / ~5,200 lines, the install
paths, NSIS per-user install, version stamping via `extraMetadata.version`, five themes / ten
wallpapers, exit codes, environment variables, and the absence of any marking code.

## Code issues found (reported, not fixed)

1. `verify` is lenient about inferred routes and has no anonymous-access check.
2. The `cart` fragment cannot pass in any combination; `personalization` is inert.
3. The design preset is chosen with `GetHashCode()`.
4. `--from spec-build` / `--from spec-expand` run nothing (already listed in ch25).

## Verification

All four editions rebuilt with `scripts/build-handbook2.py`: en 98 pages, bg 77, de 77, galactic 113;
0 Missing character warnings; no Cyrillic in the de text; no `../docs` link and no duplicate H2 in
bg/de; no table with a mismatched column count.
