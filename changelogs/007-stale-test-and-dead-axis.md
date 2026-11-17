# Test failure: stale assertion in Program_template_honors_di_registration_variation + a dead variation axis found

**Type:** Error fix
**Model:** Opus — test-failure root-cause analysis - distinguishing a stale assertion from a real generator bug.

## Where
`tests/Genesis.Tests/CoreTests.cs`, test `Program_template_honors_di_registration_variation`; underlying template `templates/project/program.sbn`.

## How it happened
The test asserted `.AddDatabaseDeveloperPageExceptionFilter()` was present in the rendered `Program.cs`. That call was deliberately removed from `program.sbn` in an earlier session (it needs a namespace only referenced under Individual auth scaffolding, and this project scaffolds `--auth None` - confirmed on a real SDK build at the time). The test was simply never updated to match that intentional removal.

## Why
Test/code drift: a template was correctly fixed once, but the test that had encoded the OLD behaviour was never revisited, because nothing had run the test suite for real until this session.

## What it affects
Only this one unit test; the generated `Program.cs` output itself was already correct.

## How to reproduce
Run `dotnet test` before this fix - `Program_template_honors_di_registration_variation` fails with `Assert.Contains() Failure: Sub-string not found`.

## How to fix
Rewrote the assertion to check the real, current output (the `AddDefaultIdentity/.AddRoles/.AddErrorDescriber` chain + `MapControllerRoute`) instead of the removed call.

Separate finding made while investigating (left unfixed, logged for a future session): `diRegistrationStyle` is a real, pinnable variation axis (`chained` / `separateStatements`) but `program.sbn` never actually reads it - every generated project gets the same DI style no matter what's pinned. Doesn't break anything (the fixed shape is valid code), but silently defeats the axis's purpose. Needs a `separateStatements` branch + a test that pins both values, done with a compiler on hand rather than guessed blind.
