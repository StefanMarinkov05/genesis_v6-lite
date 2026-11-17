# Follows Toggle: DbUpdateConcurrencyException on double-click unfollow (item 5)

**Type:** Error fix

## Where
`templates/controllers/follows_controller.sbn`, `Toggle` action.

## How it happened
`Toggle` fetches the existing `Follow` row (if any), then either adds or removes it, then calls
`SaveChangesAsync()` with no exception handling. Since each HTTP request gets its own scoped
`DbContext`, two near-simultaneous unfollow requests (a double-click, or a resubmitted form) can
both fetch the SAME row before either commits: the first request's `Remove` + `SaveChangesAsync`
succeeds (1 row deleted); the second request's `Remove` then tries to delete the SAME already-gone
row, and EF's optimistic-concurrency row-count check throws `DbUpdateConcurrencyException` ("expected
to affect 1 row(s), but actually affected 0 row(s)") - an unhandled 500 error.

## Why
Classic TOCTOU (time-of-check-to-time-of-use) race between "read existing row" and "delete it",
with no idempotency guard for the case where a concurrent request already applied the same change.

## What it affects
Any unfollow (and, less commonly since it'd need a genuine simultaneous double-follow, a follow)
triggered twice in quick succession - double-click, slow network causing a user to click "unfollow"
again, or back-button resubmission.

## How to fix
Wrapped `SaveChangesAsync()` in a `try/catch (DbUpdateConcurrencyException)` that swallows the
exception and proceeds to the normal redirect - the desired end-state (not following) has already
been reached by the concurrent request that won the race, so this is a correct idempotent no-op,
not an error. Scoped narrowly to the exact exception type actually observed in Stefan's stack trace
(the delete-race path) - did not also guard the insert path's potential unique-constraint violation
(`DbUpdateException`, a different exception type) since that wasn't confirmed/reported.

## Verification
`dotnet build` + `dotnet test`: 111/111 (one unrelated flaky parallel-timing test - passed in
isolation on rerun, not connected to this change). Not yet live-regenerated/reproduced under real
concurrent load (time-constrained session) - logic verified by reading the exact EF Core failure
mode from the reported stack trace, not guessed.
