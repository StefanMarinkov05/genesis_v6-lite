# Log grammar fix + worker-tag brackets removed

**Type:** Error fix

## Where
`templates/log-bg-map.json` (translation map), `src/Genesis.Core/Pipeline/Pipeline.cs` (`Tag`
helper).

## How it happened
Stefan inspected the temporary Bulgarian log dump produced at the end of the prior session's BG/EN
regeneration and found two issues:
1. `"wrote "` was mapped to `"записан файл: "` - grammatically wrong. "записан" is a passive
   participle ("written[-adj]"), not a finite verb; the log line reads as dps-nn (an implicit
   subject, the worker) performing the action, so it needs the active past-tense verb "записа"
   ("[he/she] wrote").
2. The parallel-worker name prefix rendered with square brackets around it (`[Кольо] wrote ...`),
   which Stefan wanted removed in both the BG and EN paths (worker names appear regardless of
   project language, per issue #5 from an earlier session).

## Why
Straightforward wording/formatting corrections against the previous session's own output - not
deep bugs, just polish caught by a human reading the actual rendered log.

## What it affects
Every dps-nn console/log line prefixed with a parallel-worker name, in any project (BG log
translation only touches the phrase-map, English logs are unaffected by the wording swap but do
lose the brackets since `Tag()` is shared by both language paths).

## How to fix
- `templates/log-bg-map.json`: `"wrote "` now maps to `"записа "` instead of `"записан файл: "`.
- `src/Genesis.Core/Pipeline/Pipeline.cs`'s `Tag(string msg)` helper: changed
  `$"[{worker}] {msg}"` to `$"{worker} {msg}"` - drops the brackets, comment updated to reflect the
  new example output (`"Кольо wrote Models/Product.cs"` instead of `"[Кольо] wrote ..."`).

## Verification
`dotnet test` 111/111 after updating the one stale assertion in `LogTranslationTests` that still
expected the old `"записан файл: "` wording (folded into this session's later work, see changelog
057). Confirmed live via full BG regenerate: real output now reads e.g. `"Кольо записа
Models/Product.cs"`.
