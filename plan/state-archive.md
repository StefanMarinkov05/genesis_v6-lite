# genesis - state archive (Sessions 1-21)

Older session history, moved out of `plan/state.md` to keep the actively-read file smaller
(session 32 cleanup, at Stefan's request — `state.md` is read in full nearly every session per
CLAUDE.md's own "read first" rule, and had grown to 31 sessions of increasingly detailed prose).
Nothing here was summarized or lost — every session below is preserved verbatim from its original
entry. `plan/state.md` itself now starts at Session 22 and carries the still-current milestone
summary; `plan/decisions.md` remains the separate bug/decision-history log, untouched by this split.

## Current milestone (as of the pre-split header, largely historical — see state.md for current status)
v5: FaceNote-capable engine. All live-run fixes folded in (slnx, emission ordering, area
injection+view move, deviation brace-walk+idempotency, migration tolerance+pending-detection,
--from override, offline input/ folder, BG validation injector). New engine features: kwargs
validations, enum/status props, soft delete w/ global filters + scaffold transform, AuditableEntity
+ SaveChanges audit, pagination+server search+sorting in template AND scaffolded controllers,
ownership checks in feature controllers, feature stacks (posts/comments/votes, cart/1-click
orders w/ status flip, follows, reviews, catalogues w/ modal picker), layout v2 (feature nav,
PFP avatar, cart badge, contact link). Previously: v2 TravelMap-calibrated. NOT yet compiled - authored in a container without a .NET SDK.
Calibration applied: ADR-002b wholesale identity pages (Login/Register/Logout markup templates),
[Display(Name)] BG + required keyword + DateOnly + computed props in model.sbn, per-model {Model}Image
tables (ImageService per-model methods, seed + card grid updated), {Project}Context at root namespace +
{Project}ContextConnection, explicit-field ctor injection + AsNoTracking/AsSplitQuery-ready queries,
routing knob meta.routing.defaultArea ("{area=Client}" single-route style), fixture pins blockScoped.

## Done (as of the pre-split header)
- Spec POCOs + schema (validated against fixture with an independent validator: 0 errors) + semantic checks
- Variation resolver (deterministic, tested)
- Pipeline runner + state store + process wrapper
- LLM bridge (manual/auto, schema-gated, hash-logged)
- All 9 stages
- Templates: models, dto, validation attrs, dbcontext, seed data, identity (user/describer/seeder/bg-map), program, controllers (template/users/image service/sorting body), views (layout/landing/contact/cardGrid/adminTable/partials)
- Feature fragments (cart real; catalogues/personalization intentional no-ops; orders/rating/posts stubs)
- Fixture spec + canned seed response
- Test suite: spec validation, variation, template emission, deviations, LLM bridge, seed schema, verify helpers
- README, architecture doc, ADR-001..006

## Next (as of the pre-split header — long since superseded by later sessions)
1. dotnet new sln / sln add / build / test - fix first-compile issues (expect Scriban + System.CommandLine beta quirks)
2. One real run against LocalDB; calibrate identity-bg-map.json + deviation regexes against actual scaffolder output
3. Upload a working reference project zip for template fidelity calibration
4. Later: doc/presentation skill integration (report contract is frozen)


## Session (localization/view-correctness cluster) - PARTIAL v5.x
DONE in source (not yet compile-verified on Windows SDK - regenerate FaceNote to confirm):
- B1: [Display(Name)] coverage - warning (not fatal) when a visible prop lacks displayName; warn/error split in SpecLoader so warnings don't abort runs.
- B2: templates/bg-term-map.json + ApplyBgResiduePass - rewrites scaffolder EN residue (Back to List, Edit, <h1>Create</h1>, submit values) to BG and buttonizes bare action links.
- B3: 2FA removed from schema identity.modules enum (FaceNote spec never included it anyway).
- A2: StripSystemFieldsFromViews - removes CreatedAt/UpdatedAt/DeletedAt/IsDeleted from Create/Edit forms + [Bind] lists (safe parse-filter-rejoin) + client Details/Delete display; admin keeps read-only.
- A3/A14/A15: rating fragment now product-scoped (Review -> Product FK); new product_details.sbn (carousel left, props right, feedback list + avg rating below); standalone Отзиви page + nav link REMOVED; InjectReviewLoadingIntoDetails transform loads reviews+authors+avg into the scaffolded Details action (injects UserManager if absent). reviews_controller now product-scoped, redirects back to product.
- C3: prompts/in + prompts/out auto-created in LlmBridge.ExchangeAsync.

RISK: InjectReviewLoadingIntoDetails is a complex regex rewrite of a scaffolded controller (ctor + Details body) - highest chance of needing live-run iteration. Quote balance verified, but not compiled.

DEFERRED TO OPUS SESSION (architecture/design - hand over with change-spec):
- A1 client product CRUD + add-to-cart button on cards
- A4 catalogue remove-item + up/down reorder (Position column)
- A5 public catalogues page (isPublic)
- A6 order details view
- A7/A11 admin delete everywhere + self-delete guard
- A8 post image upload + author delete
- A9 public user page + all the linking (catalogue item -> product, post author -> user page)
- A10 design system: palette axis + 6 named theme presets (frontend-distinctive-design)
- B4 verify-gate upgrade (EN-token deny-list over rendered HTML of discovered routes)
- C1 structured per-stage logging + runtime stats + --verbose
- C2 end-of-run banner (admin/user creds + localhost URL)
- C4 rename genesis -> dps-nn (ASCII) / ДПС-НН (display)

## Session 2 (design system + landing + logging/banner) - v5.x
DONE in source (tokenizer-verified brace balance; NOT compiled on Windows SDK - regenerate to confirm):
- A10 design system: templates/design-presets.json with 6 named presets (midnight/sunset/forest/royal/slate/rose), each = bootswatch base + accent + tinted neutrals + Google-font pairing + radius/depth. TemplateEngine.ResolveDesignPreset picks explicit meta.style.preset or deterministic-from-seed; exposes `design` + `has_design`. templates/views/design_overlay.sbn renders the accent/hover/card-lift/type CSS, injected into layout <head> (rendered to string, passed as design_overlay_html - Scriban has no include loader). schema + FaceNote spec gain meta.style.preset (FaceNote set to "midnight").
- A9 (item 22): landing CTA bug fixed - old code used parts[1] (the Area) producing dead client/client link; now derives BOTH area and controller from the .../Areas/{Area}/Views/{Controller}/... path. Hero uses .hero class for preset gradient.
- C1: per-stage timing + dps-nn.log.jsonl (start/end/error, durationMs) + end-of-run summary table with TOTAL runtime, in PipelineRunner.RunAllAsync.
- C2: PrintCompletionBanner after green run - ДПС-НН header, localhost URL (parsed from launchSettings.json), start command, and every seed user's role/email/password. Removes the findstr-password friction.
- TemplateEngine now takes genesisRoot (threaded from ctx.GenesisRoot at all 4 sites) to locate design-presets.json.

STILL DEFERRED (Opus): A1 client product CRUD+cart button, A4 catalogue reorder, A5 public catalogues, A6 order details, A7/A11 admin delete+self-guard, A8 post images+author delete, A9 user page + item/author linking, B4 verify-gate route-HTML scan, C4 genesis->dps-nn rename.

## Session 3 (Opus: B4 + A6 + C4-bounded) - v5.x
Authored WITHOUT a .NET SDK/NuGet in this container (same constraint as prior sessions) - bracket-delta
verified against the pre-edit files, Scriban for/if/end balance verified; NOT compile-verified. Regenerate
FaceNote/AutoPazar on Windows to confirm. Comprehension map added at plan/codebase-map.md.

DONE:
- B4: VerifyStage language gate widened from 3 fixed pages (/, first cardGrid, first adminTable) to the
  WHOLE discovered route surface. New internal DiscoverRoutes(spec) -> (Public cardGrids [anon], Authed
  adminTables + each enabled Client-feature route [orders->/Client/Cart(+/Orders), posts->/Client/Posts,
  follows->/Client/Follows/List, catalogues->/Client/Catalogues]). Feature/admin routes fetched best-effort
  via new TryGetAsync (non-200 warns + skips, doesn't fail verify); only "/" stays hard-required; any English
  leak on any successfully-fetched page still fails the gate. Removed now-dead FirstPublic/FirstAdminIndexPath.
  Tests: 2 new in VerifyHelpersTests (DiscoverRoutes coverage + dedup).
- A6: order details. cart_controller.sbn gains read-only OrderDetails(int id) (own-order-only; soft-delete
  filter hides deleted -> NotFound; Include Items.ThenInclude(primary)). New view templates/views/
  cart_order_details.sbn (header dl + line-item table, BG, matches cart_index display-prop logic). Emission
  wired in EmitControllersViewsStage orders block -> Areas/Client/Views/Cart/OrderDetails.cshtml. "Детайли"
  link added to cart_orders.sbn rows. No schema/model change (Order/OrderItem/Items nav already existed).
- C4 (BOUNDED, see ADR-009): user-visible surface only. ToolCommandName genesis->dps-nn; [genesis]->[dps-nn]
  log prefix (Pipeline+SpecLoader); RootCommand help + header comment -> "dps-nn (ДПС-НН)"; LLM log filename
  genesis.log.jsonl->dps-nn.log.jsonl; README/architecture titles. C# namespaces/assemblies/sln/GenesisRoot
  identifier DELIBERATELY unchanged (churn+risk, zero user benefit, uncompilable here). ADR-009 records boundary.
- A5: public catalogues. Catalogue model gains IsPublic (bool) via feature-templates/catalogues.json (plain
  model prop -> NO schema change; bool is in the type enum). catalogues_controller.sbn gains [AllowAnonymous]
  Public() + PublicDetails(int id) (only IsPublic) + owner-only TogglePublic(int id). New views
  catalogues_public.sbn + catalogues_public_details.sbn, emitted in the catalogues block. Public/Личен badge +
  toggle added to catalogues_index card header. Anonymous nav link "Публични каталози" in layout.sbn. B4
  DiscoverRoutes adds /Client/Catalogues/Public to the anonymous scan set (+test assertion).
- A4: catalogue reorder. CatalogueItem gains Position (int) via the fragment (no schema change).
  catalogues_controller.sbn gains owner-only MoveItem(itemId, direction) that normalizes positions 0..n-1
  then swaps (robust when all Position=0). catalogues_index.sbn item list is now Position-ordered with up/down
  buttons (first/last disabled); public_details is Position-ordered too. No EF query change (ordered in views).

STILL DEFERRED (need a live scaffold to calibrate regex anchors / touch core card grid + cross-linking):
A1 client product CRUD+cart button (core card_grid.sbn + scaffold transform), A8 post images + author delete,
A9 public user page + item/author linking. A7/A11 admin delete + self-guard: self-guard already implemented
in users_controller.sbn + admin_table.sbn has Delete - VERIFY on a live run, likely no new code. See
dps-nn-session3-handover.md for the change-spec + why.

## Session 4 (Opus: A1-button + A9 + findings) - v5.x
Still no compiler in-session (bracket + Scriban-balance verified only; NOT compile-verified). Findings that
shrank the pile: A8 AUTHOR-DELETE was already complete (PostsController.Delete has the owner/admin guard +
soft delete; posts_details.sbn already has the guarded Изтрий button). A1 add-to-cart button EXISTED as a
dead stub (bare <button>Добави</button> posting nowhere).

DONE:
- A1 (button half): card_grid.sbn dead "Добави" stub -> real form POSTing to Client/Cart/Add (asp-route-itemId),
  gated on features.orders (was mis-gated on page.unauthenticated_action). Login fallback preserved. CartController
  .Add(int itemId) is [HttpPost][ValidateAntiForgeryToken] - form tag helper injects the token.
- A9: new ProfilesController.sbn ([Area Client][AllowAnonymous] Public(string id) -> FindByIdAsync + feature-gated
  Posts/PublicCatalogues via ViewData) + profiles_public.sbn view (defensive: computes has-FirstName/has-avatar
  from spec.identity.application_user.extra_properties so it never references absent props). Emitted when
  posts||catalogues enabled. Author link added to posts_details.sbn (links by @Model.UserId -> Profiles/Public,
  so NO posts-controller query change). New files can't break existing generation; worst case the profile page 404s.

STILL DEFERRED (genuinely need a live scaffold, spec in handover):
- A1 client product CRUD (create/edit/delete over own products): rewrites a SCAFFOLDED Client controller ->
  needs one real aspnet-codegenerator run to calibrate regex anchors, like ApplySoftDelete/InjectReviewLoading.
- A8 post image upload: needs "images":{...} on the Post model, which triggers the per-model {Model}Image table
  + ImageService methods + a MIGRATION on a soft-delete model + seed. Too much to land unverified; spec in handover.
- A7/A11: verify-only on a live run (self-guard + delete buttons already present).

## Session 5 (Opus: FIRST real compile fixes) - v5.9
First-ever `dotnet build` (Windows, .NET 10 SDK via Ubuntu container's apt install for Claude-side work,
real SDK on Stefan's machine) surfaced exactly 2 errors, both pre-existing (not from this Opus arc's own
changes - confirmed by diffing against the pre-Opus-session baseline):
- TemplateEngine.ResolveDesignPreset was `static` but used primary-ctor param `genesisRoot` (CS9105).
  Fixed: made it an instance method, dropped redundant spec/variation params. Call site updated.
- EmitControllersViewsStage.ApplyBgResiduePass used ctx.TemplatesDir (CS1061 - never existed). Fixed:
  ctx.GenesisRoot + Path.Combine(..., "templates", ...), matching every other call site's pattern.
NEXT: rebuild, `dotnet test`, and report the actual output - this is the real feedback loop starting.

## Session 5 continued (dotnet test: 1 stale test fixed + 1 dead axis found)
Program_template_honors_di_registration_variation was asserting removed behavior - fixed to match real
output (see decisions.md for the exact reasoning). Found but NOT fixed: diRegistrationStyle axis is fully
dead - declared + pinnable + pinned in the fixture, never read by program.sbn. Every project gets the
'chained' shape regardless of pin. NEXT for whoever picks this up: wire a separateStatements branch in
program.sbn (AddDefaultIdentity(...) as one statement, then builder.Services.AddRoles<IdentityRole>();
etc. as separate statements), then a test with BOTH pins asserting distinct shapes - do this WITH the
compiler now available, not blind.

## Session 5 continued (first real FaceNote run: $primaryModel fallback bug)
First real `dotnet run -- run --all` hit spec-expand semantic validation: Review.relations[0].target
was the literal string "$primaryModel", never substituted, because rating's config in FaceNote's spec
has no explicit "model" key (legal - catalogues/orders happen to set one, rating doesn't). Root cause:
SpecExpandStage's substitution had no fallback; EmitControllersViewsStage's FeaturePrimaryModel already
did (first model with images). Fixed by extracting ONE shared SpecExpandStage.ResolvePrimaryModel used by
both. 2 regression tests added against the real FaceNote fixture. NEXT: re-run the pipeline from repo
root (`dotnet run --project src\Genesis.Cli -- run --all --spec fixtures\project-spec.facenote.json
--out out\FaceNote`) and report the next error, if any - each one found so far has been a real,
previously-invisible bug, fixed against ground truth.

## Session 6 (Opus: seed-skip + displayName sweep)
1. SeedStage now checks for an existing valid Data/seed-data.json BEFORE calling the LLM - re-runs no
   longer force a manual paste-and-resume cycle when good data already exists. Re-validated against the
   real schema each time (not just presence), so a spec change still regenerates correctly.
2. Fixed all 9 displayName warnings from the first FaceNote run, at the fragment source. Sweep of every
   feature-templates/*.json + all fixtures found 2 MORE never-triggered gaps in the same family
   (cart.json's CartItem.UserId/Quantity - a different fragment than the one FaceNote actually exercises).
   Everything is now displayName-clean. NEXT: re-run seed (`dotnet run --project src\Genesis.Cli -- seed
   --spec fixtures\project-spec.facenote.json --out out\FaceNote` or `run --all --resume` from repo
   root) - should proceed straight to migrations/database update with zero warnings and no LLM prompt.

## Session 7 (Opus: Scriban comment bugfixes + full AutoPazar removal)
1. Fixed 2 genuine Scriban bugs (design_overlay.sbn, landing.sbn): multi-line prose inside one {{~ # ~}}
   block - Scriban # only comments ONE line, continuation lines were parsed as code and crashed on
   first real render. Every other .sbn file swept and confirmed clean (the common 'one # line then real
   code' pattern used everywhere else is legitimate, not the same bug).
2. AutoPazar fully removed per request - FaceNote is now the ONLY fixture. fixtures/ now contains just
   project-spec.facenote.json + a freshly hand-built, schema-self-validated seed-response.facenote.json.
   ~16 tests updated across every test class (Car->Product, route renames, 2 tests restructured because
   FaceNote's variation pins and enabled-features differ from AutoPazar's - see decisions.md for exact
   reasoning on each). NEXT: dotnet test (expect 33/33 - was 31 + 2 new), then re-run the pipeline from
   repo root against fixtures/project-spec.facenote.json - the design_overlay.sbn crash is fixed so it
   should get past emit-controllers-views into seed this time.

## Session 8 (Opus: seed-response extended to the full 13-model schema)
Stefan's manually-pasted seed-response.json only had Brand/Category/Product/Post. Real runtime schema
(post feature-expansion + auto-fill) needs 13 models. Extended it: added refKey to Product (24 rows) and
Post (10 rows) - both become parents once features are expanded (CatalogueItem/CartItem/OrderItem/Review
target Product; PostComment/PostVote target Post). Fixed a real bug: every UserId was the literal string
"seed-user-placeholder", which seed_data.sbn's ResolveUserRef does NOT recognize (only "$seedUser:N" is
resolved to a real Identity GUID) - would have silently broken ownership everywhere. Replaced with real
$seedUser:0/1 refs. Built Catalogue/CatalogueItem/CartItem/Order/OrderItem/PostComment/PostVote/Review/
Follow from scratch, cross-reference-validated against every real refKey. Written to
prompts/in/seed-response.json (the exact path --resume reads). NEXT: dotnet run --project src\Genesis.Cli
-- run --all --spec fixtures\project-spec.facenote.json --out out\FaceNote --resume - should consume
this directly and proceed to migrations/database update.

## Session 9 (Opus: design_overlay.sbn @media crash fixed + interactive picker feature)
1. @media in design_overlay.sbn's embedded <style> block was unescaped -> Razor parsed it as C# ->
   CS0103. Fixed with @@media. Confirmed this render-to-string-then-embed pattern is unique in the
   codebase (RenderDesignOverlay/design_overlay_html), so no sibling bugs of this exact class remain.
2. Interactive spec/seed picker built: Genesis.Core.Cli.InteractiveSelection (pure logic, 7 new tests) +
   Program.cs wiring (ResolveSpecInteractively / ResolveSeedInteractively). New seeds/ folder convention
   documented in README. Caught and fixed a real bug during wiring: AddStageCommand's seed factory was
   independently re-reading --spec from the raw InvocationContext, which would have silently ignored the
   picker's resolved spec for `dps-nn seed` specifically - restructured AddStageCommand's factory
   signature to thread the resolved spec/outDir/projectName through directly instead.
   REAL BUG CAUGHT while wiring this in: AddStageCommand's "seed" factory independently re-fetched
   --spec from the raw InvocationContext, bypassing whatever the outer handler resolved entirely - so
   a naive bolt-on of the picker would have been silently ignored for `dps-nn seed`. Fixed by changing
   AddStageCommand's factory signature to receive the ALREADY-RESOLVED spec/outDir/projectName directly
   (single source of truth per invocation), updating every one of the 6 AddStageCommand call sites.
NEXT: dotnet test (expect all-green incl. 7 new InteractiveSelectionTests), then re-run
`dotnet run --project src\Genesis.Cli -- run --all --spec fixtures\project-spec.facenote.json
--out out\FaceNote --resume` - the emit-controllers-views build gate should finally pass.

## Session 10 (Opus: logs/, changelogs/, run.bat)
Three infra additions, no engine-behavior changes: (1) logs/ folder - StageContext.Info/new Debug now
also append to logs/<Project>/actions.log.jsonl; Program.cs writes logs/registry.jsonl (full history) +
logs/registry.json (latest-per-project snapshot) mapping project->spec+seed+outDir on every invocation.
(2) changelogs/ folder backfilled with one .md per past request (001-016, sourced from decisions.md, not
memory) + this session as 017, plus a README documenting the two templates (error / feature) for future
entries. (3) run.bat - double-click bootstrap: SDK check (never auto-installs), dps-nn.sln creation only
if missing, build+test+run --all --resume, no --spec so the interactive picker engages. NEXT: click
run.bat (or run it from a shell) for the full clean-machine flow in one shot.

## Session 11 (Opus: RelativeOrFull build fix + two-script split + per-run folders)
Fixed CS0117 (RelativeOrFull was internal, needed public for cross-assembly Genesis.Cli access).
run.bat split into 1-build-and-check.bat (verify only) + 2-generate.bat (generate only, one spec per
click via the existing picker, timestamped out/run_.../ per invocation, resume-to-same-folder via
logs/.pending-outdir.txt). NEXT: click 1-build-and-check.bat first, confirm green, then 2-generate.bat.

## Session 12 (Opus: real picker bug fixed + .slnx + prompts/out/schemas+seeds archival)
Fixed ResolveSpecInteractively's real bug (1 candidate silently fell back to a nonexistent default -
now auto-selects the sole candidate). Widened both scripts' sln-exists checks to accept .slnx (the
.NET 10 SDK default) alongside .sln. New prompts/out/schemas/ + prompts/out/seeds/: successful runs
MOVE their spec+lock+seed there as permanent per-project history, named <project>-schema.json /
<project>-seed.json; both pickers now also search these folders. Placed facenote-schema.json +
facenote-seed.json there right now as a one-time action. NEXT: 1-build-and-check.bat, then
2-generate.bat - expect a 2-candidate spec menu (fixtures/ copy + the new archive copy), pick either.

## Session 13 (Opus: dotnet-ef actionable error + README prerequisite)
ef migrations add/database update now catch failures and, when the output matches the textbook
'deps.json does not exist' tool-version-mismatch symptom, append the exact fix instead of a bare
stack trace. README lists dotnet-ef as a required prerequisite (matching the existing
aspnet-codegenerator entry). NEXT: `dotnet tool update -g dotnet-ef --version 10.0.*` on Stefan's
machine, then re-run 2-generate.bat --resume - should get past ef migrations this time.

## Session 14 (Opus: error logging)
ctx.Error() added (console + logs/<Project>/actions.log.jsonl, full exception detail). Wired into
PipelineRunner + RunStage's existing catches. Every command handler body now wrapped in its own
try/catch calling a shared LogGlobalError helper before re-throwing - necessary because System.
CommandLine's own exception middleware intercepts before a wrapper around InvokeAsync itself would
ever see anything (confirmed from Stefan's own stack trace showing UseExceptionHandler frames).
NEXT: re-run 2-generate.bat --resume after `dotnet tool update -g dotnet-ef --version 10.0.*` - if it
fails again for any reason, logs/<Project>/actions.log.jsonl or logs/global-errors.jsonl will have
the full detail even after the console scrollback is gone.

## Session 15 (Opus: real root cause - dotnet-ef race condition, not version)
Diagnostics Stefan ran proved it: dotnet-ef 10.0.9 correct, and the EXACT SAME command run manually
right after the pipeline's failure succeeded completely. Race condition between dotnet-ef's own
internal build-then-load sequence and the filesystem, likely made consistent (not just occasional)
by Session 12's 'separate folder per run' change - every run is now a first-ever build in a brand
new folder. Fixed: explicit dotnet build BEFORE any ef command in SeedStage (closes the race window
since it's a fully separate, fully-exited process) + RunEfWithRetryAsync as a backstop (retries once,
only for the exact 'deps.json does not exist' signature, 2s delay). NEXT: 2-generate.bat --resume -
should get past migrations now without Stefan needing to do anything manually.

## Session 16 (Opus: MSBuild node-reuse fix + DB name uniqueness + NU1901)
Session 15's pre-build fix didn't work - ruled out file-flush race. Real culprit: MSBuild build-server
node reuse causing rapid back-to-back builds to hit a stale/busy shared node (matches: tool's own
attempt fails, identical manual command moments later succeeds). Fixed with
DOTNET_CLI_DISABLE_BUILD_SERVERS=1 on the pre-build + every ef invocation, widened retry to 3 total
attempts with increasing delays. Also: DB name now gets a timestamp suffix (unconditional uniqueness,
avoids old-DB collisions like the v3-genesis one Stefan hit) and appears in the completion banner;
NU1901 fixed properly via explicit NuGet.Packaging/Protocol 6.12.5 references + a NoWarn safety net.
NEXT: 2-generate.bat --resume - if migrations STILL fail after all of this, it's a genuinely different,
rarer issue and ThrowEfError's updated hint reflects that (check dotnet-ef version, check bin/ output
directly, run manually with --verbose).

## Session 17 (Opus: stopped guessing, added --verbose instrumentation instead)
3rd fix (MSBuild node reuse) also failed - same error again. NU1901 fix confirmed working (warnings
gone from STDERR). Rather than guess a 4th blind fix, added --verbose to all automated ef invocations
so the next failure's STDOUT captures the same rich diagnostic detail the working manual run showed.
NEXT: 2-generate.bat --resume, and if it fails again, paste the FULL output (now includes --verbose
detail) - compare 'Using application base'/'Using working directory' lines against the manual run's
own verbose output from 2 turns ago to find the actual difference, rather than fixing blind again.

## Session 18 (Opus: URL print + auto-open browser; ef migrations apparently resolved)
Stefan's app is running (localhost:5095 confirmed live in his own log) - the ef migrations saga from
sessions 15-17 appears resolved on its own or via the --verbose/node-reuse changes, though we never
got a direct side-by-side comparison to confirm WHICH fix actually did it. New: program.sbn prints the
real bound URL on every startup (ApplicationStarted callback + IServerAddressesFeature); ScaffoldStage
sets launchBrowser:true on every launchSettings.json profile so dotnet run auto-opens a tab too.
Also created /mnt/skills/user/changelog-discipline this session (standing skill, not project-specific)
- document every request, check history before proposing fixes.

## Session 19 (Sonnet: v6 Package A + K - FIRST v6 work, build+test REAL)
Started the v6 task list (plan/v6-task-breakdown.md) in the recommended order (K + A foundation first).
A compiler IS present now - baseline `dotnet test` was green (41) before touching anything; ended green
(43) after. Changelog 028.
- A/task 1: displayName check in SpecLoader.SemanticCheck promoted from soft warning ([warn] prefix,
  stderr-only) to a HARD GATE (throws SpecValidationException). Added `Computed is null` to the predicate
  (computed props' [Display(Name)] is already optional in model.sbn). Runs on raw spec AND expanded lock
  (SpecStages.cs:64 + :136), so feature-injected models are gated too. Backfill was already complete
  (Session 6/7 swept every fragment) - confirmed by running `spec expand` on the fixture: lock written +
  validated with zero displayName errors across all 13 models. No spec/fragment edits were needed.
- A/task 24b: card_grid.sbn sort <option> labels now render the field's Bulgarian display_name instead of
  the raw property name. CreatedAt/UpdatedAt (on AuditableEntity, not model.properties) map to their fixed
  BG labels from auditable_entity.sbn; everything else looks up model.properties; value= stays raw so
  sorting still works. admin_table.sbn already used @Html.DisplayNameFor - only card_grid was leaking.
- A/task 3a (Identity Account/Manage localization): DEFERRED to its own session (Opus, historically the
  hardest scaffolded-Identity item) - conscious sequencing, not skipped.
- K/task 4: fixture identity.seedUsers 2 -> 5 (1 admin + 4 regular: georgi/maria/ivan@facenote.bg).
  IdentitySeeder already handles N users generically ($seedUser:2..4 auto-resolve; rotating FirstName +
  ui-avatars pfp). Regenerated prompts/in/seed-response.json + prompts/out/seeds/facenote-seed.json:
  ownership spread round-robin across all 5 users; Catalogue grown 4 -> 10 (MANDATORY: SeedStage auto-fills
  Catalogue min to max(4, seedUserCount*2) = 10 for 5 users, else MinItems seed-schema validation fails).
  fixtures/seed-response.facenote.json (raw-3-model TEST fixture) left untouched, same boundary as Session 8.
  Verified consumable via a script replicating BuildSeedSchema (row mins, required fields, no extras,
  referential integrity, $seedUser range) against the expanded lock.
NOT done this session: the full DB pipeline run (ef migrations/database update) was NOT executed here -
build+test+expand+seed-schema-validation all pass, but a real end-to-end generation against LocalDB is the
next confirmation step. NEXT: 2-generate.bat --resume against the fixture to confirm 5 users + spread seed
data land in a real DB, then move to Package H (followers/following) per the breakdown.

## Session 20 (Sonnet: v6 Package H - followers/following single page) - changelog 029
Did Package H (task 17) next per the build order - it unblocks packages D (task 7) and G (task 26),
which both consume the followers/following list. Baseline 43 tests -> 45 green after.
- follows_controller.sbn: two actions (Followers/Following, shared "List" view + "Mode" flag) collapsed
  into ONE `List()` action returning a new FollowersFollowingViewModel { Followers, Following }. View
  model defined in the same controller file (no new emission wiring). Single users query for both lists.
  Toggle redirect fallback -> nameof(List).
- follows_list.sbn: rewritten as a strongly-typed two-column page (badge counts, unfollow on Following
  side, follow-back on Followers side). FirstName display guarded by a has_first_name Scriban check
  against extra_properties (same defensive pattern as profiles_public.sbn) so it's safe for specs w/o it.
- layout.sbn: two nav links -> one ("Последователи и следвани" -> Follows/List).
- NOTABLE: verify's DiscoverRoutes already scanned /Client/Follows/List, which previously 404'd (no List
  ACTION existed, only a shared view name); this change makes that route real.
- 2 new TemplateEmissionTests render both templates through the real TemplateEngine (Scriban parse proof +
  regression guard). NOT verified here: generated controller/view COMPILING in a real project + browser
  render - needs a full 2-generate.bat run, same boundary as Session 19.
Opus-task status refresher (from the breakdown's 6 remaining Opus jobs): 3a is MOOT for FaceNote as-is -
identity only scaffolds Login/Logout/Register (custom *_page.sbn), there are NO Account/Manage/* pages to
localize; doing it means BUILDING a Manage surface first (a scope decision, not blind work). 9/7/26/10 are
the L-effort generator-core aggregation pages that depend on H (now done) + E (upload plumbing, not yet).
NEXT recommended: Package E (upload plumbing, Sonnet) - shared by tasks 3/14/15/16 and unblocks post
images (task 10). Then the big Opus pages (7, 26, 9) have their prerequisites.

## Session 21 (Sonnet: v6 Package E keystone - FileUploadService + task 15 admin pfp) - changelog 031
Built the shared upload helper (the "build once, reuse across 3/15/16" piece) + task 15, staying in
TEMPLATE-controlled territory so it's fully build/test-verifiable. Baseline 45 -> 48 green.
- NEW templates/controllers/file_upload_service.sbn: general IFormFile->relative-path helper, separate
  from per-model ImageService. Security: Guid filename (no client name -> no path traversal), extension
  allowlist + Content-Type check + 5MB cap, code-controlled subfolder. Returns null on invalid.
- NEW templates/views/users_edit.sbn: the admin user Edit form that NEVER EXISTED (latent bug -
  UsersController.Edit returned View(user) with no emitted view -> runtime 500). Multipart, avatar
  preview (handles full-URL seed value OR relative upload), file input; has_pfp/has_first_name guarded.
- users_controller.sbn: has_pfp-branched ctor injects FileUploadService; Edit POST takes IFormFile?
  profilePicture, saves + sets ProfilePicturePath ONLY on upload; the blind copy loop now SKIPS
  ProfilePicturePath (else the empty form field wiped the avatar every save).
- program.sbn: has_pfp computed; Services using when has_images||has_pfp; AddScoped<FileUploadService>
  when has_pfp. EmitControllersViewsStage: emits FileUploadService.cs (when pfp) + Users/Edit.cshtml.
- 3 new render tests. NOT verified: generated project compile + real browser upload (needs 2-generate).
DEFERRED (rest of E): task 14 product multi-image = SCAFFOLDED controller transform, needs a live
scaffold (A1/A8 class) - NOT attempted blind. task 16 feedback media = Review model prop + migration +
reviews wiring, now unblocked by this helper. task 12 real-people seed avatars = Haiku swap, not done.
Opus readiness now: 7 + 26 needed H (done) + E-upload-helper (done for pfp; feedback/post-image parts
still pending) - closer but the admin/public aggregation pages themselves are still the big Opus builds.
NEXT: 2-generate.bat to clear the A/K/H/E verification debt in one real run, OR task 16, OR an Opus page.

## Session 21 continued (task 16: feedback image upload) - changelog 032
Reused the FileUploadService keystone immediately for task 16. Baseline 48 -> 50 green.
- rating.json: Review gains ImagePath (string, NULLABLE, displayName "Снимка"). Nullable => existing
  hand-built seed Review rows (no ImagePath) stay valid (BuildSeedSchema only requires non-nullable);
  displayName keeps it clean under the task-1 gate. Verified: expand lock validates + seed validator passes.
- reviews_controller.sbn: injects FileUploadService; Create takes IFormFile? image, saves to
  uploads/reviews, stores ImagePath. product_details.sbn: review form now multipart + file input; each
  review shows its image. No controller display change (InjectReviewLoadingIntoDetails already loads whole
  Review entities into ViewData).
- FileUploadService DI/emit gate broadened to needs_upload = has_pfp || features.rating (program.sbn +
  EmitControllersViewsStage, kept explicitly in sync - drift = startup DI crash).
- 2 new render tests. Package E now: keystone + task 15 + task 16 done; task 14 (product multi-image,
  scaffolded transform) still deferred; task 12 (real seed avatars, Haiku) not done.
VERIFICATION DEBT now spans FIVE change-sets (A/K, H, E-15, E-16) with zero real DB runs. Strongly
recommend a single 2-generate.bat run before more stacking. NEXT: that run, or task 14 (needs live
scaffold), or an Opus aggregation page (7/26).

## Session 21 continued: FIRST FULLY-GREEN END-TO-END RUN - verification debt CLEARED - changelog 033
Ran the real pipeline (run --all --resume into LocalDB). This machine HAS the tools (dotnet-ef 10.0.9,
aspnet-codegenerator, MSSQLLocalDB) - so I can and did run it myself. Two real bugs caught + fixed:
1. FileUploadService CS0246 in generated Program.cs: IdentityStage builds BEFORE emit-controllers-views,
   but Program.cs (registers AddScoped<FileUploadService>) needs the class at that build gate. Mirror of
   the existing ImageService pre-emit was missing. FIX: BuildStages IdentityStage pre-emits
   FileUploadService.cs too (same has_pfp||rating gate). CLASSIC cross-stage emission-order bug that only
   a real generated-project build reveals - the generator's own tests can't.
2. verify boot timeout 60s too tight: 5-user cold seed (PBKDF2 x5 + all feature rows) > 60s. Confirmed
   NOT a crash (manual dotnet run -> "Now listening on :5050"). FIX: VerifyStage BootWaitSeconds 60->180.
RESULT: verify PASSED build/db/boot/http/manifest/LANGUAGE. Banner shows all 5 users. FIRST TIME the
pipeline has ever cleared ef-migrations AND reached+passed verify. All of A/K + H + E-15 + E-16 are now
validated in a real compiling/migrating/booting/BG-localized app. Tests 50/50.
WORKFLOW HAZARD found + flagged: successful run --all MOVES the used spec+seed into prompts/out (Session
12 behavior) - it DELETED fixtures/project-spec.facenote.json + prompts/in/seed-response.json. Restored
both from the archive (tests back to 50/50). Needs a follow-up fix (copy-not-move, or skip the active
fixture) so a green run doesn't nuke the canonical dev fixture every time.
NEXT: fix the archival-MOVE hazard; then task 14 (product multi-image, now has a live scaffold to
calibrate against in out/), or an Opus aggregation page (7/26) - the foundation (H + uploads) + a proven
green pipeline are all in place now.
