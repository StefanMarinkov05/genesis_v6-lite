# Stopped guessing at the ef migrations bug - added real diagnostics instead

**Type:** Error fix (diagnostic instrumentation, not a new fix attempt)
**Model:** Opus — a debugging-discipline decision (stop guessing, add instrumentation) rather than a fix - judgement call.

## Where
`src/Genesis.Core/Stages/SeedStage.cs`, the migrations/database-update section.

## What happened
The third fix attempt (disabling MSBuild build-server node reuse) did not resolve the issue either -
the exact same "Build succeeded" then "deps.json does not exist" failure recurred a fourth time, in a
fresh run, with all three previous mitigations (dotnet-ef version confirmed correct, pre-build, node
reuse disabled) in place.

One real, verifiable win did show up: the NU1901 warnings (NuGet.Packaging/Protocol) are gone from this
run's STDERR, confirming that fix from the same session worked correctly. The core migrations bug is a
separate, still-unresolved problem.

Also double-checked, out of caution, whether the previous session's own change (passing a custom `env`
dictionary to `Proc.RunAsync`) could have introduced a *new* bug - e.g. accidentally wiping the child
process's environment instead of adding one key on top of it, which would break `PATH`/`DOTNET_ROOT`/etc.
and could plausibly cause exactly this kind of symptom. Confirmed this is not the case:
`ProcessStartInfo.Environment` inherits the full parent environment by default, and setting one key on
it does not clear anything else - correct, standard .NET behavior.

## Decision
Three different theories (dotnet-ef version mismatch, a file-flush/antivirus race, MSBuild node reuse)
have now all failed to explain or fix this. Continuing to propose a fourth blind code change without new
evidence isn't good debugging practice - each theory sounded plausible and still didn't hold up, and
there's no reason a fifth guess would be more likely to land than the previous three.

## What changed instead
Added `--verbose` to all three automated `dotnet ef` invocations in `SeedStage`. This is the one piece
of diagnostic information that has never actually been captured before: `dotnet-ef`'s own detailed
output (which assembly/working directory/application base it's using, etc.) for the **automated**,
failing attempt specifically. The same detail already exists for a **manual** run in the same kind of
folder, which has succeeded every single time it's been tried by hand - but the two have never been
compared side by side, because the automated attempt's verbose output was never captured until now.

Updated `ThrowEfError`'s hint text accordingly: instead of suggesting another fix, it now points at
comparing the newly-captured verbose output (in the STDOUT above the error) line-by-line against a
manual `dotnet ef migrations add Initial --verbose` run in the same project folder - specifically the
"Using application base" / "Using working directory" lines, since a difference there would point at the
actual root cause directly instead of another guess.

## How to reproduce / next step
Run the pipeline again. If it fails at the same point, the error output will now include the same rich
diagnostic detail as a manual `--verbose` run. Compare that against a manual run's own verbose output in
the same folder and look for any difference in the reported paths - that comparison is the fastest real
path to the actual fix from here.
