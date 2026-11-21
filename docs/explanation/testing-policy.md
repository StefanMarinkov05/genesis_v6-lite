<!-- last-verified: ad7411e -->
# Testing policy — prove the bug, fix it, prove the test

**Diataxis type:** explanation (policy). How tests are written and judged in this project, for
humans and agents alike. The suites: an xUnit suite under `tests/Genesis.Tests/` and the GUI's
`node --test` suite under `gui/`; both run in CI on every push and pull request.

## The rule for every bug: red, green, revert, green

A bug fix is not done when the symptom disappears. It is done when a test **proves** the bug
existed and **proves** the fix removed it:

1. **Red — reproduce.** Write a test that exercises the bug, and run it. It must fail, and fail
   *for the reason the bug describes* (read the failure message — a test that fails on a typo
   proves nothing).
2. **Green — fix.** Change the code until the test passes. Change nothing the test does not need.
3. **Revert — prove the test.** Undo the fix and keep the test. Run it again: it must fail again.
   A test that still passes without the fix does not test the bug — it passed by coincidence, and
   would let the bug come back unnoticed. Rewrite it until the revert turns it red.
4. **Green — restore.** Put the fix back; the test passes. Commit the test and the fix together,
   and name the test after the behavior (and the changelog entry) so the next reader knows why it
   exists.

The revert step is the one most often skipped and the one that matters most: it is the only
evidence that the test is connected to the bug at all.

## New behavior: test first

New features follow the same order: the test that describes the behavior is written first and
seen to fail, then the code makes it pass, then the code is cleaned up with the test as the safety
net. A test written after the code tends to describe what the code does, not what it should do.

## Regression: the suite only grows

- Every fixed bug leaves its test behind. A regression test is never deleted, skipped or weakened
  to make a build pass — if it fails, the bug is back.
- The full suites run on every push and pull request (`.github/workflows/ci.yml`); a change with a
  red CI is not merged.
- A **flaky test is a bug**, in the test or in the code, and gets the same red-green-revert
  treatment. Example: a parallel-emit test that failed only on a single CPU was reproduced on
  purpose (`taskset -c 0`, 30 failures in 30 runs), fixed with an explicit barrier, and re-run
  (0 in 30) — changelog 134.
- For anything that changes generated apps, the end-to-end test is the pipeline itself: a full
  run on the FaceNote fixture must pass `verify` (build, migrate, boot, HTTP checks, English-leak
  scan).

## Mutants: testing the tests

Passing tests show the code works for the cases tested; they do not show the tests would notice a
mistake. **Mutation testing** does: it makes small deliberate changes to the code — flips a
condition, removes a call, changes a constant — and runs the suite against each changed copy (a
*mutant*). A mutant the suite fails on is *killed*; a mutant that passes every test *survives*,
and marks behavior no test actually checks.

- Surviving mutants in code that matters (stage logic, spec validation, the agent's repair loop,
  path handling) get a test that kills them.
- The revert step above is a hand-made, targeted mutant: the fix removed is the most important
  mutation of all.
- Tools: **Stryker.NET** for the .NET suite and **StrykerJS** for the GUI. They are not yet wired
  into the repo or CI — a full run is slow — so mutation runs are done on demand, per module,
  before a release or after a large change to that module.

## What "done" means

A change is done when: the relevant tests were seen red and then green, the bug-fix tests survived
the revert check, both suites pass, `verify` passes for changes to generated output, and the
changelog entry names the tests that prove it. See also [techniques and traps](techniques.md) for
the recurring build traps, and `changelogs/README.md` for how changes are recorded.
