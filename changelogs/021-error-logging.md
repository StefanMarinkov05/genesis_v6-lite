# Write error messages into logs/, not just the console

**Type:** Feature / change
**Model:** Opus — traced System.CommandLine's own exception middleware to avoid shipping dead code - needed deep tool-internals reasoning.

## Summary
Failures previously only ever went to the console (or, for stage failures, to the per-project
`out/<Project>/dps-nn.log.jsonl` with just the message, no stack trace). Now every failure lands in
`logs/` with full detail, in the right place depending on whether a project context exists yet.

## Techniques
- New `StageContext.Error(msg)`, alongside the existing `Info`/`Debug` - writes to console (distinct
  `[dps-nn] [ERROR]` prefix) and to `logs/<Project>/actions.log.jsonl` at `error` level, with the full
  exception (`ex.ToString()`, not just `ex.Message`) so the stack trace survives even after the console
  scrollback is gone.
- Wired into `PipelineRunner`'s existing per-stage catch block (used by `run --all`) and into
  `RunStage`'s catch block (used by every individual stage command, e.g. `dps-nn seed` on its own) -
  both previously only handled `LlmPendingException` specially and let everything else propagate with
  no persistent record at all.
- New `logs/global-errors.jsonl` for failures that happen BEFORE any project context exists (spec
  resolution itself throwing, `GenesisRoot()` not finding `templates/`) - there's no project name to
  file those under.
- Real design correction made while implementing: a wrapper around `root.InvokeAsync(args)` at the very
  end of `Program.cs` would have been dead code for handler-thrown exceptions. System.CommandLine's own
  default pipeline already has exception-handling middleware (`UseExceptionHandler` - visible in
  Stefan's own pasted stack trace) that intercepts and prints "Unhandled exception: ..." without
  re-throwing past `InvokeAsync`. Fixed by wrapping the BODY of every command handler (`buildCmd`,
  `expandCmd`, the shared per-stage-command handler, `runCmd`) in its own try/catch instead - those
  actually run, since they're inside the handler, before System.CommandLine's middleware ever sees the
  exception. The outer `InvokeAsync` wrapper was kept anyway as harmless defense-in-depth.

## Affected files
- `src/Genesis.Core/Pipeline/Pipeline.cs`
- `src/Genesis.Cli/Program.cs`
- `README.md`, `logs/README.md`
