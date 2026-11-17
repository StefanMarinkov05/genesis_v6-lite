# Total generation time shown in minutes/seconds + thread-count analysis

**Type:** Feature / change
**Model:** Sonnet — user switched the session to Sonnet before this request - a small, well-scoped display change plus an analysis question, not architectural.

## Summary
The end-of-run stage-timings summary now prints the TOTAL in a human-readable minutes/seconds form
alongside the raw milliseconds — e.g. `TOTAL   385593 ms   (6m 25s / 385.6s)` — because a full
generate runs into several minutes and a bare `385593 ms` is hard to read at a glance.

Also answered (no code change) the "can more threads speed it up?" question — see the analysis below.

## Techniques
- New `PipelineRunner.HumanDuration(long ms)`: `"6m 25s / 385.6s"` once the duration passes a minute,
  else `"42.3s"`. Always includes total seconds so the two views agree.
- **InvariantCulture** on the `F1` format so the decimal separator is always `.` regardless of the
  machine's locale (this repo runs under a non-en locale, which first produced `0,5s` and failed the
  new tests — logs must read identically everywhere).
- Only the TOTAL line changed; per-stage rows stay in ms (all well under a minute individually).

## Analysis: more threads will NOT speed the pipeline up (no code change)
Recorded so a future session doesn't re-add lanes expecting a win. Real timings from a full generate:
emit-models 78.3s, identity 32.0s, emit-controllers-views 179.4s, seed 63.8s, verify 31.9s, report
0.07s. The parallel emission fan-out (changelog 051) renders a few dozen files — **single-digit-to-
low-hundreds of milliseconds total**, a rounding error next to the multi-minute wall clock. Wall-clock
is dominated by **serial external `dotnet` processes**: `dotnet build` (once per stage), ~11
`aspnet-codegenerator` scaffolds in emit-controllers-views (each running its own build), `ef`
migrations/update, and the app boot. Those are exactly the parts that are **race-unsafe to
parallelize** (they mutate the one shared generated project and run their own builds). So the 5 named
lanes already saturate the tiny CPU-bound render work; more lanes add scheduling overhead for zero
wall-clock gain (and 5 is a natural cap — the feature is 5 named workers). Any real speedup would come
from cutting the number of build/codegen gates, a separate and riskier change to the incremental-
verification design, not a thread-count knob.

## Verification
- `dotnet build` clean; `dotnet test` **89/89** (4 new `HumanDuration` theory cases incl. the 385593 ms
  value from the real run). Display-only change to the summary line — no effect on generated output, so
  no full generate needed (the "run a full generate after any change" rule targets template changes
  that can hide compile/boot bugs; this is neither).

## Affected files
- `src/Genesis.Core/Pipeline/Pipeline.cs` (`HumanDuration` + TOTAL line)
- `tests/Genesis.Tests/CoreTests.cs` (4 new theory cases)
