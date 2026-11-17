# Fix: archival copy-not-move (green run no longer deletes canonical fixture)

**Type:** Error fix
**Model:** Sonnet — straightforward fix to a flagged hazard, within the same Sonnet session that found it.

## Summary
A successful `run --all` was silently deleting `fixtures/project-spec.facenote.json` and
`prompts/in/seed-response.json` after archiving them to `prompts/out/schemas/` and
`prompts/out/seeds/`. The archival was implemented as a MOVE (copy then delete), which
destroyed the canonical dev fixture on every green run and required manual restoration from
the archive.

Flagged at the end of Session 21 (changelog 033) as a workflow hazard.

## Root cause
`ArchiveCompletedProject` in `src/Genesis.Cli/Program.cs` called `File.Delete` on the
source after each `File.Copy` — three deletes total (spec, lock, seed-response).

## Fix
Removed all three `File.Delete` calls. Archival is now a pure copy:
- `fixtures/project-spec.facenote.json` — stays in place after a green run
- `fixtures/project-spec.facenote.json.lock.json` — stays in place
- `prompts/in/seed-response.json` — stays in place

The `prompts/out/schemas/` and `prompts/out/seeds/` archive copies are still written and
overwritten on each green run (same "keep the latest" behaviour as before).

## Verification
- `dotnet test` 60/60 (no test changes needed — this is runtime behaviour, not template logic)

## Affected files
- `src/Genesis.Cli/Program.cs` (`ArchiveCompletedProject`)
