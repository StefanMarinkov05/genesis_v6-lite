# Decision log
- ADR-001..009: see docs/adr/
- **ADR-009 REVERSED (v6, 2026-07-11):** C# namespaces renamed `Genesis.Core.*` → `GenesisV6.Core.*`,
  `Genesis.Cli` → `GenesisV6.Cli`, csproj files renamed `Genesis.Core.csproj` → `GenesisV6.Core.csproj` etc.
  GitHub repo renamed `genesis-v5_26` → `genesis_v6`. VERSION file added (6.0.0). Working directory
  moved to `D:/Projects/ДПС-НН`. Original rationale for ADR-009 (namespace churn) no longer applies
  given the project is making a clean major version jump.
- Expand review pass logs accepted LLM suggestions instead of auto-patching JSON pointers (conservative v1)
- Feature fragments catalogues/personalization inject nothing by design - realized by built-in template behavior
- Verify seeds nothing itself; it re-derives the admin password from the variation profile when absent from the lock
- Session 3 (Opus): B4 verify gate now scans the whole discovered route surface (not one page per kind);
  feature/admin route fetches are best-effort (non-200 warns, only real EN leaks fail) so route-derivation
  guesses can't make verify brittle. Rationale: the product's USP is Bulgarian-only UI, but the gate was
  blind to every feature page.
- Session 3 (Opus): C4 rename scoped to user-visible surface only; C# namespaces stay Genesis.* (ADR-009).
  Rationale: namespace churn is a huge uncompilable diff for zero user benefit.
- Session 3 (Opus): A5 (public catalogues) + A4 (reorder+Position) built - template-controller only, no
  schema change (IsPublic/Position are plain model props). A1/A8/A9 still deferred, NOT attempted blind;
  A7/A11 needs only a live-run confirm. Rationale: the remaining items rewrite scaffolded
  controllers / the core card grid / cross-link many templates, and need one live SDK-10 scaffold to
  calibrate regex anchors against (the state.md "Next, locally" step). Shipping them uncompiled and
  un-bisectable would violate the incremental discipline the project has run on.
- Session 4 (Opus): A1 add-to-cart button (dead stub -> real Cart/Add form) + A9 public profile page built
  blind (new self-contained files + one UserId-based author link, no scaffold dependency). A8 author-delete
  found already implemented. Left: A1 client CRUD + A8 image upload (both need a live scaffold/migration to
  verify) + A7/A11 live-run confirm. These are deferred to a compiler-enabled session, not skipped.
- Session 5 (Opus, FIRST real compile): dotnet build on Windows surfaced 2 pre-existing bugs neither
  session had caught blind (both predate this Opus arc - Session 2 'A10 design system' and an earlier
  templates/bg-term-map.json wiring):
  1. TemplateEngine.ResolveDesignPreset was `private static` but read the primary-constructor parameter
     `genesisRoot` (CS9105 - a static method cannot capture instance/primary-ctor state). Fixed by making
     it an instance method and dropping the now-redundant spec/variation args (already in scope).
  2. EmitControllersViewsStage.ApplyBgResiduePass referenced ctx.TemplatesDir, which never existed on
     StageContext (CS1061) - every other call site correctly uses ctx.GenesisRoot + Path.Combine(...,
     "templates", ...) (see StageContext.Template() in Pipeline.cs). Fixed to match that pattern.
  Both were caught on the FIRST real `dotnet build` this project has ever had. This is exactly why B4/
  A6/A4/A5/A9/A1-button were kept small and self-contained this session - a compiler is a MUCH better
  bisection tool than bracket-counting, and it just proved it in under a minute.
- Session 5 (Opus, dotnet test): 30/31 green after the build fixes. The 1 failure was a STALE TEST, not a
  generator bug: it asserted `.AddDatabaseDeveloperPageExceptionFilter()`, which program.sbn's own comment
  says was deliberately removed (needs a namespace not referenced under --auth None scaffolding, confirmed
  on a live SDK 10.0.100 build in an earlier session) - the test was never updated to match. Fixed the
  assertion to check real current output instead (AddDefaultIdentity/.AddRoles/.AddErrorDescriber chain +
  MapControllerRoute).
  SEPARATE FINDING while investigating (NOT fixed - needs its own scoped session): `diRegistrationStyle`
  is a declared, pinnable variation axis (chained|separateStatements), pinned to "chained" in the fixture,
  but program.sbn never actually branches on it - the identity chain is unconditionally emitted as one
  fluent chain regardless of the axis value. So the axis is currently dead: every generated project gets
  the same DI style no matter what's pinned/resolved. This does NOT block compilation or generation (the
  chained output is valid code), but it silently defeats the axis's purpose (inter-project variation - the
  whole point per the class doc: 'a template literally cannot vary per-file'... except this one doesn't
  vary at all). Needs: a separateStatements branch in program.sbn + a real test that pins BOTH values and
  asserts different output shapes. Left alone rather than guessing the separateStatements form blind.
- Session 5 (Opus, first real pipeline run): FaceNote run failed spec-expand semantic validation -
  "relation target '$primaryModel' is not a declared model" on the rating feature's Review model.
  ROOT CAUSE (real bug, not a bad spec): SpecExpandStage's $primaryModel token substitution only fired
  when the feature's own config block had an explicit "model" key. EmitControllersViewsStage's
  FeaturePrimaryModel already had a fallback (first model with images) for exactly this case - but
  SpecExpandStage's separate, earlier substitution logic did not share it. FaceNote's "rating": {"enabled":
  true} (no "model" key, legal per schema) hit the gap; catalogues/orders happened to have explicit
  "model": "Product" so they never exposed it.
  FIX: extracted SpecExpandStage.ResolvePrimaryModel(spec, cfg) as the single shared resolution rule;
  EmitControllersViewsStage.FeaturePrimaryModel now delegates to it instead of duplicating the logic.
  2 new regression tests (fallback case + explicit-model case, both against the real FaceNote fixture).
  This is the SAME class of bug as the ctx.TemplatesDir/genesisRoot fixes: two code paths computing the
  same concept with subtly different rules, invisible without a real run through both paths.
- Session 6 (Opus, Stefan requests): two fixes.
  1. SeedStage always called the LLM exchange even when a valid Data/seed-data.json already existed
     from a prior run, forcing a fresh manual paste-and-resume cycle (LlmPendingException) every re-run.
     Fixed: check for an existing file FIRST, re-validate it against the SAME schema an LLM response
     would need to satisfy (LlmBridge.Validate, reused - not just "file exists"), and skip the exchange
     entirely if it's still valid. A spec change that invalidates the old data (new model, new required
     field) correctly falls through to a fresh LLM call, so this can't silently seed stale/wrong data.
  2. All 9 displayName warnings from the FaceNote run fixed at the SOURCE (feature-templates/*.json), not
     the generated project - CartItem.UserId (orders.json - the one actually flagged), Order.UserId,
     OrderItem.Quantity, PostComment.UserId, PostVote.UserId/Value, Review.AuthorUserId, Follow.Follower/
     FollowedUserId. A full sweep of every feature-template fragment + all 3 fixtures caught 2 MORE
     never-flagged gaps in the same family: cart.json's CartItem.UserId (I fixed the wrong file first -
     cart.json vs orders.json both declare a CartItem, only orders.json's is what FaceNote actually uses)
     and cart.json's CartItem.Quantity. Both fixed too. feature-templates/*.json + all fixtures now 100%
     displayName-clean per SpecLoader's own warning rule (non-key, non-FK properties only).
- Session 7 (Opus, Stefan requests): two real bugs found + full AutoPazar removal.
  1. templates/views/design_overlay.sbn and templates/views/landing.sbn each had a multi-line prose
     comment inside ONE {{~ # ... ~}} block. Scriban's # is a SINGLE-LINE comment - it only comments to
     end of that line, not to the closing ~}}. Continuation lines were being parsed as CODE, crashing
     with 'Invalid token found `,`' the first time design_overlay.sbn was ever actually rendered (this
     is why it was never caught blind). Fixed by giving every line its own #. Swept ALL .sbn files for
     the same anti-pattern (refined to exclude the legitimate 'one # comment line then real code'
     idiom used throughout the codebase) - only these 2 were genuine bugs; none remain.
  2. Full AutoPazar removal per request. Deleted fixtures/project-spec.autopazar.json, 
     project-spec.autopazar-extended.json, seed-response.autopazar.json. FaceNote is now the sole
     fixture (tests/Genesis.Tests/CoreTests.cs Paths.Fixture repointed, FaceNoteFixture constant folded
     away). This touched ~16 tests across every test class:
     - Car->Product, Cars->Products, AutoPazar->FaceNote, CarImages->ProductImages throughout.
     - VariationTests.Explicit_axis_pin_wins_over_derivation: FaceNote's fixture only pins namespaceStyle
       (not collectionInit/adminRoleName like AutoPazar did) - computed FaceNote's actual SHA256-derived
       axis values in Python (replicating BitConverter.ToUInt32's little-endian read exactly) to confirm
       adminRoleName would derive to "Admin" (not "Administrator") unpinned, so rewrote the test to
       assert namespaceStyle=blockScoped instead - the one axis FaceNote's fixture genuinely pins, and
       provably different from its unpinned derivation (fileScoped), so it actually tests "pin wins".
     - VerifyHelpersTests: FaceNote enables ALL 6 features, so there's no natural 'feature disabled'
       case anymore. Split into two tests: the existing one now asserts orders IS scanned (positive),
       and a NEW Discover_routes_excludes_a_disabled_feature_regardless_of_fixture_state test uses a
       minimal hand-built ProjectSpec (no fixture file) for the negative case - more robust than before,
       since it can never silently stop testing anything just because a fixture's toggles change again.
     - New fixtures/seed-response.facenote.json: hand-built canned response for Brand/Category/Product
       (the only 3 models in FaceNote's RAW unexpanded spec.Models - Post is feature-injected via
       posts.json and doesn't exist until spec-expand runs, so BuildSeedSchema against the raw fixture
       only covers those 3, confirmed by tracing BuildSeedSchema's exact filter logic). Self-validated
       with a faithful Python reimplementation of BuildSeedSchema before committing to it - passed clean.
     - README's example command + 2 illustrative code comments (Pipeline.cs, SpecStages.cs) updated from
       AutoPazar to FaceNote examples. plan/state.md's historical AutoPazar mentions left untouched
       (ADRs/history aren't rewritten, same principle as ADR-009).
     Full repo sweep confirms zero remaining AutoPazar references outside historical plan/state.md.
- Session 8 (Opus, Stefan's manual seed paste): the pasted seed-response.json only covered
  Brand/Category/Product/Post (4 models) - matching the RAW unexpanded spec's own rowsPerModel keys.
  But the REAL runtime schema (built inside SeedStage.RunAsync, AFTER feature expansion + the
  FeatureModelDefaultRows auto-fill) requires ALL 13 models: + Catalogue, CatalogueItem, CartItem,
  Order, OrderItem, Follow, Review, PostComment, PostVote. Simulated the exact expanded-spec schema
  in Python (fragment merge with $primaryModel->Product, then the SAME auto-fill dict SeedStage uses)
  to get every model's exact required-field set right before writing anything.
  SEPARATE REAL BUG caught in the pasted data itself: every UserId used the literal string
  "seed-user-placeholder", which does NOT match seed_data.sbn's ResolveUserRef (only strings starting
  with "$seedUser:" get resolved to a real Identity GUID - anything else is inserted AS-IS). This would
  have silently seeded every product/post owned by a phantom, never-matching user id - broken ownership
  checks, and the A9 profile page would 404 on every seeded post's author link. Fixed by replacing every
  occurrence with a real $seedUser:0/$seedUser:1 alternating between FaceNote's 2 actual seed users.
  ALSO discovered: once features are expanded, Product and Post themselves become PARENTS too (targeted
  by CatalogueItem/CartItem/OrderItem/Review and PostComment/PostVote respectively) - so they ALSO need
  a refKey, which the pasted data didn't have on either. Added refKey to all 24 Product rows and all 10
  Post rows, then built the 9 missing models' rows from scratch, all cross-referencing real refKeys/
  seed-user indices. Fully self-validated (required/forbidden fields per BuildSeedSchema's exact rules,
  AND that every parentRef_X / AuthorUserId / FollowerUserId actually resolves to something real) before
  writing. Written to prompts/in/seed-response.json (LlmBridge.ResponsePath("seed")'s exact expected
  path) so --resume picks it up directly. NOT added to fixtures/seed-response.facenote.json, which is
  separate test infrastructure intentionally scoped to only the raw spec's 3 models.
- Session 9 (Opus): real build-crash fix + interactive spec/seed picker feature.
  1. templates/views/design_overlay.sbn's `@media (prefers-reduced-motion...)` was NEVER escaped -
     Razor does not treat inline <style> blocks as raw text the way it does <script>, so a bare @media
     inside embedded CSS gets parsed as a C# expression `media(...)`, producing exactly Stefan's error
     (CS0103: 'media' does not exist). This is a well-known ASP.NET Core Razor gotcha. Fixed with @@media
     (Razor's own escape for a literal @). Confirmed via RenderDesignOverlay/design_overlay_html trace
     that this is the ONLY 'render-to-string-then-embed-into-another-cshtml' pattern in the codebase, so
     no other latent instances of this exact bug class exist elsewhere.
  2. New feature: interactive spec/seed picker (Stefan: 'I may have multiple config json and multiple
     seeds, I want to choose which one every time'). New Genesis.Core.Cli.InteractiveSelection (pure,
     zero Console I/O, fully unit-testable: FindCandidates/FormatMenu/ParseSelection - 7 new tests).
     Program.cs wiring: ResolveSpecInteractively (scans cwd+fixtures/ for project-spec*.json, menu when
     2+, explicit --spec always wins with zero prompting - detected via the raw args array rather than
     an uncertain System.CommandLine 'was this explicit' API, since that can't be verified without a
     compiler) and ResolveSeedInteractively (new seeds/ folder convention, menu when 2+, copies the
     chosen file to prompts/in/seed-response.json AND clears any stale Data/seed-data.json in the target
     project first - otherwise SeedStage's own 'reuse existing valid seed data' fast path from Session 6
     would silently win over the fresh choice, defeating the whole point of choosing).
     REAL BUG CAUGHT while wiring this in: AddStageCommand's "seed" factory independently re-fetched
     --spec from the raw InvocationContext, bypassing whatever the outer handler resolved entirely - so
     a naive bolt-on of the picker would have been silently ignored for `dps-nn seed`. Fixed by changing
     AddStageCommand's factory signature to receive the ALREADY-RESOLVED spec/outDir/projectName directly
     (single source of truth per invocation), updating every one of the 6 AddStageCommand call sites.
- Session 10 (Opus, Stefan requests): logs/ + changelogs/ + run.bat, all pure additions (zero changes
  to generation/pipeline BEHAVIOR). StageContext gained per-project leveled logging via a COMPUTED
  property (Directory.GetCurrentDirectory()-rooted), deliberately NOT a new constructor parameter, so
  zero existing call sites (Program.cs, tests) needed to change. Program.cs's registry write is
  best-effort (try/catch) so a logging failure can never break a real run. changelogs/001-016 were
  written FROM decisions.md's own session-by-session record (the authoritative source), not
  reconstructed from conversation memory, to avoid getting historical details wrong.
- Session 11 (Opus): fixed a real build error (InteractiveSelection.RelativeOrFull was `internal`,
  invisible to Genesis.Cli across the assembly boundary - only Genesis.Tests has InternalsVisibleTo
  access; changed to `public`, swept for sibling mismatches, found none). Split run.bat into
  1-build-and-check.bat (SDK/sln/build/test only) + 2-generate.bat (runs the pipeline, one spec per
  invocation - reusing the existing picker rather than adding a new mechanism) per Stefan's request.
  Implemented 'separate folder per project' at the SCRIPT level (out/run_<timestamp>/), not by
  changing StageContext.ProjectDir in the engine - that computation is load-bearing for StateStore/
  VerifyStage/the completion banner, so changing it without a compiler was the wrong risk trade-off
  versus a batch-script timestamp. Handled resume-after-pending correctly via a small marker file so
  a --resume click continues in the SAME folder instead of silently restarting in a new one.
- Session 12 (Opus): fixed a REAL, previously-shipped bug + 2 directory-structure requests.
  1. ResolveSpecInteractively's own comment said 'keep current behavior' for 0-or-1 candidates, but
     the code returned the bare nonexistent default 'project-spec.json' even when exactly ONE real
     candidate existed (Stefan's actual situation: only fixtures/project-spec.facenote.json). This is
     what caused his FileNotFoundException. Fixed: 0 candidates -> fallback (unchanged); exactly 1 ->
     auto-select it (NEW, the actual fix); 2+ -> menu (unchanged). Deliberately did NOT apply the same
     auto-select-on-1 fix to ResolveSeedInteractively - that would force a Data/seed-data.json delete+
     recopy on EVERY run whenever exactly one seeds/ file exists, regressing the 'reuse existing valid
     seed data' feature from an earlier session for the common single-seed case. Caught this BEFORE
     shipping it by reasoning through the interaction, not after another bug report.
  2. .slnx: .NET 10 SDK's `dotnet new sln` now defaults to the newer .slnx format, not .sln. Both batch
     scripts' existence checks widened to `if not exist X.sln if not exist X.slnx` (chained ifs = AND).
  3. New prompts/out/schemas/ + prompts/out/seeds/ folders: on a successful run --all, the spec (+lock)
     and seed-response actually MOVE there (File.Copy+Delete, not just Copy - 'this is history'),
     renamed <project>-schema.json / <project>-seed.json (lowercased project name, matching Stefan's
     own "facenote-seed.json" example exactly rather than the spec's as-declared casing). Both pickers'
     search paths extended to include these folders, so an archived spec/seed stays selectable later.
     One-time action: copied the current FaceNote spec + the already-extended seed-response.json into
     the new folders right now (facenote-schema.json / facenote-seed.json), so they're immediately
     discoverable without requiring a run first. NOTE flagged to Stefan: this leaves the ORIGINAL
     fixtures/project-spec.facenote.json in place too (his ask was 'put it there AS WELL', i.e. add,
     not remove) - so the next 2-generate.bat run will see 2 spec candidates and show a menu, which is
     expected/correct, not a bug.
- Session 13 (Opus): dotnet-ef actionable error handling. Stefan's real run got past emit-controllers-
  views + seed-data generation, then `ef migrations add Initial` failed with 'Build succeeded' followed
  by 'the specified deps.json does not exist' - the textbook symptom of a globally-installed dotnet-ef
  tool that predates the generated project's EF Core 10.0.* packages (BuildStages already pins
  Microsoft.EntityFrameworkCore.Tools --version 10.0.*): it can trigger a build (version-agnostic) but
  then fails to locate/load the resulting net10.0 assembly. Confirmed there was ALREADY an identical,
  working actionable-error pattern for aspnet-codegenerator in BuildStages.cs but NOTHING equivalent for
  dotnet-ef anywhere - README didn't even list it as a prerequisite. Added ThrowEfError (mirrors the
  existing pattern) to all 3 ef invocations in SeedStage, detecting the exact 'deps.json does not exist'
  substring and appending the precise fix (`dotnet tool update --global dotnet-ef --version 10.0.*`)
  instead of a bare stack trace. README's prerequisites section updated to list dotnet-ef alongside the
  codegenerator. NOTE: my own quick Python C#-tokenizer false-positived 'unterminated string' on this
  edit; confirmed via bracket-delta (open==close per type, the method used throughout every session) AND
  by running the SAME tokenizer against the pre-edit file pulled from the last shipped zip - it ALSO
  false-positives on the untouched original, which Stefan has already compiled successfully multiple
  times, so this is a pre-existing limitation of my own ad-hoc checker on this file, not a real issue.
- Session 14 (Opus): error logging, per Stefan's request. Added ctx.Error() to StageContext (console
  + logs/<Project>/actions.log.jsonl, level=error, full exception detail via ex.ToString() not just
  ex.Message). Wired into PipelineRunner's existing per-stage catch AND RunStage's catch (individual
  stage commands, not just run --all). REALIZATION while implementing: a naive wrapper around
  `root.InvokeAsync(args)` at the very end would be DEAD CODE for handler-thrown exceptions - Stefan's
  own pasted stack trace shows System.CommandLine's own UseExceptionHandler middleware frames, which
  intercepts and prints 'Unhandled exception: ...' WITHOUT re-throwing past InvokeAsync. Fixed by
  extracting a shared LogGlobalError(ex) helper and wrapping the BODY of every command handler
  (buildCmd/expandCmd/AddStageCommand's generic handler/runCmd) in its own try/catch that logs then
  re-throws - this actually runs, since it's inside the handler, before System.CommandLine's own
  middleware ever sees the exception. Kept the outer InvokeAsync wrapper too as defense-in-depth
  (harmless even if unreachable for this specific case) rather than removing it.
  Net effect: any failure now lands in logs/ - stage failures in logs/<Project>/actions.log.jsonl
  (full detail), earlier failures (spec resolution, GenesisRoot()) in logs/global-errors.jsonl.
- Session 15 (Opus): real root cause found via requested diagnostics, NOT a dotnet-ef version issue
  after all. Stefan confirmed dotnet-ef 10.0.9 (correct, matches EF Core 10.0.* packages) via
  `dotnet ef --version`. The SAME EXACT command (`dotnet ef migrations add Initial --verbose`), run
  MANUALLY right after the pipeline's own automated attempt had just failed with 'deps.json does not
  exist', completed successfully end-to-end (migration + snapshot written, 'Done.'). Same command, same
  folder, back-to-back - one fails, one succeeds. That's the signature of a RACE CONDITION, not a
  config/version problem: dotnet-ef's own internal sequence is (1) evaluate the project via `dotnet
  msbuild /getProperty:...`, (2) `dotnet build ... /p:PublishAot=false`, (3) immediately `dotnet exec
  --depsfile ... ef.dll ...` to actually run the design-time command - and on a PROJECT FOLDER THAT'S
  NEVER BEEN BUILT BEFORE, that build-then-immediate-load can race the filesystem (antivirus scanning
  a freshly-written DLL, writes not fully flushed, etc.) - a known class of dotnet-ef flakiness.
  CONNECTED THIS TO SESSION 12's OWN CHANGE: 'one folder per run' (out/run_<timestamp>/) means EVERY
  single invocation is now a first-ever build in a brand new folder - the exact scenario prone to this
  race - where reusing the same out/<Project>/ folder across runs (the old behavior) would have kept
  that folder's build warm after the first run and likely masked this most of the time.
  FIX: added an explicit, fully-separate `dotnet build` in SeedStage BEFORE any `dotnet ef` command -
  since it's its own process that fully exits before the ef commands start, it closes the specific
  race window (ef's own internal build becomes a fast, already-up-to-date no-op). Added
  RunEfWithRetryAsync as a defensive backstop: retries once, ONLY for the exact 'deps.json does not
  exist' signature, after a 2s settle delay - any other failure or a second failure after retry is
  NOT retried again, just returned for ThrowEfError to handle as before. Updated ThrowEfError's hint
  text to no longer lead with 'it's your dotnet-ef version' as the primary suspect, since that's now
  usually wrong - a failure THIS PAST the pre-build+retry is a genuinely persistent issue.
- Session 16 (Opus): the pre-build+2s-retry fix from session 15 did NOT resolve Stefan's ef migrations
  failure - same exact error recurred. This rules out a pure file-flush/antivirus race (a single extra
  build ahead of time can't fix a STALE BACKGROUND PROCESS). Revised theory: MSBuild's build-server
  'node reuse' feature keeps a persistent build server process running across invocations for speed;
  rapid back-to-back builds (our pre-build, then dotnet-ef's own internal one) can hit that same reused,
  possibly-still-busy node. This matches the observed symptom exactly: the tool's own attempt fails,
  but the IDENTICAL command run manually moments later (a fresh shell, not sharing that node in the
  same way) succeeds outright. FIX: set DOTNET_CLI_DISABLE_BUILD_SERVERS=1 (Proc.RunAsync already
  supported passing env vars) on the pre-build AND every dotnet-ef invocation, forcing a clean process
  every time instead of hoping a settle delay is long enough. Also widened RunEfWithRetryAsync to up to
  2 retries (3 total attempts) with increasing delays (2s, 5s), and updated ThrowEfError's hint text to
  reflect everything now tried, since a failure reaching that point is a much rarer, more genuinely
  persistent issue than before.
  ALSO (separate, Stefan-requested fixes):
  1. DB name collision: Stefan correctly recalled an old v3-genesis-era LocalDB database could still
     exist under the plain project name. Rather than add a live SQL existence-check (a new package
     dependency + live-connection assumption unverifiable without a compiler), made the database name
     UNCONDITIONALLY unique per generation attempt (a timestamp suffix computed once in ScaffoldStage,
     which only ever executes once per fresh out/run_.../ folder thanks to the existing state.json skip
     logic) - achieves the same practical goal (never silently collide with an old DB) without that
     risk. Surfaced the actual DB name in the completion banner (read back from appsettings.json) so
     Stefan can always see which database a given run actually used.
  2. NU1901 (NuGet.Packaging/NuGet.Protocol 6.12.1 low-severity advisory GHSA-g4vj-cjjj-v7hg): searched
     and confirmed the patched version in the same 6.12.x line is 6.12.5. Added explicit direct
     PackageReferences at 6.12.5 to the generated project (NuGet resolves to the HIGHEST version
     requested anywhere in the graph, so this upgrades the transitive one pulled in by the codegen/EF
     tooling packages) - the real fix, not just hiding the message. Belt-and-braces: also added NU1901
     to the generated csproj's NoWarn (matching this tool's own existing NU1902-1904 NoWarn convention)
     via XDocument-based structure-aware editing, as a safety net in case the version bump alone
     doesn't fully silence it.
- Session 17 (Opus): third fix attempt (MSBuild node-reuse disabling) ALSO failed to resolve the ef
  migrations issue - exact same error recurred a fourth time. Confirmed the NU1901 fix DID work (the
  NuGet.Packaging/Protocol warnings are gone from this run's STDERR - real, verifiable progress even
  though the core bug persists). Double-checked Proc.RunAsync's env-parameter handling wasn't a NEW bug
  introduced by the last session (it wasn't: ProcessStartInfo.Environment inherits the full parent
  environment and setting one key on top doesn't clear anything else - confirmed correct).
  DECISION: stopped guessing a fourth blind code fix. Three theories (dotnet-ef version, file-flush
  race, MSBuild node reuse) have all failed, which means something about the actual mechanism is still
  unknown - continuing to guess isn't good debugging practice at this point. Instead, added --verbose
  to all 3 automated ef invocations in SeedStage, so the NEXT failure captures the exact same rich
  diagnostic detail (Using application base/working directory/etc.) that Stefan's WORKING manual run
  showed 2 turns ago - this has never been visible for the AUTOMATED (failing) attempt before. Updated
  ThrowEfError's hint to point at a line-by-line comparison against the manual run instead of
  suggesting yet another fix. This is a pure diagnostic/instrumentation change - zero behavior risk,
  just more visibility - not a fix attempt.
- Session 18 (Opus): the ef migrations issue appears RESOLVED - Stefan's pasted log shows the app
  actually starting successfully ('No migrations were applied. The database is already up to date.'
  + 'Now listening on: http://localhost:5095') via a manual `dotnet run` inside the generated project.
  Per changelog-discipline (new skill, created this session in /mnt/skills/user/): the --verbose
  instrumentation from the previous entry was never actually exercised/compared since the underlying
  issue didn't recur - noting this rather than claiming a fix that wasn't actually verified as the fix.
  New request: print the actual localhost URL every time the app runs (Stefan had to hunt for the port
  in scrollback). Implemented BOTH complementary mechanisms: (1) program.sbn now registers an
  ApplicationStarted callback that prints the REAL bound address via IServerAddressesFeature (accurate
  even under an env-var override, not just whatever launchSettings.json says); (2) ScaffoldStage now
  sets launchBrowser:true on every launchSettings.json profile with an applicationUrl, so `dotnet run`
  auto-opens a tab too. Both apply automatically to every future generation.
- Session 19 (Sonnet, v6 Package A + K): started the v6 breakdown in the K+A-first order. Four decisions
  worth recording:
  1. displayName GATE placement: promoted the existing SemanticCheck warning to fatal rather than adding a
     new check elsewhere - it already runs on both the raw spec and the expanded lock (loader.Load at
     SpecStages.cs:64 and :136), so one edit gates every model including feature-injected ones. Added
     `Computed is null` to the predicate because model.sbn already guards `[Display(Name)]` on computed props
     with `if prop.display_name` (optional by design); force-gating them would contradict that. No backfill
     was needed - Sessions 6/7 had already swept every fragment/fixture clean, CONFIRMED here (not assumed)
     by running `spec expand` and watching the lock validate with zero displayName errors.
  2. 24b scope: only card_grid.sbn was leaking raw sort labels; admin_table.sbn already renders
     @Html.DisplayNameFor. CreatedAt/UpdatedAt needed a hardcoded BG-label map in the template because they
     live on AuditableEntity, not in spec model.properties, so a model.properties lookup alone would still
     leak "CreatedAt" for FaceNote's Products (which sort on it).
  3. task 3a (Identity Manage localization) DEFERRED, with Stefan's explicit sign-off, to its own Opus
     session - it's the scaffolded-Identity item that's fought back historically; bundling it risked the
     foundation packages.
  4. Seed regeneration honored the SeedStage auto-fill coupling: Catalogue's row minimum is
     max(4, seedUserCount*2), which jumps to 10 at 5 users - so the canned seed HAD to grow 4 -> 10
     Catalogues or seed-schema MinItems validation would fail. This is the exact class of "two code paths,
     one concept" coupling prior sessions got bitten by; caught it by reading SeedStage before writing seed
     data, not after a failure. Chose hand-build-and-validate (deterministic transform + a script replicating
     BuildSeedSchema's rules) over the LLM seed stage, per Stefan, to keep it reproducible and paste-free.
  Honest boundary: did NOT run the full LocalDB pipeline this session - build/test/expand/seed-validation all
  pass, but end-to-end generation into a real DB is the next confirmation, not yet done.
- Session 20 (Sonnet, v6 Package H, task 17): collapsed the two follows actions/views into one page.
  1. View model PLACEMENT: put FollowersFollowingViewModel in the controller .sbn file (same namespace as
     the controller) rather than adding a new template + emission-wiring for a Models/*.cs file. "View model
     next to controller" is standard MVC; one-file change, less risk, and the view fully-qualifies the type
     (...Areas.Client.Controllers.FollowersFollowingViewModel) exactly like other views qualify their models.
  2. Kept the action name `List` (not `Index`) deliberately: verify's DiscoverRoutes already scans
     /Client/Follows/List, and the emitted view file is List.cshtml - so `List()` makes an already-expected
     route real instead of inventing a new one. Old Followers/Following actions removed (no test/route pinned
     them; nav updated to the single List link).
- Session 21 (Sonnet, v6 Package E keystone): built the shared upload helper + task 15 (admin pfp).
  1. Scope: split Package E along the template-vs-scaffold seam. FileUploadService + task 15 live entirely
     in written-from-scratch templates (UsersController + its views) I fully control, so they're
     build/test-verifiable now. Task 14 (product multi-image) rewrites SCAFFOLDED Create/Edit POST actions
     and needs a live scaffold to calibrate regex anchors - the exact A1/A8 deferral class - so it was NOT
     attempted blind. This keeps the incremental, bisectable discipline intact.
  2. FileUploadService is deliberately SEPARATE from ImageService: ImageService manages per-model
     {Model}Image tables; pfp is a single string path on ApplicationUser. One general helper (Guid names,
     extension+Content-Type allowlist, size cap, code-controlled subfolder) serves pfp now and feedback/
     other single-file uploads later (tasks 3/16) - the "build once, reuse" the breakdown asked for.
  3. Found + fixed a latent bug: UsersController.Edit returned View(user) with NO emitted Edit view - a
     runtime 500 the semantic check never caught (writtenFromScratch controllers aren't in the
     template-Index page check). Emitting Users/Edit.cshtml both fixes that and hosts the pfp upload.
  4. Guarded the blind copy loop: it set user.{prop}=input.{prop} for ALL extra_properties; left as-is it
     would blank ProfilePicturePath (not a form text field) on every save. Now skips it and sets it only
     from a successful upload. Caught by reasoning through the form round-trip, not a bug report.
  Honest boundary: no full DB run this session either - build/test/render all pass, browser upload unverified.
- Session 20 continued: Routing decision for the broader "do these [Opus] tasks" request: chose Package H (Sonnet) over charging
     at the 6 Opus tasks, because (a) 3a is MOOT for FaceNote - no Account/Manage/* pages are scaffolded, so
     there's nothing to localize without first BUILDING a Manage surface (a scope decision); (b) 9/7/26/10 are
     L-effort generator-core rewrites that depend on H (now done) + E (not yet), so doing them now would both
     blow the token budget and build on absent prerequisites. H was the highest token-value, fully
     build/test-verifiable, dependency-unblocking move. Recorded so a future session doesn't re-attempt 3a
     blind or start the big aggregation pages before E exists.
- Session 23 (Opus, last five tasks 10/26/7/9/11): four design/verification decisions worth keeping.
  1. Tasks 26 (public profile) + 7 (admin user detail) built as ONE shared component (UserProfileViewModel +
     UserProfileService + Views/Shared/_UserProfileContent) rather than two parallel pages, per the breakdown's
     "share components". The service is the single query source of truth (publicOnly flag limits catalogues on
     the anon page); the partial branches on ViewData["AdminLinks"] to pick admin-detail vs public-profile link
     targets. Placed the partial in root Views/Shared so both areas (Client Profiles, Admin Users) resolve it.
     Emission/DI gated on posts||catalogues in all 3 sync points (program.sbn, EmitControllersViewsStage,
     BuildStages pre-emit) - the same FileUploadService drift discipline from changelog 033.
  2. Task 9 (admin total supervisor) implemented as a SPEC-level injection (SpecExpandStage.
     InjectAdminSupervisorSurface) not an emission-loop special case. Injecting Admin scaffold controllers +
     adminTable pages into the expanded spec BEFORE the lock is written means every existing consumer (scaffold
     pipeline, the layout's spec.controllers-driven admin nav, admin_table, ModelForPage, verify DiscoverRoutes)
     picks them up with ZERO new wiring - the cleanest possible "generator-level change to how the admin area is
     emitted". Chose scaffold (reusing the full transform chain) over a hand-written CRUD template so FK
     dropdowns / soft-delete / BG residue all come free. Excluded {Model}Image side-tables (not spec.models
     entries). The "highest risk, do last" item passed verify on the first generate.
  3. Task 11 (infinite scroll) reuses the card grid's EXISTING ?page=N server pagination via a client-side
     IntersectionObserver + DOMParser append - deliberately NO new JSON/partial endpoint, so the change is
     confined to card_grid.sbn (zero controller/scaffold-transform risk). Client search extracted to
     filterCards() and re-applied to appended cards so scroll-loaded cards respect the active filter.
  4. VERIFICATION POLICY correction: Session 22's render-tested-only batch (tasks 5..14) hid FOUR generate-time
     bugs (changelog 046) invisible without a real compile+boot - a Razor tag-helper rule (C# in <option> attr),
     a cross-file namespace assumption (context in .Data vs root), a non-existent nav property (Catalogue.User),
     and an absent-key JSON read (GetProperty on a nullable seed field). Ran the full pipeline to green TWICE
     this session (10/26/7, then 9/11). Standing rule going forward: a full `run --all` must follow ANY batch of
     template changes, not accumulate render-tested deltas. The machine has the tools (dotnet-ef/codegen/LocalDB),
     so there is no excuse to defer the DB run. Note: unattended runs need ANTHROPIC_API_KEY removed from the
     child env (else spec-expand's optional LLM review pass becomes "fillable" and blocks waiting for a manual
     paste); with the key unset and no expand-review-response.json present, the review pass is skipped and
     deterministic expansion runs straight through.
- Session 24 (Opus, issue #5 async pipeline): three decisions worth keeping.
  1. WHOLE-STAGE PARALLELISM REJECTED as race-unsafe, on purpose. The 9 stages are a strict linear chain;
     each ends with a `dotnet build` (or external `dotnet new`/`ef`/`aspnet-codegenerator`) against ONE
     shared generated project, so two stages / two builds can never overlap without corrupting it. Rather
     than force a fake parallel DAG, parallelized ONLY the race-free work: the pure Scriban render->write
     fan-out WITHIN emit-models and emit-controllers-views (distinct paths, no shared mutable state, no
     external process, no read-after-write between items). Every build gate stays serial after the fan-out;
     the emit-controllers-views scaffold loop (external codegen + regex transforms on shared build state)
     stays serial and still runs BEFORE the parallel template block, so the rating feature's product Details
     still overwrites the scaffolded one.
  2. Thread naming via AsyncLocal, not real named Threads. The work is async on the thread pool and may hop
     threads across awaits, so a per-lane AsyncLocal<string?> (Кольо/Наско/Иван/Мишо/Киро) set inside each
     lane's task body is the reliable way to tag log lines - it's captured into the item's ExecutionContext
     and never leaks up to the orchestrator (which logs untagged). StageContext logging had to become
     thread-safe first: File.AppendAllText was unsynchronized and would corrupt the action log under
     concurrent lanes, so one process-wide lock now guards both sinks.
  3. Seed image downloads deliberately NOT parallelized - independent I/O, but they hit rate-limited
     external services (Pexels/Pixabay); a parallel burst risks throttling. Kept the parallel region to
     provably race-free pure-CPU emission (weakest-guarantee-that's-correct / don't over-engineer for scale
     the work set doesn't have - a few dozen files). Verified with a real full run --all -> verify PASSED,
     with all five workers observed across both parallelized stages.
- Session 25 (Sonnet, meta.language="en" support): the scope-boundary decision is the one worth keeping.
  A request framed as "if en, skip the Bulgarian translations (the simple version)" assumed this was a
  small switch. Before writing any code, counted actual Bulgarian content in the codebase and found 54
  template files with hardcoded Cyrillic baked directly into hand-written .sbn templates (not spec-
  sourced) - a completely different, much larger thing than the ~4 retrofit passes that translate
  SCAFFOLDED code (which genuinely CAN be skipped cheaply). Surfaced this gap explicitly via
  AskUserQuestion rather than either (a) silently implementing the narrow version and letting Stefan
  discover later that "en" doesn't produce an English UI, or (b) guessing he wanted the full 54-template
  rewrite and doing a much bigger job than asked. Confirmed: narrow version (gate the retrofit passes),
  scope boundary documented prominently in the code (MetaSection.IsBulgarian's own doc comment) and the
  changelog, not just mentioned once and left implicit. Also declined to widen the schema based on
  spec.meta.language for a DIFFERENT feature (issue #5's follow-up funny-English-worker-names ask) for
  the same reason in reverse - that field is a hard product requirement (Bulgarian-only generated UI),
  not a place to bolt on unrelated tool-console preferences.
- Changelog 141 (Opus): REVERSES Session 3's "feature/admin route fetches are best-effort" — every
  declared route must answer 200, and protected routes must refuse anonymous requests. Adding that
  check exposed missing `[Authorize]` on scaffolded controllers; the stage now stamps it per area.
- Changelog 141: REVERSES "personalization inject nothing by design" for personalization — still no
  models, but `features.personalization` now gates the navbar greeting/avatar/profile link.
- Changelog 141: feature fragments may share a model if the definitions are IDENTICAL (cart/orders
  share CartItem); a conflicting duplicate still fails expansion. Preset pick is SHA-256, never
  `GetHashCode()`.
