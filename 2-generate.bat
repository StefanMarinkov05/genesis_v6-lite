@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion
rem The chcp above MUST be the very first line (before any comment containing
rem non-ASCII text) - switching codepage mid-file re-parses cmd's already-
rem buffered lines under the new codepage and can corrupt them. Without it,
rem cmd's inherited legacy OEM codepage corrupts non-ASCII text (e.g. the
rem repo's own folder name) flowing through from dotnet's child process,
rem before it even reaches the PowerShell capture below. This whole comment
rem block is deliberately ASCII-only for the same reason.

rem dps-nn - GENERATE. Run 1-build-and-check.bat first if you haven't yet.
rem Runs exactly ONE spec per invocation - if there's more than one
rem project-spec*.json in cwd/fixtures/, or more than one file under seeds/,
rem you'll get a numbered menu to pick from; run this script again to
rem generate a different one. Every fresh generation lands in its own
rem timestamped folder under out/ - re-running never overwrites a previous
rem generation of the same project.
cd /d "%~dp0"

if not exist dps-nn.sln if not exist dps-nn.slnx (
    echo.
    echo No dps-nn.sln/.slnx found yet - run 1-build-and-check.bat first.
    echo.
    pause
    exit /b 1
)

if not exist logs mkdir logs >nul 2>&1
set RUNLOG=logs\last-generate.log
if exist "%RUNLOG%" del "%RUNLOG%" >nul 2>&1

set MARKER=logs\.pending-outdir.txt

rem If a previous invocation is still waiting on a manually-pasted LLM
rem response (exit code 3), resume it in the SAME folder it started in -
rem otherwise we'd silently restart that project from scratch in a new one.
if exist "%MARKER%" (
    set /p OUTDIR=<"%MARKER%"
    echo ============================================================
    echo   dps-nn - resuming the pending run in: !OUTDIR!
    echo ============================================================
) else (
    for /f "delims=" %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set TS=%%i
    set OUTDIR=out\run_!TS!
    echo ============================================================
    echo   dps-nn - starting a new run in: !OUTDIR!
    echo ============================================================
)
echo.

rem No --spec: if there's more than one candidate, you'll be asked to pick
rem ONE. No --out folder-per-project-name: !OUTDIR! keeps every run separate.
rem Captured in full (via 2>&1 into a variable, NOT piped live into a
rem scriptblock - piping a native command through ForEach-Object/Where-Object
rem resets $LASTEXITCODE to 0 even on a real failure, verified by testing
rem this exact pattern before trusting it - including the exit-code-3
rem manual-LLM-pending signal the check below depends on) then displayed and
rem appended to %RUNLOG% in explicit UTF-8 (Tee-Object has no -Encoding param
rem in Windows PowerShell 5.1 and defaults to UTF-16, which non-PowerShell
rem tools misread as spaced-out text). NOTE: no ForEach-Object scriptblock
rem ("{ }") anywhere below - verified by bisection that a later, NEVER-
rem EXECUTED "{ }" alone breaks every earlier "goto :error" in a batch file
rem with cmd.exe's own "cannot find batch label" error. Trade-off: output
rem appears once the pipeline finishes rather than streaming stage-by-stage
rem as it runs - a real cost for a multi-minute generation, accepted for
rem correctness (revisit with a tailed-file/Start-Process approach if this
rem matters).
echo Running - this can take several minutes; output appears when it finishes.
powershell -NoProfile -Command "$o = & dotnet run --project src\Genesis.Cli -- run --all --out '!OUTDIR!' --resume 2>&1; $code = $LASTEXITCODE; Write-Host ($o -join [Environment]::NewLine); Add-Content -Path '%RUNLOG%' -Value $o -Encoding UTF8; exit $code"
set PIPELINE_EXIT=!errorlevel!
echo.

if !PIPELINE_EXIT! equ 3 (
    echo !OUTDIR!> "%MARKER%"
    echo ------------------------------------------------------------
    echo Waiting for a manual LLM response - see the instructions above
    echo ^(also saved in full to %RUNLOG%^).
    echo Paste it where it says, then run this script again; it will
    echo resume in the SAME folder: !OUTDIR!
    echo ------------------------------------------------------------
    pause
    exit /b 3
)

if exist "%MARKER%" del "%MARKER%" >nul 2>&1

if !PIPELINE_EXIT! neq 0 goto :error

echo ============================================================
echo   Done. Project generated in: !OUTDIR!
echo   See the banner above for the URL and login credentials.
echo ============================================================
echo.
pause
exit /b 0

:error
echo.
echo ============================================================
echo   Something failed above - full output saved to %RUNLOG%
echo   (also copied to your clipboard - just paste it here)
echo ============================================================
echo.
powershell -NoProfile -Command "Get-Content '%RUNLOG%' -Raw | Set-Clipboard" >nul 2>&1
pause
exit /b 1
