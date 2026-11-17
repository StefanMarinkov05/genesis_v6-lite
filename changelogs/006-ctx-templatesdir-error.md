# Build error: EmitControllersViewsStage.ApplyBgResiduePass (CS1061)

**Type:** Error fix
**Model:** Opus — same build-fix session; root-cause diagnosis of a non-existent property reference (CS1061).

## Where
`src/Genesis.Core/Stages/EmitControllersViewsStage.cs`, method `ApplyBgResiduePass`.

## How it happened
The code referenced `ctx.TemplatesDir`, a property that never existed on `StageContext`. Every other call site in the codebase correctly builds the templates path via `ctx.GenesisRoot` + `Path.Combine(..., "templates", ...)` (matching `StageContext.Template()`'s own implementation) - this one spot used a name that was never actually defined.

## Why
Likely a typo/API mismatch introduced when this method was authored in an earlier, uncompiled session; nothing before this had ever built the project, so the mismatch was invisible.

## What it affects
Same as above - blocks every build of the generator itself.

## How to reproduce
Run `dotnet build`. Fails with:
`CS1061: 'StageContext' does not contain a definition for 'TemplatesDir'...`

## How to fix
Replaced `ctx.TemplatesDir` with `Path.Combine(ctx.GenesisRoot, "templates", ...)`, matching the pattern used everywhere else in the codebase.
