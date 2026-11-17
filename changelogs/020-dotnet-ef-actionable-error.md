# Actionable error for a dotnet-ef version mismatch

**Type:** Error fix
**Model:** Opus — diagnosing the first ef-migrations failure and matching it against an existing actionable-error pattern.

## Where
`src/Genesis.Core/Stages/SeedStage.cs`, the three `dotnet ef ...` invocations (`migrations add`,
`migrations has-pending-model-changes`, `database update`).

## How it happened
`ef migrations add Initial` printed "Build started... Build succeeded." and then failed with
"The specified deps.json [...\FaceNote.deps.json] does not exist." The generated project built fine -
the `dotnet-ef` CLI tool itself couldn't then locate/load the assembly it had just built.

## Why
This is the textbook symptom of a globally-installed `dotnet-ef` tool that predates the generated
project's EF Core packages. The generator pins `Microsoft.EntityFrameworkCore.Tools --version 10.0.*`
(see `BuildStages.cs`), so `dotnet-ef` itself needs to be a compatible 10.0.* version too - an older
one can still run `dotnet build` (that part is version-agnostic) but doesn't know how to find/load a
net10.0 build output afterward. There was already an identical, working actionable-error pattern for
exactly this class of "missing/incompatible external tool" problem (`dotnet-aspnet-codegenerator`, in
`BuildStages.cs`) but nothing equivalent existed for `dotnet-ef` - and the README didn't even list it
as a required prerequisite.

## What it affects
The `seed` stage (and therefore `run --all`) on any machine where the globally-installed `dotnet-ef`
tool is older than the project's EF Core 10.0.* packages - blocks migrations and database update
entirely, with only a raw stack trace and no indication of what to actually do about it.

## How to reproduce
Have an outdated global `dotnet-ef` installed, generate a project, let the pipeline reach `seed`.
Fails with `Build succeeded.` followed by `The specified deps.json [...] does not exist.`

## How to fix
Added `ThrowEfError`, mirroring the existing codegenerator pattern: on failure, checks for the exact
"deps.json does not exist" substring and, if present, appends the precise command
(`dotnet tool update --global dotnet-ef --version 10.0.*`) instead of just the bare stack trace. Wired
into all three `ef` invocations. Also added `dotnet-ef` to the README's prerequisites list, next to the
existing `dotnet-aspnet-codegenerator` entry, so this doesn't have to be discovered by hitting the error.
