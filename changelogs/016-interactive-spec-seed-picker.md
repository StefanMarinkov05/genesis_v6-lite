# Interactive spec/seed picker

**Type:** Feature / change
**Model:** Opus — new CLI subsystem, plus a real bug caught while wiring it into existing command handlers.

## Summary
Added two independent interactive menus so multiple project specs or seed-response files can be chosen per run instead of remembering/typing exact paths. Both are fully backward-compatible: an explicit `--spec` always wins with zero prompting, and neither menu appears at all with 0-1 candidates.

## Techniques
- New pure, zero-Console-I/O `InteractiveSelection` helper (file discovery, menu formatting, selection parsing) - fully unit-tested (7 tests) without needing a real console
- Spec picker: scans cwd + `fixtures/` for `project-spec*.json`, shows a numbered menu when there are 2+
- Seed picker: new `seeds/` folder convention; the chosen file is copied to `prompts/in/seed-response.json` and any stale `Data/seed-data.json` is deleted first so the fresh choice actually takes effect
- Fixed a real bug caught while wiring this in: the `seed` stage's factory was independently re-reading `--spec` from the raw command-line parse result, bypassing the picker entirely - restructured so the resolved spec/output-dir/project-name flow through as one source of truth per invocation

## Affected files
- `src/Genesis.Core/Cli/InteractiveSelection.cs`
- `src/Genesis.Cli/Program.cs`
- `tests/Genesis.Tests/CoreTests.cs`
- `README.md`
