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

rem dps-nn - BUILD AND CHECK ONLY. Does not generate anything.
rem Double-click this first; once it's green, use 2-generate.bat to actually
rem run the tool. Safe to re-run any time - it never touches out/, logs/,
rem prompts/, or seeds/ (other than the build-check log below).
cd /d "%~dp0"

if not exist logs mkdir logs >nul 2>&1
set BUILDLOG=logs\last-build-check.log
if exist "%BUILDLOG%" del "%BUILDLOG%" >nul 2>&1

echo ============================================================
echo   dps-nn - build and check
echo ============================================================
echo.

echo [1/4] Checking for the .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo.
    echo ERROR: no "dotnet" found on PATH.
    echo Install the .NET SDK first: https://dotnet.microsoft.com/download
    echo ^(this project targets net10.0 - see global.json^)
    echo.
    pause
    exit /b 1
)
for /f "delims=" %%v in ('dotnet --version') do echo       found .NET SDK %%v
echo.

if not exist dps-nn.sln if not exist dps-nn.slnx (
    echo [2/4] First run: creating the solution and adding the 3 projects...
    dotnet new sln -n dps-nn
    if errorlevel 1 goto :error
    dotnet sln add src\Genesis.Core\GenesisV6.Core.csproj
    if errorlevel 1 goto :error
    dotnet sln add src\Genesis.Cli\GenesisV6.Cli.csproj
    if errorlevel 1 goto :error
    dotnet sln add tests\Genesis.Tests\GenesisV6.Tests.csproj
    if errorlevel 1 goto :error
) else (
    echo [2/4] Solution already exists, skipping.
)
echo.

rem Every command below is captured in full (via 2>&1 into a variable, NOT
rem piped live into a scriptblock - piping a native command through
rem ForEach-Object/Where-Object resets $LASTEXITCODE to 0 even on a real
rem failure, verified by testing this exact pattern before trusting it) then
rem displayed and appended to %BUILDLOG% in explicit UTF-8 (Tee-Object has no
rem -Encoding param in Windows PowerShell 5.1 and defaults to UTF-16, which
rem non-PowerShell tools misread as spaced-out text). NOTE: no ForEach-Object
rem scriptblock ("{ }") anywhere below, even on a line that never executes -
rem verified by bisection that a later unexecuted "{ }" alone breaks every
rem earlier "goto :error" in this file with cmd.exe's own "cannot find batch
rem label" error. Trade-off: output appears once the command finishes, not
rem line-by-line as it runs - a build/test only takes seconds, so this is not
rem a real cost here.
echo [3/4] Restoring and building...
powershell -NoProfile -Command "$o = & dotnet build 2>&1; $code = $LASTEXITCODE; Write-Host ($o -join [Environment]::NewLine); Add-Content -Path '%BUILDLOG%' -Value $o -Encoding UTF8; exit $code"
if errorlevel 1 goto :error
echo.

echo [4/4] Running tests...
powershell -NoProfile -Command "$o = & dotnet test 2>&1; $code = $LASTEXITCODE; Write-Host ($o -join [Environment]::NewLine); Add-Content -Path '%BUILDLOG%' -Value $o -Encoding UTF8; exit $code"
if errorlevel 1 goto :error
echo.

echo ============================================================
echo   All green. Run 2-generate.bat next to actually generate a project.
echo ============================================================
echo.
pause
exit /b 0

:error
echo.
echo ============================================================
echo   Something failed above - full output saved to %BUILDLOG%
echo   (also copied to your clipboard - just paste it here)
echo ============================================================
echo.
powershell -NoProfile -Command "Get-Content '%BUILDLOG%' -Raw | Set-Clipboard" >nul 2>&1
pause
exit /b 1
