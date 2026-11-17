# B4: widen the verify language gate to the whole route surface

**Type:** Feature / change
**Model:** Opus — cross-cutting change to the generator's core verify-gate logic (whole route-surface scan); pre-Claude-Code era, all sessions ran on Opus.

## Summary
The Bulgarian-only-UI verify gate only ever checked 3 fixed pages (`/`, first cardGrid, first adminTable). Widened it to scan every discovered route: all cardGrids (anonymous), all adminTables, and every enabled Client-area feature page (Cart/Orders, Posts, Follows, Catalogues), authenticated via a real seeded-admin login. Feature/admin route fetches are best-effort (a wrong guess warns and is skipped; only a real English leak on a page that DID load still fails the gate).

## Techniques
- New `VerifyStage.DiscoverRoutes(spec)` helper + `TryGetAsync` best-effort fetch
- 2 new unit tests covering route coverage and de-duplication

## Affected files
- `src/Genesis.Core/Stages/VerifyAndReportStages.cs`
- `tests/Genesis.Tests/CoreTests.cs`
