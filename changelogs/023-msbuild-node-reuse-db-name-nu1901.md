# Real fix attempt #2 for ef migrations (node reuse) + DB name uniqueness + NU1901 properly fixed

**Type:** Error fix + Feature / change
**Model:** Opus — revised root-cause theory after a prior fix failed - required not repeating a theory already ruled out.

## Error: ef migrations add still failing after the previous "fix"

### Where
`src/Genesis.Core/Stages/SeedStage.cs`, the migrations/database-update section.

### How it happened
Session 15's fix (an explicit `dotnet build` before any `dotnet ef` command, plus a single retry
after a 2s delay) did not resolve it - the exact same "Build succeeded" then "deps.json does not
exist" failure recurred, in a brand new run.

### Why
A single prior build ruled out a pure file-flush/antivirus race (that theory predicted one extra
build would be enough - it wasn't). The real, better-fitting culprit: MSBuild's build-server "node
reuse" feature keeps a persistent build-server process running in the background across invocations
for speed. Rapid back-to-back builds - our own pre-build, then `dotnet-ef`'s own internal build,
seconds apart - can hit that same reused, possibly still-busy node. This matches the exact symptom
observed two turns running: the pipeline's own automated attempt fails, but the identical command run
manually moments later (a fresh shell, not reusing that node the same way) succeeds outright, every
single time it was tried.

### What it affects
Every `run --all` / `seed` invocation - and more consistently since an earlier change made every run
land in a brand-new output folder, removing whatever incidental spacing/timing used to mask this.

### How to reproduce
Generate a project and let it reach the `seed` stage's migrations step; per Stefan's reports this
reproduced consistently across three separate attempts.

### How to fix
Set `DOTNET_CLI_DISABLE_BUILD_SERVERS=1` (via `Proc.RunAsync`'s existing `env` parameter) on the
pre-build call AND every `dotnet ef` invocation, forcing a clean, non-reused process every time instead
of hoping extra builds or short delays are enough. Widened the retry backstop from 1 to 2 retries (3
total attempts) with increasing delays (2s, 5s). Updated `ThrowEfError`'s hint text: a failure that
still reaches it after all of this is a genuinely rarer, more persistent issue, not the common
transient case - the hint now points at checking `dotnet-ef --version` and the actual `bin/` output
directly rather than repeating the same suggestion a third time.

## Feature: unique database name per generation + real NU1901 fix

### Summary
Two separate requests: avoid silently colliding with an old database left behind by an earlier tool
version (Stefan found exactly this - a "FaceNote" LocalDB database from a much older v3 run), and
properly address the NU1901 low-severity NuGet advisory instead of just living with the warning.

### Techniques
- Database name now gets a timestamp suffix (`{Project}_{yyyyMMddHHmmss}`) computed once in
  `ScaffoldStage`, which - thanks to the existing `state.json` skip-check - only ever runs once per
  genuinely new generation attempt, so the name stays stable for that attempt's lifetime. Deliberately
  NOT a live "does this name already exist" query against LocalDB/SQL Server: that needs a new package
  dependency and a live-connection assumption that can't be verified without a compiler on hand.
  Unconditional uniqueness achieves the same practical goal - never collide with an old database -
  without that risk.
- The actual database name now appears in the completion banner (read back from `appsettings.json`),
  so it's always visible which database a given run actually used.
- Searched and confirmed the patched version for GHSA-g4vj-cjjj-v7hg in the `6.12.x` line is `6.12.5`.
  Added explicit direct `PackageReference`s to `NuGet.Packaging`/`NuGet.Protocol` at that version in the
  generated project - NuGet always resolves to the highest version requested anywhere in the dependency
  graph, so this upgrades the vulnerable transitive copy pulled in by the codegen/EF tooling packages.
  This is the real fix, not just hiding the message.
- Belt-and-braces: also added `NU1901` to the generated project's own `NoWarn` (matching this tool's
  own existing `NU1902`-`NU1904` `NoWarn` convention), via structure-aware `XDocument` editing rather
  than naive string manipulation, as a safety net in case the version bump doesn't fully silence it on
  its own.

### Affected files
- `src/Genesis.Core/Stages/SeedStage.cs`
- `src/Genesis.Core/Stages/BuildStages.cs`
- `src/Genesis.Cli/Program.cs`
