# genesis - state

## Current status (accurate as of changelog 101, 2026-07-16)
DPS-NN v6.4.0. The 9-stage CLI pipeline is fully working end-to-end against LocalDB (real
compiling/migrating/booting/BG-localized FaceNote generations, multiple times over) - all v6
task-breakdown items are DONE (`plan/v6-task-breakdown.md`), plus a round-2 23-item live-usage
fix batch (changelogs 057-071), async parallel emission (changelog 051), and `meta.language`
EN-generation support alongside the default BG-only path (changelog 054). A real Electron GUI
(`gui/`) exists over the CLI: phase-1 MVP (changelog 073), phase 3b's full 15-language chrome
picker (changelogs 093-094), a recent-projects sheet (changelog 100), and all 5 themes incl. a
real theme picker (changelog 101). **The manual `npm run dev` gate is CLEARED (2026-07-16)** -
Stefan confirmed the GUI renders correctly across themes and is responsive; a real end-to-end
generation from the GUI also succeeded. This unblocks Phase 2 (Pause/Resume UI wiring). Test
suite: 120 (.NET) + 37 (GUI `node --test`).

**Older session history (Sessions 1-21) archived to `plan/state-archive.md`** (Session 32
cleanup, at Stefan's request - `state.md` is read in full nearly every session per CLAUDE.md's
own "read first" rule and had grown past 700 lines). Nothing was lost or summarized away -
every archived session is preserved verbatim. Check the archive before assuming something from
the v5/early-v6 era ("first real compile", the dotnet-ef race-condition saga, the interactive
picker, etc.) isn't already solved - it almost certainly is.

## Next (open items, see plan/tasks.md for the live/current picture)
`plan/tasks.md` is the authoritative open-task list (kept current, unlike this historical log). As
of Session 31: GUI phase 3a (4 more themes incl. the SGA glyph font), phase-3 wallpapers/loading
animation/recent-projects sheet, and phase-2 Pause/Resume UI are the open Sonnet-tier GUI items.
`docs/explanation/gui-architecture.md` and `design/design-specs.md` are the GUI contracts.

## Session 22 (Sonnet/Haiku batch: tasks 5/13/24a/6/8/19/2/18/25/20/21/12/14) - changelogs 034-042
Worked the remaining Sonnet + Haiku v6 tasks (cart badge, admin sort fix, desc sort, admin user id/delete
guard, order receiver+address, catalogue rename, add-to-catalogue on details, catalogue->product link,
catalogue owner link, admin img columns, sticky header, real-people seed avatars, product multi-image).
All test-verified at Scriban-render level (tests -> 63). NO full generate run was done after 033, so this
whole batch shipped render-tested-only - which hid four generate-time bugs later caught in Session 23.

## Session 23 (Opus: v6 tasks 10/26/7/9/11 - the last five, ALL verified end-to-end) - changelogs 043-048
Did the five remaining Opus tasks AND cleared the render-only debt from Session 22 with real generates.
- Task 10 (blog post photos): comments+likes already existed; added nullable Post.ImagePath via the shared
  FileUploadService (3-gate sync widened to include posts). changelog 043.
- Tasks 26 + 7 (public profile + admin user detail): built ONE shared aggregation component -
  UserProfileViewModel + UserProfileService + Views/Shared/_UserProfileContent partial (followers/following/
  listings/posts/feedbacks/catalogues, feature-gated). ProfilesController.Public + new UsersController.Details
  both build the VM; partial branches on ViewData["AdminLinks"]. changelogs 044-045.
- Task 9 (admin total supervisor): SpecExpandStage.InjectAdminSupervisorSurface injects an Admin scaffold
  controller + adminTable page for EVERY model lacking one, into the expanded spec - so scaffold pipeline +
  admin nav + admin_table + verify all pick them up free. 10 extra CRUD controllers auto-generated for
  FaceNote. New Pluralize helper. The "highest risk, do last" item - worked first generate. changelog 047.
- Task 11 (infinite scroll): card_grid.sbn pager -> IntersectionObserver sentinel that lazy-loads ?page=N
  of the same Index route and appends .card-item nodes (reuses existing pagination, no new endpoint).
  changelog 048.
FOUR pre-existing generate-time bugs from Session 22 (never generate-tested) caught + fixed via real runs
(changelog 046): card_grid RZ1031 C#-in-option-attr (task 24a) -> selected="@(bool)"; layout CS0234 wrong
context namespace (.Data, task 5) -> root ns; catalogues_public CS1061 Catalogue.User nav doesn't exist
(task 25) -> owner dictionary; seed_data KeyNotFoundException on absent nullable props (task 16/10) ->
TryGetProperty. RESULT: TWO fully-green run --all pipelines (verify PASSED build/db/boot/http/manifest/
language) - one for 10/26/7, one for 9/11. Tests 79/79. All committed + pushed.
LESSON recorded (046): render-tested-only batches hide Razor-tag-helper / cross-file-namespace / absent-key
JSON bugs invisible without a real compile+boot. Run a full generate after ANY batch of template changes.
ALL v6 tasks in plan/v6-task-breakdown.md are now DONE (3a is moot for FaceNote - no Account/Manage pages).

## Session 24 (Opus: issue #5 - async emission pipeline + Bulgarian worker names) - changelog 051
GitHub issue #5 (NOT the breakdown's Package-B "task 5" cart badge - same number, unrelated). Baseline
79 tests green -> 85 green after. Full `run --all` (resumed from emit-models) -> verify PASSED
(build/db/boot/http/manifest/language).
- DEPENDENCY-GRAPH FINDING: the 9 stages are a strict linear chain, each ending in a `dotnet build`/
  external `dotnet new|ef|aspnet-codegenerator` against ONE shared generated project. Whole-stage
  parallelism is race-UNSAFE (two stages / two builds would corrupt the project). Did NOT fake a
  parallel DAG. The only race-free parallelism is WITHIN the two pure-emission stages: independent
  Scriban render->write to DISTINCT paths, no shared mutable state, no external process.
- NEW ParallelEmit (src/Genesis.Core/Pipeline/ParallelEmit.cs): up to 5 lanes pulling from a shared
  ConcurrentQueue (dynamic load-balance), named Кольо/Наско/Иван/Мишо/Киро. Each lane sets an
  AsyncLocal CurrentWorker in its own task body; StageContext.Info/Debug/Error prepend `[name]` when
  set. Logs now read `[dps-nn] [Кольо] wrote Models/Product.cs`.
- StageContext logging made THREAD-SAFE: one process-wide LogLock guards the console write AND the
  previously-unsynchronized File.AppendAllText (concurrent appends could corrupt the action log).
- emit-models: all model/dto/context/user/aux renders fan out, then serial build. emit-controllers-
  views: the POST-scaffold template block fans out with the render running INSIDE the lane (Func<string>,
  so the CPU-heavy part parallelizes); scaffold loop + build stay serial. TemplateEngine.RenderFile
  confirmed thread-safe (all-local per-call state).
- DELIBERATE NON-GOAL: did NOT parallelize seed image downloads (external rate-limited Pexels/Pixabay -
  parallel burst risks throttling; kept parallel region to provably race-free pure-CPU emission).
- 6 new ParallelEmitTests. All committed + pushed (no Co-Authored-By, per request).

## Session 25 (Sonnet: changelog model attribution + funny English worker names) - changelog 053
Two bundled requests. 97 tests green after (was 85).
- Changelog discipline extended: every entry now records a `**Model:**` line (model + WHY, per
  coding-standards' own routing rubric) right after `**Type:**`. Backfilled 001-052 via a one-off
  script sourced from state.md session headers + the v6 breakdown's own per-task model routing +
  explicit haiku/sonnet batch labels already in git log. 3 entries (026/027/030, the Claude Code
  migration) had no model recorded anywhere - marked Opus by chronological placement, flagged
  explicitly in changelogs/README.md as best-effort reconstruction, not re-verified fact.
- ParallelEmit gained EnglishWorkerNames (Bubba/Chuck/Duke/Rusty/Slim), toggled via DPS_NN_LOG_LANGUAGE
  env var (en -> English; unset/anything else -> Bulgarian, preserving issue #5's default). DELIBERATELY
  NOT gated on spec.meta.language: that's schema-locked to "bg" only (CLAUDE.md hard requirement), and
  nothing else in the generator would actually produce English output if widened - would silently
  half-work (the same "two paths, one concept, drift" bug class from sessions 5/8/33). Confirmed this
  scope call with Stefan via AskUserQuestion before implementing, rather than guessing which he meant.
  Smoke-tested live against the real project (DPS_NN_LOG_LANGUAGE=en emit-models -> real [Bubba]/
  [Chuck] log lines) instead of just trusting unit tests.

## Session 25 continued (Sonnet: meta.language="en" support - skip BG retrofit passes) - changelog 054
Widened meta.language schema enum to ["bg","en"] (FaceNote's fixture already had "bg" explicit at
both meta + identity.errorDescriber level - confirmed, no fixture change needed). 106 tests green
after (was 97).
- BEFORE implementing: counted actual Bulgarian content in the codebase - 54 template files have
  hardcoded Cyrillic text baked directly into hand-written .sbn templates (card_grid.sbn alone: 935
  Cyrillic chars), completely separate from the ~4 retrofit passes that translate SCAFFOLDED code.
  Surfaced this gap to Stefan via AskUserQuestion before writing anything (his framing suggested a
  much smaller job than reality) - confirmed scope: gate only the retrofit passes, do NOT attempt the
  54-template rewrite (deferred, same discipline as A1/A8/A9's "don't half-ship, defer with a written
  boundary").
- Single source of truth: new MetaSection.IsBulgarian (case-insensitive Language=="bg"), read by
  EVERY gate (both C# stages and program.sbn via meta.is_bulgarian) - avoids 5 independently-drifting
  checks, same principle as ParallelEmit's ResolveWorkerNames.
- Gated: ApplyBgResiduePass (bg-term-map residue translation), both Identity-page localization layers
  (identity-bg-map + wholesale Login/Register/Logout template emission), InjectBgValidationMessages,
  BgIdentityErrorDescriber emission + its AddErrorDescriber DI wiring in program.sbn (kept in sync via
  the same flag), VerifyStage's English-leak gate (skipped entirely + explanatory report line, not
  just non-fatal), SeedStage's seed-LLM Bulgarian/Cyrillic instruction (only rule 1 branches).
- NOT touched: the 54 hardcoded-Cyrillic templates (explicit boundary); SpecBuildStage's prompt (no
  spec exists yet at that stage, nothing to read Meta.Language from); feature-template/model
  errorMessage content (spec-authored data, not generator code).
- REAL BUG CAUGHT by the mandatory full pipeline run: VerifyStage.ManifestCheck still unconditionally
  expected BgIdentityErrorDescriber.cs on disk regardless of language - a real English run --all
  failed verify's manifest check over a file correctly never emitted. Never would have been caught by
  the 9 new unit tests alone (none exercise ManifestCheck) - only the real run surfaced it. Fixed by
  adding the same IsBulgarian gate to the manifest's expected-file list.
- Verified with TWO full real run --all pipelines: FaceNote (bg, unchanged) -> verify PASSED with the
  full language gate; a cloned FaceNoteEn fixture (language:"en", same models/features) -> verify
  PASSED with the language gate correctly skipped. Spot-checked EN output directly: no
  Infrastructure/Identity/ dir at all, zero AddErrorDescriber in Program.cs, English Login page,
  untranslated "Back to List"/"Edit" residue, bare [Required] with no injected ErrorMessage - each
  contrasted against the BG output showing the opposite. Smoke-test clone + its out/ + archive entries
  deleted after verification (was a throwaway proof, not a new permanent fixture).

## Session 26 (Sonnet: Windows notification hook + BG log translation + 2nd admin user) - changelogs 055-056
Four bundled requests. 111 tests green after (was 106).
- Windows toast notification (Claude Code config, NOT dps-nn code): new ~/.claude/hooks/windows-
  toast.ps1 (NotifyIcon balloon tip, reads stdin JSON defensively) wired into ~/.claude/settings.json's
  new Notification hook (global - applies to all of Stefan's Claude Code projects, not project-scoped).
  Verified live twice (direct API call, then piped exactly as the hook invokes it) before wiring in.
- Bulgarian log translation: new templates/log-bg-map.json (same longest-fragment-first static-phrase-
  map technique as bg-term-map.json/identity-bg-map.json, aimed at dps-nn's OWN console/action-log
  output instead of the generated app's UI). StageContext.Info/Debug/Error now call TranslateLog(msg)
  before Tag(), gated on the same Spec.Meta.IsBulgarian flag from changelog 054. Cached per-GenesisRoot
  to avoid re-parsing the map on every one of the ~50+ log calls per run. Smoke-tested live against the
  real FaceNote project (genuine "[Кольо] записан файл: Models/Product.cs" output) - caught and
  immediately fixed a self-inflicted mistake where testing the EN path with a lock-less spec file
  fell back to the raw unexpanded fixture and briefly broke out/FaceNote's build; restored + confirmed
  green before moving on.
- Second admin user for FaceNote: identity.seedUsers grows 5->6 (petar@facenote.bg, admin role),
  appended at the END (index 5) specifically to avoid renumbering existing $seedUser:0..4 refs. Per
  explicit "don't run them" instruction, self-validated by replicating BuildSeedSchema's exact rules
  in a script instead of running the pipeline - confirmed the scaled Catalogue minimum (12, up from 10)
  is satisfied by 2 new Catalogue rows + 1 CatalogueItem, all refs resolve, all 4 touched files
  (fixture + its schema archive + seed-response + its seed archive) stay in sync and valid JSON.
  FLAGGED, NOT FIXED: fixtures/project-spec.facenote.lock.json + prompts/out/schemas/facenote-
  schema.lock.json are now STALE (still show 5 users) - run --all's spec-expand pre-check only
  re-runs when state.json doesn't already mark it done, so the next --resume would silently keep
  using the stale lock. Deliberately did NOT hand-derive the lock file's resolved role name + seed
  password myself (unverifiable without running it) - NEXT REAL RUN needs an explicit `dps-nn spec
  expand --spec fixtures/project-spec.facenote.json` first to pick up the new admin.
- Recorded (memory, not code): Stefan will supply English seed data himself for a fully-English
  project - no generator change needed for that, only documentation later (he'll ask separately).
- Did NOT file GitHub issues for any of this (explicitly told it's not necessary this time).

## Session 27 (Sonnet: round-2 23-item fix batch from live FaceNote usage) - changelogs 057-071, v6.4.0
Stefan hand-tested the generated FaceNote app and reported 23 real issues. Planned into 5 dependency-
ordered checkpoints (translation infra -> Product structural changes -> images/seed -> small bug
fixes -> new features), each item its own commit/VERSION bump (6.3.1 -> 6.3.15) per explicit
instruction, every item live-verified via a full `run --all` regenerate against real generated
output - not just unit tests. 111/111 tests green throughout (one known-flaky parallel-timing test
unrelated to any of this work, confirmed by isolation reruns each time it fired).
- **Translation/display-name infra** (item 1): new `meta.domainHintBg`, `ModelSpec.displayName`,
  `ControllerSpec.displayName`/`isLookup`, enum values restructured to `{value, displayName}`
  objects. Caught mid-implementation: Scriban's `||` returns a BOOLEAN not an operand (unlike JS/
  Python) - a `label = c.display_name || short` pattern silently printed the literal string "true"
  in three spots; recurred again in checkpoint 5's Характеристики view and was caught before ever
  reaching a build the second time. Role display names (item 16) bundled in.
- **Product structural changes** (items 18+19): `Product.UserId` auto-stamped on Create (both
  areas), ownership-`Forbid()`-guarded on client Edit/Delete - new generic `InjectOwnerAutoAssign`
  transform (regex surgery on scaffolded controllers, same family as existing
  `StripSystemFieldsFromViews`/`InjectReviewLoadingIntoDetails`). Client Products Create/Edit/Delete
  UN-removed (a PRIOR session had deliberately stripped them - this request reverses that). Added
  `Product.Quantity`; `Status` now derived server-side (`InjectQuantityDerivedStatus`), never hand-
  set - stays visible on Details (unlike audit fields) via a form-only vs. display-strip split.
  Caught a silent regex-match failure: `Create\([^)]*\)` couldn't backtrack past `[Bind("...")]`'s
  own closing paren, so Create's owner-stamp never fired - found by inspecting real generated code,
  not by a build error (the regex just silently didn't match).
- **Images + seed** (items 8, 10, 11, 14, 15): root cause for "images not displaying" found -
  `FileUploadService` returns paths with no leading `/`, 7 render sites used them raw (resolved
  relative to the current page URL, a 404). Fixed all 7. Admin Posts Edit gained a "remove image"
  checkbox instead of a raw URL textbox (`InjectImageRemoveOption`, form-only-strip pattern reused
  from Status). PostComment got real image upload (Review already had it - narrowed scope after
  checking, didn't redo existing work). Seed data: closed user-5 coverage gaps (added mid-session as
  2nd admin, was absent from every engagement model), filled receiver/address on all orders. 2nd-
  image-per-product explicitly deferred (needs a real SeedStage.cs change, flagged not done).
- **Bug fixes** (items 2, 3, 4, 5, 9, 12, 13, 20): sorting-direction inversion + add-to-cart hidden
  by the catalogue button (both 1-line fixes). Admin Orders Create removed (`removedActions`, an
  existing mechanism, just applied). Admin-table FK columns now link to the related record (real
  target-controller lookup, not naive "+s" pluralization guessing - would've broken on Category).
  Unfollow `DbUpdateConcurrencyException` (double-click race) now an idempotent no-op instead of a
  500. Item 4 (OrderDetails receiver/address) needed NO code change - investigation found the
  checkout form/controller/view were already fully correct, the gap was purely seed data (folded
  into item 8). Item 12 (profile changes over time) - investigated, found NO reproducible code path
  in the seed/identity stages (they're correctly idempotent) - flagged as needing a live repro from
  Stefan, not guessed at a third time. **Recovered a dropped item**: Stefan's original "Admin Posts
  Details - add postcomments" got lost during this session's own planning renumbering into
  checkpoints - caught by re-checking generated output against his original list, not re-reported.
- **New features** (items 6/7, 17, 21): consolidated "Характеристики" admin page - one nav entry,
  tabbed by model, for the 5 small lookup/join models, replacing 5 separate full-CRUD nav entries.
  Hashed public profile URLs (`ProfileHash.Of`, SHA256-truncated, resolved by materializing+
  comparing in-memory since the hash isn't SQL-translatable) + Identity Manage page scaffolded for
  the FIRST TIME this session (flagged as a real unknown during planning - resolved via `dotnet
  aspnet-codegenerator identity --listFiles`). 2FA/Personal-data deliberately never scaffolded
  (not just hidden). Own-PFP change/delete. This cluster took 6 live-regenerate iterations to land
  cleanly, each catching a genuinely NEW failure mode: (1) the BG residue map blindly corrupted C#
  identifiers containing "Email"/"Password" as substrings (`EmailModel` -> `ИмейлModel`,
  `ManageNavPages.Email` -> `ManageNavPages.Имейл`) - excluded the whole `Manage/` subtree from the
  blind map rather than attempt fragile prose-vs-identifier text heuristics; (2) TWO separate
  `_ViewImports.cshtml` layers needed the new `Infrastructure` using (per-MVC-area ones dps-nn
  already generates, AND the separate Identity Razor Pages one, scaffolder-owned, cascades from the
  top-level file down the folder tree); (3) `ProfileHash.cs` was originally emitted in a LATER
  pipeline stage than `IdentityStage`'s own build check that needed it - moved the emission earlier.
  Also caught (before ever building): `RedirectToAction` doesn't work on a Razor Page target -
  Identity's Manage/Index is a Page, not an MVC action - fixed to `RedirectToPage`.
- Session was briefly interrupted mid-checkpoint-5 by an abrupt process teardown unrelated to the
  work itself - left a torn (all-null-bytes) `out/FaceNote/FaceNote/plan/state.json` and a few
  orphaned `dotnet.exe` processes holding stale locks. No code lost (everything through item 14 was
  already committed) - recovered by killing the orphans and one more clean `out/` wipe + regenerate.
- Final: both BG and EN full regenerates green (verify PASSED on both, language gate correctly
  skipped for EN), VERSION bumped 6.3.15 -> 6.4.0.

## Session 28 (Opus: GUI MVP - Electron shell over the CLI, spec B.0 phase 1) - changelog 073
- First real GUI **app code**. Prior GUI sessions produced only the design record (arch doc +
  ADR-010, `design/design-specs.md`, `design/preview.html`); this session built the running
  `gui/` Electron app against those contracts. Scope held strictly to B.0 phase 1: **NOVA theme
  only, EN only** - spec/seed input (upload + list-select), GUI-side dedup, Start/Stop, stage
  progress, xterm debug terminal, completion/error screens. Pause/Resume + the "Claude" seed
  button are in the IPC surface but UI-disabled (phase 2), exactly as the phasing dictates.
- **Grounded every wire against real CLI code before building** (didn't trust the design doc
  blind): confirmed `PipelineRunner.AppendLog` writes `{out}/{Project}/dps-nn.log.jsonl`
  (`{ts,stage,phase,durationMs,error}`), `state.json` at `{out}/{Project}/plan/state.json`
  with `completedStages[]`, exit-3 = LLM-pending, cwd-must-be-genesis-root, and the exact
  `run --all --spec --out --no-interactive` flag surface (verified live via `run --help`).
- **Stack: electron-vite + vanilla TS** (mirrors the vanilla preview, tiny dep surface).
  Confirmed the debug terminal needs **no node-pty** - it only *displays* piped stdout/stderr,
  so xterm.js write-only mode suffices and native compilation is avoided.
- Runner design follows the inviolable rule: one `run --all` spawn, poll-tail the jsonl from
  the spawn-time byte offset (log persists across runs), progress index seeded from the
  `state.json` baseline so resumes show an accurate bar; resolve by exit code (0 → parse
  completion banner, 3 → awaiting-LLM, other → error). Key lives only in main, injected into
  child env, never in the renderer (contextIsolation+sandbox on, minimal typed preload).
- Added two channels the report screen needed - `shell:openExternal`/`shell:openPath` - and
  updated the arch doc's IPC list + `build:done` shape (`outDir`) in the same commit.
- **Verified:** `tsc` clean · `electron-vite build` all 3 targets · banner-parser `node --test`
  3/3 (Node 24 type-stripping runs the .ts directly since its only non-node import is a stripped
  `import type`) · real CLI flag surface confirmed. **NOT yet verified** (needs a display +
  LocalDB + a multi-minute pipeline run, not runnable in the build session): the on-screen
  render and a live end-to-end generation. That is Stefan's `cd gui && npm run dev` gate before
  phase 2 - stated plainly, not claimed as done.

## Session 29 (Sonnet: card-grid bulk average rating - deferred half of changelog 091) - changelog 092
Picked up the two queued items from changelog 091. Item 2 (product_details.sbn zero-star row) was
already fixed as an UNCOMMITTED working-tree change from a prior session - verified it matches the
ask exactly (star row always renders, caption switches between "(X от Y отзива)" and "(няма
отзиви)") and left it as-is, no further edit needed.
Item 1 (card grid average rating): `sorting_index_body.sbn` gains a `has_rating`-gated
`.Include(x => x.Reviews)` + a post-fetch `ViewData["AverageRatings"]` bulk dict (keyed by primary
key, null when a product has no reviews); `card_grid.sbn` renders a 5-star row per card reading
its own entry out of that dict, defaulting to 0/5 when absent. New `EmitControllersViewsStage.
ModelHasRating(spec, modelName)` threaded into both the sorting-body snippet render and the
page-template render as `has_rating` (no primary-model lookup was exposed to Scriban before this).
REAL BUG caught while regenerating: my own first two `emit-controllers-views` invocations passed
the wrong `--out` value (`out/FaceNote` instead of `out`), which - combined with
`ScaffoldControllerAsync` having NO existing-file check (aspnet-codegenerator silently no-ops a
non-interactive overwrite prompt) - left a stray duplicate `out/FaceNote/FaceNote/...` tree that
broke the next `dotnet build` with CS0101/CS0111 duplicate-definition errors across half the
project. Cleaned up (deleted the stray nested folder) and confirmed it was self-inflicted, not a
generator bug. SEPARATE REAL FINDING (not fixed, just surfaced): `ApplyDeviations`' sorting-body
injection is keyed off a `ViewData["TotalPages"]` idempotency marker, so re-running
`emit-controllers-views` against an ALREADY-migrated controller silently skips re-rendering the
Index body even when the template changed underneath it - had to manually delete
`ProductsController.cs` to force a clean scaffold+re-inject. This generator behavior (a stale
customized controller isn't refreshed by a stage re-run) predates this session and applies to
every deviation-based transform, not just this one; flagging for whoever eventually wants
re-runs to reliably pick up template edits, since it's out of scope for a one-off rating feature.
Verified for real: regenerated `emit-controllers-views` + `verify` against FaceNote (build green,
verify PASSED build/db/boot/http/manifest/language), then manually booted the app and curled
`/Client/Products` - real mixed per-card star counts observed (★★★★★/☆☆☆☆☆/★★★★☆), not just a
template-render check. 4 new render tests (has_rating true/false × card_grid/sorting_index_body).
Suite 116 → 120 green. KNOWN GAP flagged in the changelog: only the sorting-enabled Index path
got the bulk-rating wiring (the only path FaceNote exercises / this session could verify) - a
card grid with sorting disabled keeps the plain scaffolded Index and would silently show 0-star
rows even with real reviews. NEXT: if a non-sorting cardGrid+rating combination ever matters,
extend `InjectImageInclude`'s sibling transform pattern to add the same Reviews-Include +
AverageRatings dict to the plain scaffolded Index shape.

## Session 30 (Opus: GUI phase 3b - six languages + flag picker) - changelog 093
Stefan explicitly routed this to Opus (the one phase-2/3 GUI item with real design ambiguity; the
rest are Sonnet-tier and left for a Sonnet session). Filled the i18n seam the MVP left in place.
- **The Opus-worthy decision was a BOUNDARY, not the feature itself:** these six GUI languages
  (EN/BG/DE/RU/Romani/SGA) control ONLY the GUI's own chrome. They are renderer-local (main process
  stays language-agnostic per the arch doc) and have ZERO effect on the language a GENERATED app is
  produced in - generated apps stay Bulgarian-only (CLAUDE.md) unless the SPEC's meta.language says
  otherwise (changelog 054, a separate axis). Documented this boundary loudly in i18n/en.ts, the
  arch doc, and changelog 093 - it's the same "two concepts, one word, drift" trap the generator
  side got bitten by. Because language is renderer-local, it persists via the EXISTING
  settings.language field - NO new IPC channel, so the IPC contract + its same-commit doc are untouched.
- Widened `Strings` type (`Widen<typeof en>`): en.ts is `as const` (narrow literals); the widened
  alias keeps the shape but relaxes literals to string, so every translation table typed `Strings`
  makes a missing/renamed key a tsc ERROR, not a runtime blank. Compiler-enforced "table fill not
  re-wire".
- SGA is a FONT SWAP over English (arch doc), not a translation: sga.ts re-exports en verbatim;
  selecting it toggles a `.lang-sga` class swapping the font-family. The actual enchanting glyph
  font is owned by the ENCHANT theme (design-specs §176, a separate phase-3 theme task) - until it
  lands the seam falls back to normal faces (SGA renders as English). Honest boundary, seam complete.
- Flags are INLINE SVG, not PNG: the design doc said "flag PNGs from images/" but no flag binaries
  exist and the renderer CSP forbids external assets. Inline SVG (tricolors + simplified Union Jack
  + Romani chakra + SGA runic tile) is CSP-safe and self-contained. Updated the arch-doc i18n bullet
  to say SVG (same-commit rule).
- Romani flagged HONESTLY as best-effort/not-native-verified (no standard orthography, no native
  software vocabulary) - attested common words where they exist, loanwords otherwise; header + changelog
  ask for a native review. Shipping it still fully exercises the picker/switch machinery.
- `retranslate()` re-paints every already-rendered string live without losing run/selection state
  (restores wells, live progress via a snapshot, select placeholders, open report/error screen).
  Picker is data-driven off the LANGUAGES registry (add a language = table fill + one entry).
- VERIFIED: tsc clean, electron-vite build all 3 targets, node --test 14/14 (3 existing + 11 new
  i18n: shape-match per language, SGA===en, unknown-code fallback, function strings render, every
  language has a non-empty SVG flag). NOT verified (needs a display - the manual `npm run dev` gate
  that's Stefan's, same boundary as the phase-1 MVP): the on-screen render + a live switch. Logic is
  build/test-proven; pixels are the eyeball gate. No git commit/push (per standing instruction this
  session - left as reviewable working-tree changes).
- STILL OPEN in phase 3: the other 4 themes (IMPERATOR/VEIL/FORGE/ENCHANT - Sonnet, incl. the SGA
  glyph font), per-project wallpapers + loading animation (Sonnet), recent-projects sheet (Sonnet),
  and phase 2 Pause/Resume UI (Sonnet, still gated on the manual eyeball). All Sonnet-tier - a
  future Sonnet session, not Opus.

## Session 31 (Sonnet: language-picker overflow fixes + 5 more languages) - changelog 094
Stefan screenshot-reviewed Session 30's picker and reported 2 CSS bugs + asked for Turkish plus
all 4 of the "suggestions not built" list from that session's closing message.
- CSS: `.toolbar`/`.panels` sibling elements shared z-index:4 (equal-z-index siblings stack by
  DOM order, so later-in-HTML `.panels` painted over `.toolbar` regardless of the dropdown's own
  z-index:20 - a descendant can never escape its ancestor's stacking context). Bumped `.toolbar`
  to z-index:5. Both `.toolbar` and the seed panel's `.select-row` (Claude button still cut off -
  same overflow class, different container) gained `flex-wrap: wrap` + `flex-shrink:0` on buttons.
- Turkish (tr): real hand-written translation, same personality tone as existing tables.
- Pirate/l33t/flip: three MORE generated languages via the `fromEn(leaf, wrapFn)` deep-mapper
  from changelog 093 (whole-word dictionary swap, char-substitution table, Unicode flip+reverse
  respectively) - same "generated not transcribed" guarantee.
- Yoda: the one HAND-WRITTEN joke-tier table (same honesty boundary as Romani) - natural word
  reordering isn't a safe mechanical leaf transform, ~45 strings individually reworded.
- REAL BUG CAUGHT before shipping: pirate originally wrapped function outputs like every other
  generated language, but `error.title(stage)` receives a CALLER-SUPPLIED value (the real
  pipeline stage name, e.g. "seed") and PIRATE_WORDS maps seed->"buried treasure" - wrapping
  meant the dictionary swap ran on the composed string, silently turning "stage 'seed' failed"
  into "stage 'buried treasure' failed" - a WRONG error message, not a joke. Same failure class
  as changelog 070's BG residue-map bug (blind text transform colliding with a value that must
  survive verbatim). Fixed: pirate wrapFn=false (matches morseLite's existing "don't touch
  dynamic content" precedent). Caught by actually rendering real output via a Node one-liner
  instead of trusting shape-only tests - a shape check alone would have shipped this.
- VERIFIED: tsc clean, electron-vite build all 3 targets, node --test 32/32 (11 new, incl. a
  regression test pinning the pirate fix). LANGUAGES now 15 entries (7 real/script + 8 joke).
  Not verified: on-screen render (Stefan's `npm run dev` gate, same boundary as every GUI session).

## Session 32 (Sonnet: state.md compression - Sessions 1-21 archived) - no changelog (docs-only housekeeping)
Stefan noticed the changelogs/ folder and plan/state.md keep growing and asked whether that fills
context unnecessarily. Correction on scope: changelogs/ (95 files, 516K) is NOT a per-session cost -
individual entries are only ever read on-demand (per CLAUDE.md's own changelog-discipline rule,
grepped when debugging a recurring symptom), never bulk-scanned. plan/state.md WAS the real cost:
it's in CLAUDE.md's "read first, every new session" list, so its full 744 lines loaded every
session regardless of relevance. Moved Sessions 1-21 verbatim (nothing summarized/lost) to the new
plan/state-archive.md; state.md now carries a short accurate current-status header + Sessions 22-31
inline (~340 lines instead of 744). plan/decisions.md left untouched (separate log, not on the
"read first" list in the same blocking way... actually it IS also read-first per CLAUDE.md - flagged
as a candidate for the same treatment if it keeps growing, not touched this session since it's
currently 431 lines, well under state.md's pre-split size).
## Session 33 (Opus: public-release prep, phase 1 hygiene) - changelog 131
Stefan plans a public GitHub release: full repo (with LFS music) for students + a music-free lite
mirror for recruiters; Windows-only installer from Releases; MIT; UI is off-limits ("the code is the
professional part"). Phases: 1 hygiene → 2 README/docs → 3 installer (+ `dps-nn doctor`) → 4 lite
mirror → 5 visibility flip (ask first). Phase 1 done on branch `release/public-prep`: LICENSE, CI
branch fix (was `main`, never ran), VERSION single-sourced via `src/Directory.Build.props`,
context.md/tasks.md moved under plan/, stale GUI claims in CLAUDE.md fixed. Build 0/0, tests 121/121.
Found for phase 3: rich-seed images live in gitignored `.image-cache/` (9.6M, 645 files) — a fresh
clone/installed copy won't have them. New idea raised mid-phase: autonomous agent mode (paste a
description → LLM writes spec+seed → generator → verify → repair loop) and LaTeX/PUML PDF docs.
## Session 34 (Opus: public-release prep, phase 2 - agent mode) - changelog 132
Stefan asked to treat Genesis as an AI agent: paste a description → LLM writes the spec → generator
runs → failures go back to the LLM. Decided with Stefan: repair edits the SPEC only, never generated
code (ADR-013 — keeps every app template output; capability gaps go to `meta.unsupportedRequests`
as the generator backlog instead); Sonnet 5.5 spec/repair, Haiku 5.5 seed; GUI gets ONE textarea +
button in the existing Specification panel, nothing else. PDF handbook = Markdown+Mermaid via
pandoc in CI (phase 3). Revised phases: 1 hygiene ✅ → 2 agent ✅ → 3 README + handbook PDF →
4 installer + `doctor` → 5 lite mirror → 6 visibility (ask first). Built `dps-nn create`
(AgentRunner + LlmSpecAuthor), `build:create` IPC, docs (ADR-013, agent-mode.md). No API key on
this machine: e2e verified against a local stub via `ANTHROPIC_BASE_URL`. Found: the old
`fixtures/seed-response.facenote.json` no longer matches the seed schema; GUI tests need Node ≥ 23.6. Fixed a pre-existing bug: Main returned InvokeAsync's 0 over
Environment.ExitCode, so exit 2/3 never reached the shell (the GUI's exit-3 awaiting-LLM path never fired).
## Session 34b (Opus: public-release prep, phase 3 - README + handbook PDF) - changelog 133
README rewritten as the public front page; CLI detail moved to new `docs/reference/cli.md`;
`scripts/build-handbook.py` + `.github/workflows/handbook.yml` build a ~60-page Architecture
Handbook PDF from docs/ (mermaid-cli, pandoc 3.13, XeLaTeX/DejaVu) — artifact on docs changes,
attached on release. Verified the key-free quick start (copy the bundled seed to prompts/in).
Workflow not yet run (needs a push). Next: phase 4 installer + `dps-nn doctor`.

## Session 35 (Opus: public-release prep, phase 4 - installer + doctor) - changelog 134
`dps-nn doctor` (prerequisite check, GUI runs it at startup into the debug terminal); install
root vs workspace split (ADR-014: read-only `resources\genesis` with self-contained `dps-nn.exe`,
writable `Documents\DPS-NN` as CLI cwd); electron-builder NSIS config + `stage-genesis.mjs`;
`release.yml` (tag → draft release with installer). Running the staged build found a real bug:
global tools couldn't find a per-user .NET without `DOTNET_ROOT` → `DotnetHost` infers it. Flaky
ParallelEmit test reproduced (30/30 on one CPU) and fixed. Packaged Linux build verified end to
end over CDP up to the LocalDB stop. Not verified: the NSIS installer on Windows. Next: Windows
dry run of `release.yml`, then phase 5 (lite mirror - ask first).

## Session 35b (Opus: public-release prep, phase 5 - lite mirror) - changelog 135
`lite-mirror.yml` + `scripts/lite-tree.py`: the music-free lite edition is rebuilt from every push
to master / v* tag and pushed to `StefanMarinkov05/genesis_v6-lite` via a deploy key; fresh linear
history, tags mirrored so the lite repo builds the Lite installer. Rehearsed against a local bare
repo. Lite repo + deploy key not created yet (outward step, awaiting Stefan). Next: create the lite
repo, push, Windows dry run of release.yml, then phase 6 (visibility flip - ask first).

## Session 35c (Opus: post-merge fixes) - changelog 136
PR #7 merged (2026-11-17). `genesis_v6-lite` created (public), deploy key + `LITE_DEPLOY_KEY`
secret set; the first lite-mirror sync succeeded, CI green in both repos. The handbook workflow failed
(`lmodern.sty` not found: `fonts-lmodern` has only the fonts) - now installs `lmodern`. The lite
repo now gets its own hand-written `README.lite.md` instead of a patched README; full README
gained a "The music" section. Next: Windows dry run of release.yml, then phase 6 (ask first).

## Session 36 (Opus: handbook audit + edition 2 proposal) - changelog 137
Audited the docs against the code and wrote a second, separately authored handbook under
`handbook2/` (18 chapters, 5 parts, 7 diagrams) built by `scripts/build-handbook2.py` to
`dist/dps-nn-handbook-v2.pdf` (59 pages; edition 1 rebuilt at 90). Structure is a gradient:
overview -> one generation as a story -> output tour -> hands-on -> the machine -> components ->
quality -> runtime -> limits/glossary/decisions. Audit fixes: `spec-build` is not part of
`run --all` (control flow corrected; `--from` unknown stage runs nothing - documented, not
changed); ADR-004 vs ADR-008 attribution; 67 files = 63 .sbn + 4 JSON maps; file map covers
`src/` only; the seed JSON contract was undocumented (new `docs/reference/seed-format.md`); the
seed skill emitted `imageKeyword` while the stage requires `imageKeywords` (array) - fixed;
VariationResolver header said 8 axes, registry has 11 - re-synced. Not wired into CI yet; the
edition is a review artifact.

Naming note (same session): the English handbook is now titled **Genesis** (cover, running head,
PDF metadata, edition-2 chapters); `dps-nn` stays the executable/CLI spelling and ДПС-НН the
Bulgarian edition. Conflicts with ADR-009's single user-visible name — recorded in changelog 137 as
an amendment to make, not silently applied repo-wide. The cover logo still shows ДПС-НН.
Motivation added (same session): both handbooks now explain the variety mechanism by its use case -
one developer delivering several projects to the same school/class, submissions compared side by
side. Documented honestly: style/naming/presentation/content/feature-composition vary; page
structure does not (one template set).
Handbook edition 2 batch (same session): Ch1 now lists the real capability inventory; new Ch14
"how the project is built" (AI-DLC, Diátaxis, skills vs raw prompts, Sonnet/Haiku ~$0.17 measured
by Stefan, red-green-revert-green, CLI-for-agents + GUI-for-people); new Ch17 "the delivered
application" (what a buyer receives, capability inventory, ownership marks: default clean, marked
builds per chosen seed with private tooling, can fail at startup / play music / self-edit / loop —
mechanism intentionally undocumented); Ch5 gained the spec-level personalization section (merging
controllers/views/DTOs, names, messages). All Cyrillic removed from the English manuscript.
Preface is now unnumbered front matter so printed chapter numbers match prose references.
Handbook edition 2, architecture round (same session): new Ch8 "The architecture, in one picture"
(style, 13-pattern catalogue with limits, deliberately absent patterns, coding style for generator
and generated app, problems->mechanisms, tools table with rejected alternatives, standout claims).
Original edition's Parts II-III moved in as Part V "Deep dives" (CLI, GUI, image pipeline,
Specifications page, techniques) - the same docs/, rendered English via a new build-time
TEXT_TRANSFORMS hook. Ch1 gained a "Signature choices" table so ADR reasoning appears as product
characteristics, not only as links. Book renumbered to 25 chapters, 96 pages; TOC and cross-refs
checked; zero Cyrillic.

## Session 37 — four editions of the handbook

`scripts/build-handbook.py` is now a parameterised engine (fonts, logo, cover gradient + national
band, title/labels/tags/licence, text transforms) and `scripts/build-handbook2.py --lang {en,bg,de,xx}`
drives it. Shipped: en 96 p, bg 75 p, de 75 p, galactic 110 p, plus edition 1 rebuilt at 90 p.

Nine defects fixed (recorded in `changelogs/137-...md`): unsubstituted `@HANDBOOKUPPER@` on covers,
`@FONTSETUP@` substituted before `@PLEX@`, inverted flag bands, one logo for every edition, an
alpha-flattening "background removal" that produced a black box, Deseret → Minecraft SGA with one
macro per letter pinned to a font that has the glyph, English Mermaid labels in the galactic edition,
letterspacing eating the cover word gap, and five missing translated sections.

Lesson recorded: render the artwork on the target background and pre-render every diagram before
starting a four-minute PDF build.


## Session 38 — handbook translations proofread (changelog 138)

Edition 2 approved by the author. bg and de read in full. Each had duplicated sections in ch 8/11/13,
left by the 137 addendum adding full sections without removing the condensed ones. Both now
deduplicated, and de/08 reordered to the English section order. Latin prose words, calques and
grammar were fixed in bg; grammar, false friends and English leftovers were fixed in de. The English
manuscript was left untouched. Its weaknesses were reported to the author: wrong cross-reference
chapter numbers, ch12/13 layering vs the real imports, ch14 missing test-first/regression/mutants,
and the ch23 ownership-marks risk.

## Session 39 — handbook claim audit (changelog 139)

The author asked that every claim in edition 2 be true for Genesis. Chapters 00–25, plus the docs
behind 17–21, were read against the code. About 30 false or overstated claims were rewritten: the
biggest are that `verify` "fetches every route" (only `/` and the admin login are hard failures),
the ch02 wishlist feature (does not exist), the seven features (`cart` is unusable, `personalization`
inert) and "the same spec always yields the same app" (the preset uses `GetHashCode()`). Same-claim
fixes went into README, README.lite, project-overview, control-flow, generated-app-anatomy and
gui-architecture (13 GUI languages, TR was missing). Sonnet mirrored the change list into bg/de;
the Opus review fixed four bg calques. Builds: en 98 pages, bg 77, de 77, galactic 113 — 0 Missing
character warnings, no Cyrillic in de, no `../docs` links or duplicate H2 in bg/de.

Code issues found and reported, NOT fixed (await the author): lenient verify + no anonymous-access
check; broken `cart` fragment; inert `personalization`; `GetHashCode()` preset pick.

## Session 40 — bg handbook ownership marks + demo watermark (changelog 140)

Author asked to document the watermark/delivery behaviour. English stays the serious public book, so
only `handbook2/bg/23` was edited. The section now separates (a) the inert authorship MARK (passive
signature; marked project is still a clean build, passes `verify`) from (b) the demo WATERMARK.
Author confirmed (b) is DISCLOSED trialware: buyer knows it's a demo, only the seller's tool makes
the clean build. Documented the named demo behaviours as disclosed limitations when run unlicensed
(plays music e.g. Azis; source self-modifies so it won't compile; heavy computation so pages don't
load; similar), plus a Граници note that it's confined to the demo and removed by the clean build,
and that the book documents behaviour, not the removal-tool/self-modification implementation. bg
rebuilt: 78 pages, 0 Missing character warnings. en/de/xx untouched.

## Session 41 — four generator bugs from the audit fixed (changelog 141)

Author chose to repair, not remove. Order: preset hash → verify → personalization → cart.
(1) Preset now `DeterministicIndex(seed,"designPreset",n)`, golden-value test. (2) verify strict:
every declared route must answer 200, failures collected and thrown once; new anonymous-refusal pass
(401/403/302→Login|AccessDenied). Adding it exposed that scaffolded admin + client CRUD controllers
had NO `[Authorize]` — new `InjectAreaAuthorization` (admin role on admin area; `[Authorize]` +
`[AllowAnonymous]` Index/Details elsewhere). (3) cart: cart.json's CartItem = orders.json's,
controllers entry dropped, MergeFragment dedups identical defs (throws on conflicting), `has_cart`
template flag; checkout/Orders/OrderDetails stay on `orders`. (4) personalization gates the navbar
greeting/avatar + "Моят профил" link. Tests 191 green, each fix revert-checked. Docs: handbook2
en+de (bg untouched by request; trump derives from en), README(s), control-flow, techniques,
anatomy, spec-format; PDFs rebuilt.

NOT run: generated-app verify (LocalDB = Windows). Needs a Windows `run --all` on FaceNote and on a
cart-only spec — the stricter gate may surface routes that were silently skipped before.

## Session 42 — handbook Trump edition (changelog 142)

New edition `handbook2/trump/` (00–25), built with `build-handbook2.py --lang trump` into
`dist/dps-nn-handbook-trump.pdf` (98 pp, 0 Missing character).

- Tables, code and diagrams are verbatim from en, with the voice in the prose only. 17–21 are
  condensed retellings.
- Author rules:
  - no people, parties or ideologies; nameless catchphrases are OK;
  - judge MDGA.png by file name only (the cover middle was not viewed);
  - "MAKE DIPLOMA PROJECTS GREAT AGAIN" is an assumed reading of MDGA.
- Re-synced to changelog 141 mid-session: strict verify, `[Authorize]`, cart, personalization,
  preset fixed.
- STALE in the English source, not fixed here: en 01:127, 15:103, 24 (preset and verify rows),
  25:16 (and probably de 24/25). Fix them in en/de next and rebuild those PDFs.

## Session 43 — handbook Trump edition in Bulgarian (changelog 143)

New edition `docs/handbook/trump-bg/` (00–25), built with `build-handbook2.py --lang trump-bg` into
`dist/dps-nn-handbook-trump-bg.pdf` (110 pp, 0 Missing character). The cover logo is MDGA_bg.png,
judged by file name only.

- It translates `trump/` (post-141 facts), not `bg/`.
- Terminology follows the bg glossary.
- Code is verbatim from trump, and the diagrams come from bg.
- ch23 was translated from trump/23 only. Author rule: never read bg/23 for this.
- The author allows Bulgarian jokes and sayings, but no people, parties or ideologies.
- `handbook2/` was moved to `docs/handbook/` by a concurrent session mid-write. One stray file was
  moved over by hand.
- The English trump PDF was rebuilt (trump/01 riff placement).
- Follow-up: once the `feature-templates/` → `templates/features/` move lands, sweep the path
  mentions in all editions. The cover's "Version" label is English in every edition.

## Session 45 — handbook CI on edition 2, first release v6.6.6 (changelog 146)

No release existed in either repo: no `v*` tag had ever been pushed. `handbook.yml` now builds the
English edition 2 (`build-handbook2.py --lang en`) and attaches `dps-nn-handbook-v2.pdf` on a
published release; edition 1 is no longer built in CI. Tagged `v6.6.6` on master: `release.yml`
drafts the full installer here, the mirrored tag drafts the Lite installer in genesis_v6-lite.
Drafts stay unpublished until the author reviews them.

## Session 44 — directory reorganization + dist tracked (changelog 145)

Tier 1+2 approved, Tier 3 (one workspace/ dir) rejected — prompts/ out/ logs/ stay at root.
Moves: feature-templates/ → templates/features/, images/ → templates/static/, handbook2/ →
docs/handbook/, docs/01-tutorial-genesis.md → docs/tutorial/genesis.md, 5 finished plans →
plan/archive/. spec.schema.json stays at root (GenesisRoot marker). dist/ is now tracked; lite
mirror keeps only English handbook (sources + the two EN PDFs + build scripts). Also finished the
141 doc sweep (en 01/15/24/25, de 15/24/25). Tests 191 green. bg/trump-bg left with old paths
(author's rule); file-map.md needs regenerating on Windows (pwsh); PDFs not rebuilt (another
session was building into build/handbook/ concurrently).

## Session 41 — trump-bg handbook watermark (changelog 144)

Carried the watermark/demo documentation (changelog 140, serious bg) into the Bulgarian Trump edition
`docs/handbook/trump-bg/23`, in that edition's voice. Kept the three ownership facts; added four
subsections: inert mark (passive, still a clean build), disclosed demo watermark, demo behaviours
when run unlicensed (Azis music, self-modifying source so it won't compile, heavy-computation hang —
all removed by the clean build), and a Границите scope note (confined to the demo; book documents
behaviour not the removal-tool/self-mod implementation). Same facts + safety boundary as bg,
Trump register. Built --lang trump-bg: 111 pages, 0 Missing character warnings. Other editions untouched.

## Session 38 — the Bulgarian retelling learns to sound like a book

`docs/handbook/trump-bg/` (26 chapters) rewritten at the rhetorical layer, facts untouched:
`Господине…` 27 → 0, `хора` 82 → 14, `повярвайте ми` 21 → 1, `Тръмп` as an edition label 5 → 0.
The full name is now expanded (Дипломни проекти за серсеми: Нова надежда), the 50+ defended projects
since summer 2024 are stated in the preface and chapter 1, and eight chapters carry earned credit to
the programmer and to Claude. The political disclaimer is gone.

Lesson: a running formula is a tic, not a voice. One device per chapter, and the boast survives; the
same device in twenty-seven chapters and the book turns into a rally. Recorded in changelog 147.

