# Remove AutoPazar entirely; FaceNote is now the sole fixture

**Type:** Feature / change
**Model:** Opus — repo-wide removal touching ~16 tests across every test class - cross-cutting.

## Summary
Deleted all 3 AutoPazar fixture files. FaceNote is now the only spec the test suite and documentation reference. This rippled into roughly 16 tests, since several hardcoded AutoPazar-specific model/route names, and two tests depended on variation-axis pins and feature toggles that only held for AutoPazar's fixture, not FaceNote's.

## Techniques
- Mechanical renames: Car -> Product, Cars -> Products, AutoPazar -> FaceNote, CarImages -> ProductImages
- `Explicit_axis_pin_wins_over_derivation` rewritten to test FaceNote's actual pinned axis (`namespaceStyle`), after computing FaceNote's real SHA256-derived (unpinned) values in Python to confirm the old assertions would no longer hold
- `VerifyHelpersTests`'s "feature disabled" case restructured to use a minimal hand-built `ProjectSpec` (no fixture file), since FaceNote enables every feature and there's no natural disabled case left
- New `fixtures/seed-response.facenote.json`, self-validated against a faithful reimplementation of `BuildSeedSchema`'s exact logic before being committed

## Affected files
- `fixtures/ (autopazar files deleted)`
- `tests/Genesis.Tests/CoreTests.cs`
- `README.md`
- `src/Genesis.Core/Pipeline/Pipeline.cs`
- `src/Genesis.Core/Stages/SpecStages.cs`
