# Build error: TemplateEngine.ResolveDesignPreset (CS9105)

**Type:** Error fix
**Model:** Opus — first-ever real-compiler-caught bug - root-cause build-error diagnosis (CS9105).

## Where
`src/Genesis.Core/Util/TemplateEngine.cs`, method `ResolveDesignPreset`.

## How it happened
The method was declared `private static` but read `genesisRoot`, a primary-constructor (instance) parameter of the enclosing class. A static method cannot capture instance/primary-constructor state - the C# compiler rejects this with CS9105 ("Cannot use primary constructor parameter in this context").

## Why
This was a pre-existing bug from an earlier session (the "A10 design system" work) that had never been run through a real compiler before. It made the entire Genesis.Core project fail to build.

## What it affects
Every build of the generator itself. Nothing about a *generated* project - this is the tool's own code.

## How to reproduce
Run `dotnet build` on the repo (any version before this fix). It fails immediately with:
`CS9105: Cannot use primary constructor parameter 'string? genesisRoot' in this context.`

## How to fix
Made `ResolveDesignPreset` an instance method (dropped `static`) and removed the now-redundant `spec`/`variation` parameters, since they're already visible as instance state. Updated the one call site to match.
