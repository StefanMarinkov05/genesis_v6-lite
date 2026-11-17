# 078 — Build/generate error capture: log file + clipboard, four real bugs found

**Type:** Feature / change (also qualifies as an error-fix cluster — logged with the fuller template
since every one of the four bugs was found by actually running the fix, not assumed)
**Model:** Sonnet — real-bug diagnosis under an existing script structure via systematic bisection,
not a design decision.

## Summary

Stefan pasted a truncated System.CommandLine stack trace and asked for either a copy button or an
exported file I can always check after a build/generate failure — manual terminal copy-paste loses
the actual message when the trace is long. Added both: `1-build-and-check.bat` and `2-generate.bat`
now capture their full output to a fixed-path log (`logs/last-build-check.log` /
`logs/last-generate.log`, overwritten fresh each run) **and** auto-copy it to the clipboard on
failure, so Stefan can just paste it in chat and I always know exactly which file to read.

Getting there took four real, sequentially-discovered bugs — each one only surfaced by actually
running the "fix" and reading its output, exactly the discipline CLAUDE.md's changelog rule asks
for. Recorded in full because each is a genuinely non-obvious cmd.exe/PowerShell interaction worth
not re-discovering:

1. **Cyrillic corruption ("ДПС-НН" → "!-").** The `.bat` files never called `chcp`, so their
   `cmd.exe` session inherited the legacy OEM codepage, corrupting any non-ASCII text (the repo's
   own folder name) flowing through from `dotnet`'s child process before it even reached the
   PowerShell capture. Fixed with `chcp 65001` as the literal first line — **must** be the first
   line: switching codepage mid-file re-parses cmd's already-buffered lines under the new codepage
   and corrupts *those* instead, which is exactly what happened when it was first placed a few
   lines down (confirmed by testing, not assumed) — cmd's own "not recognized" errors appeared for
   fragments of a comment that had never had that problem when chcp was moved to line 1 and the
   comment made ASCII-only.
2. **UTF-16 log files.** `Tee-Object` has no `-Encoding` parameter in Windows PowerShell 5.1 and
   defaults to UTF-16, unreadable to any non-PowerShell tool as spaced-out text. Switched to
   `Add-Content -Encoding UTF8`.
3. **Exit codes silently becoming 0.** Piping a native command's output directly into a
   `ForEach-Object { }` scriptblock resets `$LASTEXITCODE` to 0 even after a genuine failure —
   confirmed by isolated testing (`cmd /c 'echo hi & exit 7' *>&1 | ForEach-Object {...}` reliably
   reports exit 0). This would have made every real build/test failure silently look like success.
   Fixed by capturing fully into a variable first (`$o = & cmd 2>&1; $code = $LASTEXITCODE`) and
   reading `$LASTEXITCODE` *before* any scriptblock runs. Trade-off: output now appears once the
   command finishes rather than streaming live — negligible for a ~10s build/test, a real cost for
   `2-generate.bat`'s multi-minute pipeline run, accepted for correctness (revisit with a
   tailed-file/`Start-Process` approach if live progress matters later).
4. **A later, never-executed `{ }` scriptblock breaking an *earlier* `goto :error`.** The most
   surprising one: even after fixing bug 3, `goto :error` still failed with cmd.exe's own "The
   system cannot find the batch label specified" — for a label that demonstrably exists in the
   file. Found by systematic bisection (pristine original vs. each change added back one at a
   time): a `ForEach-Object { }` scriptblock anywhere later in the file — even on a line the failing
   branch never reaches — corrupts cmd.exe's label-scanning for a `goto` that fires earlier. Fixed
   by removing the last curly-brace scriptblock (`Write-Host ($o -join [Environment]::NewLine)`
   instead of piping through `ForEach-Object`).

## Verified

Both scripts tested end-to-end for real, multiple times, through both the success and failure
paths, via `cmd /c` (not just PowerShell-native invocation, which has its own `$var`
pre-expansion quirk that produced a false negative during testing — also documented so it isn't
mistaken for a real bug next time). Confirmed: correct exit codes (0 and 1) propagate to the
caller, the log file is valid UTF-8 with correctly-rendered Cyrillic, `goto :error` fires
correctly, and the clipboard receives the exact failure text.

## Affected files

- `1-build-and-check.bat`, `2-generate.bat`
