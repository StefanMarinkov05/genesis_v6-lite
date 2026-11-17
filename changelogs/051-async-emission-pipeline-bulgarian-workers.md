# Async emission pipeline with Bulgarian worker names

**Type:** Feature / change
**Model:** Opus — explicitly requested at XL effort with thinking on - cross-cutting change to the generator's core pipeline concurrency model, needed dependency-graph reasoning to avoid a race-unsafe design.

GitHub issue: https://github.com/StefanMarinkov05/genesis_v6/issues/5 (v6 task #5 — async pipeline
with Bulgarian thread names). NOTE: this is a *different* "task #5" from the v6-task-breakdown's
Package-B row 5 (cart badge, done earlier) — same number, unrelated work.

## Summary
Parallelized the pipeline's pure-emission fan-out across up to five named worker lanes
(**Кольо, Наско, Иван, Мишо, Киро**), which now show up as tags in the logs (e.g.
`[dps-nn] [Кольо] wrote Models/Product.cs`). Only the genuinely race-free work was parallelized;
the stage graph itself stays serial.

## Dependency-graph analysis (why NOT whole-stage parallelism)
The 9 stages are a **strict linear chain**: each consumes the previous stage's files and ends with a
`dotnet build` (or an external `dotnet new` / `ef` / `aspnet-codegenerator`) against **one shared
generated project**. Two stages — or two builds — running at once on that project would corrupt each
other, so whole-stage parallelism is race-**unsafe** and was deliberately not attempted.

The only race-free parallelism is **within the two pure-emission stages**: each Scriban render→write
reads the immutable spec/variation and writes a **distinct file path**, with no shared mutable state,
no external process, and no read-after-write dependency between items. That fans out safely; the build
gate stays serial, after the fan-out.

## Techniques
- **New `ParallelEmit` executor** (`src/Genesis.Core/Pipeline/ParallelEmit.cs`): up to 5 lanes pulling
  from a shared `ConcurrentQueue` (dynamic load-balancing beats static chunking when render costs
  vary). Lane `i` is named `WorkerNames[i]` and sets an `AsyncLocal<string?> CurrentWorker` inside its
  own task body (an AsyncLocal write doesn't flow back to the parent, so lanes never see each other's
  names and the orchestrator context stays `null`). Fail-loud: any item throwing surfaces through
  `Task.WhenAll`.
- **Thread-safe logging** (`StageContext` in `Pipeline.cs`): a process-wide `LogLock` now guards both
  the console write and the previously-unsynchronized `File.AppendAllText` — concurrent appends could
  otherwise interleave/corrupt the action log. A `Tag()` helper prefixes each line with the current
  worker's name when one is running (null → unchanged sequential output).
- **`emit-models`**: all model/dto/context/user/aux renders collected into work items and run through
  `ParallelEmit`, then one serial `dotnet build`. The DbContext references ApplicationUser only at
  *build* time, and both are on disk before the build — so emission order between them is irrelevant.
- **`emit-controllers-views`**: the post-scaffold template block (users views, feature stacks, profile
  aggregation stack, layout, declared pages) collected into lazy work items — the Scriban render runs
  *inside* the lane (via `Func<string>`), so the CPU-heavy part parallelizes, not just the file write.
  The scaffold loop (external `aspnet-codegenerator` + regex transforms on shared build state) stays
  **serial** and still runs before this block, so the rating feature's product `Details` correctly
  overwrites the scaffolded one. Unknown-page-kind still fails loud eagerly, before any lane starts.
- **`TemplateEngine.RenderFile` confirmed thread-safe**: it builds all Scriban state locally per call
  (new `Template`/`ScriptObject`/`TemplateContext`) and touches no shared mutable field, so a single
  shared engine is reused across lanes.

## Deliberate non-goal
Seed-stage image downloads were **not** parallelized. They're independent I/O and would speed seed up,
but they hit rate-limited external services (Pexels/Pixabay) — a parallel burst risks throttling. Per
the "pick the weakest guarantee that's still correct / don't over-engineer for imaginary scale"
principle, the parallel region is confined to provably race-free pure-CPU emission. (Kleppmann lens:
this is a maintainability/throughput win on a few-dozen-file work set, not a scalability play — five
lanes is already more than enough.)

## Verification
- `dotnet build` clean; `dotnet test` **85/85** (79 baseline + 6 new `ParallelEmitTests`: every item
  runs exactly once, every item runs under a Bulgarian worker name, names/order correct, exceptions
  propagate, empty input is a no-op, orchestrator context stays untagged).
- **Full `run --all` (resumed from `emit-models`) → verify PASSED: build, db, boot, http, manifest,
  language.** All five workers observed across both parallelized stages; completion banner + report
  emitted; canonical fixtures untouched (archival is copy-not-move).

## Affected files
- `src/Genesis.Core/Pipeline/ParallelEmit.cs` (new)
- `src/Genesis.Core/Pipeline/Pipeline.cs` (thread-safe logging + worker tag)
- `src/Genesis.Core/Stages/BuildStages.cs` (`emit-models` fan-out)
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs` (post-scaffold template fan-out)
- `tests/Genesis.Tests/CoreTests.cs` (new `ParallelEmitTests`)
