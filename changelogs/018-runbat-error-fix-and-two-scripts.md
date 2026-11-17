# Build error fix (RelativeOrFull) + split run.bat into two scripts + one folder per run

**Type:** Error fix + Feature / change
**Model:** Opus — real build-error fix (accessibility bug across an assembly boundary) plus script restructuring.

## Error: CS0117 'InteractiveSelection' does not contain a definition for 'RelativeOrFull'

### Where
`src/Genesis.Core/Cli/InteractiveSelection.cs`, method `RelativeOrFull`; called from `src/Genesis.Cli/Program.cs`.

### How it happened
`RelativeOrFull` was declared `internal static`. `Genesis.Cli` (where `Program.cs` calls it) is a
*separate assembly* from `Genesis.Core`, and only `Genesis.Tests` has `InternalsVisibleTo` access to
`Genesis.Core`'s internals - a normal project reference does not grant access to `internal` members
across assemblies.

### Why
Every other member on the same class was already `public`; this one was missed when the class was
first written.

### What it affects
Blocked the entire `Genesis.Cli` build (and therefore `dotnet run`/the generator) - `Genesis.Tests`
built fine, since it *does* have internals access, which is why only one of the two projects failed.

### How to reproduce
`dotnet build` on the version before this fix. Fails with exactly the error above.

### How to fix
Changed `RelativeOrFull` to `public`, matching the rest of the class. Swept every other
`InteractiveSelection` member `Program.cs` calls, and every other Genesis.Core symbol referenced from
`Program.cs`, to confirm no sibling accessibility mismatches remain.

## Feature: split the one-click script into two, and one folder per generation run

### Summary
Split the single `run.bat` into two scripts matching two separate concerns, and made every generation
run land in its own folder instead of overwriting the last one.

### Techniques
- `1-build-and-check.bat` - SDK check, `dps-nn.sln` bootstrap (first run only), `dotnet build` +
  `dotnet test`. Never generates anything.
- `2-generate.bat` - runs the pipeline. Always exactly ONE spec per invocation (the interactive picker
  from `016` already only ever lets you choose one at a time - to generate a different spec, run this
  script again).
- Deliberately handled "one folder per project" at the **script** level (a computed
  `out/run_<yyyyMMdd_HHmmss>/` output folder), not by changing the engine's `StageContext.ProjectDir`
  computation - that path is threaded through `StateStore`, `VerifyStage`'s URLs, and the completion
  banner, so changing it blind (still no compiler on hand) was a much larger risk than a timestamp in a
  batch script.
- Handled the resume interaction correctly: if a run pends on a manual LLM paste (exit code 3), the
  *next* click must resume in the SAME timestamped folder, not silently start over in a new one. A
  small marker file (`logs/.pending-outdir.txt`) records the in-progress folder and is deleted once
  that run actually completes.

### Affected files
- `src/Genesis.Core/Cli/InteractiveSelection.cs`
- `run.bat` (deleted)
- `1-build-and-check.bat` (new)
- `2-generate.bat` (new)
- `README.md`
