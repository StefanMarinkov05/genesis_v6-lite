# Real root cause: dotnet-ef race condition, not a version mismatch

**Type:** Error fix
**Model:** Opus — root-cause debugging from diagnostics, connecting the symptom to a prior architectural change (per-run folders).

## Where
`src/Genesis.Core/Stages/SeedStage.cs`, the migrations/database-update section.

## How it happened
The previous fix (020) assumed a `dotnet-ef` version mismatch, since "Build succeeded" followed by
"the specified deps.json does not exist" is the textbook symptom of that. Stefan's diagnostics ruled
that out: `dotnet ef --version` confirmed `10.0.9` (correct, matches the project's EF Core 10.0.*
packages). More tellingly - the exact same command, `dotnet ef migrations add Initial --verbose`, run
manually right after the pipeline's own automated attempt had just failed, completed successfully
end-to-end in the same folder. Same command, same folder, back-to-back: one fails, one succeeds.

## Why
That's the signature of a race condition, not a configuration problem. `dotnet-ef`'s own internal
sequence is: evaluate the project via `dotnet msbuild /getProperty:...`, then `dotnet build`, then
immediately `dotnet exec --depsfile ...` to load the just-built assembly and run the actual command.
On a project folder that has never been built before, that build-then-immediate-load can race the
filesystem (antivirus scanning a freshly-written DLL, writes not fully flushed yet) - a known class of
`dotnet-ef` flakiness. This connects directly to an earlier change (019, "separate folder per run"):
every generation now lands in a brand-new `out/run_<timestamp>/` folder, so every single run is now a
first-ever build in that exact location - precisely the scenario prone to this race. Reusing the same
output folder across runs (the old behavior) would have kept that folder's build "warm" after the
first run and likely masked this most of the time.

## What it affects
Every `run --all` / `seed` invocation, intermittently but apparently consistently for Stefan's setup,
given the new per-run folder structure removes the "already warm" mitigation that used to mask it.

## How to reproduce
Generate a project into a brand-new output folder and let it reach the `seed` stage's migrations step.

## How to fix
Added an explicit, fully-separate `dotnet build` in `SeedStage` before any `dotnet ef` command - since
it's its own process that fully exits before the `ef` commands start, it closes the specific race
window (`dotnet-ef`'s own internal build becomes a fast, already-up-to-date no-op). Added
`RunEfWithRetryAsync` as a defensive backstop: retries once, only for the exact "deps.json does not
exist" signature, after a short settle delay - any other failure, or a second failure after the retry,
is not retried again and is handled by the existing `ThrowEfError` as before. Updated that error
message's hint text to no longer lead with "your dotnet-ef version" as the primary suspect, since a
failure that gets past the pre-build and the retry is a genuinely persistent issue, not the common
transient case anymore.
