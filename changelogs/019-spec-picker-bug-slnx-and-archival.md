# Fix real spec-picker bug + .slnx support + prompts/out/schemas & seeds archival

**Type:** Error fix + Feature / change
**Model:** Opus — real shipped-bug fix requiring reasoning about its interaction with an existing feature's semantics.

## Error: spec picker crashed with exactly one candidate spec

### Where
`src/Genesis.Cli/Program.cs`, `ResolveSpecInteractively`.

### How it happened
The function's own comment said "0 or 1 candidate: no choice to make, keep current behavior" - but
the code returned the bare, nonexistent default `project-spec.json` in BOTH the 0-candidate case AND
the 1-candidate case. With Stefan's actual repo (exactly one real spec, `fixtures/project-spec.facenote.json`),
that single valid candidate was found and then thrown away in favor of a file that doesn't exist.

### Why
The intent ("if there's nothing to choose between, don't bother the person") was right; the
implementation conflated "nothing found" with "found exactly one" - both hit the same early return
instead of being handled as two different cases.

### What it affects
Every invocation of `2-generate.bat` (or `dotnet run ... -- run --all` without `--spec`) on a repo
with exactly one spec file - which is the normal, common case, not an edge case.

### How to reproduce
Have exactly one `project-spec*.json` under `fixtures/`, run `dotnet run --project src\Genesis.Cli --
run --all --out out --resume` with no `--spec`. Fails with
`FileNotFoundException: Could not find file '...\project-spec.json'`.

### How to fix
Split the early return into three cases: 0 candidates -> keep the original fallback behavior
unchanged; exactly 1 -> auto-select it (the actual fix); 2+ -> show the menu (unchanged). Deliberately
did NOT apply the same "auto-select on 1" change to the seed picker - reasoned through the interaction
before shipping it and realized that would force a delete-and-recopy of `Data/seed-data.json` on every
single run whenever exactly one file sits in `seeds/`, undoing the "reuse existing valid seed data"
behavior from an earlier session for the common single-seed case. The seed picker only intervenes on a
genuine 2+ choice, unchanged.

## Feature: .slnx support + prompts/out/schemas & prompts/out/seeds archival

### Summary
The .NET 10 SDK's `dotnet new sln` now creates a `.slnx` file by default, not the classic `.sln` - both
one-click scripts' existence checks were only looking for `.sln`, so they'd try to re-bootstrap (and
likely fail) on every run. Also added permanent per-project history: `prompts/out/schemas/` and
`prompts/out/seeds/`, populated automatically when a project finishes generating, plus both interactive
pickers now search these folders too.

### Techniques
- Both scripts' bootstrap checks widened to `if not exist X.sln if not exist X.slnx` (chained `if`s -
  the block only runs when BOTH are absent).
- New `ArchiveCompletedProject` in `Program.cs`, called right after a successful `run --all`: moves
  (copy + delete, not just copy - "this is history") the spec file, its `.lock.json` sibling if present,
  and `prompts/in/seed-response.json` into `prompts/out/schemas/<project>-schema.json` (+
  `-schema.lock.json`) and `prompts/out/seeds/<project>-seed.json`. Project name is lowercased to match
  the exact naming convention Stefan asked for (`facenote-seed.json`), not the spec's as-declared casing.
  Overwrites any previous archive entry for the same project (same convention as `logs/registry.json`'s
  "keep the latest" snapshot). Best-effort - an archival failure is reported but never masks a
  successful run.
- Both `ResolveSpecInteractively` and `ResolveSeedInteractively`'s search paths extended to include
  `prompts/out/schemas/` and `prompts/out/seeds/` respectively, so an archived spec/seed stays
  selectable in a later run instead of disappearing once moved.
- One-time action (not code): copied the current FaceNote spec and the already-extended
  `seed-response.json` into the new folders as `facenote-schema.json` / `facenote-seed.json`, so they're
  immediately discoverable without needing a run first. Left the original `fixtures/project-spec.facenote.json`
  in place too (the request was to put a copy there "as well," not remove the original) - this means the
  next generate run will see 2 spec candidates and show a menu, which is expected, not a bug.

### Affected files
- `src/Genesis.Cli/Program.cs`
- `1-build-and-check.bat`, `2-generate.bat`
- `prompts/out/schemas/facenote-schema.json` (new), `prompts/out/seeds/facenote-seed.json` (new)
- `README.md`
