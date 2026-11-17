# Seed stage: reuse existing valid seed data instead of always re-prompting

**Type:** Feature / change
**Model:** Opus — generator-core behavior change (idempotent re-run semantics).

## Summary
`seed` always called the manual-LLM exchange, even when a valid `Data/seed-data.json` already existed from a prior run - forcing a fresh paste-and-resume cycle on every single re-run. Now it checks for an existing file first and re-validates it against the exact schema an LLM response would have to satisfy; only falls through to a fresh exchange if the file is missing or no longer matches the current spec.

## Techniques
- Existing-file check placed before the LLM exchange, reusing `LlmBridge.Validate` (not just "file exists")
- A spec change that invalidates old data (new model, new required field) still correctly triggers regeneration - this can't silently seed stale/wrong data

## Affected files
- `src/Genesis.Core/Stages/SeedStage.cs`
