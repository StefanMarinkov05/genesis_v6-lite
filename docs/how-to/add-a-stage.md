<!-- last-verified: e95fedf -->
# How to add a pipeline stage

**Diataxis type:** how-to (task steps). **Source of truth:** `src/Genesis.Core/Stages/*` +
`src/Genesis.Cli/Program.cs`. For *why* the pipeline is staged/resumable this way, see
`docs/explanation/architecture-decisions.md`; for the current stage list and verify-gate detail,
see `docs/reference/control-flow.md`. This doc holds steps only.

## Steps

1. **Implement `IStage`** (`Pipeline.cs`):
   ```csharp
   public sealed class MyStage : IStage
   {
       public string Name => "my-stage";
       public async Task RunAsync(StageContext ctx, CancellationToken ct = default) { ... }
   }
   ```
   `RunAsync` **must be idempotent** — re-running it (via `run --all` resume or `--from`) has to
   detect and skip already-done work internally. This is a contractual requirement, not a
   suggestion: `PipelineRunner` only skips a stage it already marked done in `state.json`; if
   forced to re-run (`--from`), your stage is the only thing standing between that and duplicate
   work.

2. **Place it in the canonical order.** Add `"my-stage"` to `PipelineRunner.CanonicalOrder` in
   `Pipeline.cs`, in the position it actually runs. This array is also what
   `docs/reference/control-flow.md`'s stage table documents — update that doc in the same commit
   (same-commit rule).

3. **Add it to the `run --all` stage array**, in `Program.cs`'s `runCmd` handler — the `IStage[]`
   literal passed to `new PipelineRunner(stages)`. Position matches step 2.

4. **Wire the standalone invocation**, if the stage should also be runnable on its own (most are):
   call `AddStageCommand("my-stage", (ctx, spec, outDir, projectName) => new MyStage(...))` near the
   other `AddStageCommand` calls. This gives you `dps-nn my-stage --spec ... --out ...` for
   iterating on the stage in isolation without a full `run --all`.

5. **End in a `dotnet build` gate** if your stage emits any code — every code-emitting stage does.
   Use `Proc.RunAsync("dotnet", "build --nologo", ctx.ProjectDir, ...)` and `ThrowIfFailed(...)`
   (see any existing stage in `BuildStages.cs` for the pattern). This is what makes a broken
   emission fail immediately at its own stage rather than surfacing three stages later as a
   confusing unrelated error.

6. **Log through `ctx.Info`/`ctx.Error`**, not raw `Console.WriteLine` — this keeps your stage's
   output in the leveled action log (`logs/<Project>/actions.log.jsonl`) and gets the Bulgarian
   log-translation pass for free on a Bulgarian project.

7. **Checkpoint is automatic** — `PipelineRunner.RunAllAsync` calls `ctx.State.MarkDone(stage.Name)`
   after your `RunAsync` returns successfully. You don't call this yourself inside a stage running
   under `run --all`; the standalone `AddStageCommand` path does call it via `RunStage`.

8. **If your stage needs an LLM call**, don't call an API directly — use `LlmBridge` (the ADR-005
   file contract) the way `SeedStage`/`SpecBuildStage` do, so manual and automatic mode both work
   for free.

## Verifying it

- `dotnet test` — add a test class or extend an existing one in `tests/Genesis.Tests/`.
- A real `run --all` against the FaceNote fixture, watching your stage's position in the stage
  timings summary that `PipelineRunner` prints at the end of a run.
- If your stage is resumable-sensitive, kill a run mid-way and re-run `run --all` to confirm your
  idempotency check actually skips the completed work rather than redoing it.
