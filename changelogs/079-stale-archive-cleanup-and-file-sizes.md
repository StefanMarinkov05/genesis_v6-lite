# 079 — Stale spec archive removed; file sizes shown everywhere in the GUI

**Type:** Feature / change (two small, unrelated requests handled together)

## Part 1: removed a stale, misleading spec archive

Stefan asked whether I'd updated the specs/seed and, if so, to remove old versions so he wouldn't
pick the wrong one. Investigated rather than guessed: `git log -- fixtures/ seeds/` confirms
neither was touched in this session (last real edit was commit `1f1272f`, well before this work).

What *is* real: `prompts/out/schemas/facenote-schema.json` (+ `.lock.json`) was confirmed
byte-identical to `fixtures/project-spec.facenote-en.json`, not the BG default
`fixtures/project-spec.facenote.json` its filename implies. Both fixtures share
`meta.projectName: "FaceNote"`, and `ArchiveCompletedProject` (`Program.cs`) keys the archive purely
by that name — so the EN fixture's last successful run silently overwrote the BG run's archive
entry. This is exactly the kind of "old version lying around, might pick the wrong one" risk
flagged: the GUI's spec picker lists both `fixtures/` and this archive as separate entries, and the
archived one would silently hand back EN content under a BG-implying name.

Checked before deleting anything: `prompts/out/seeds/facenote-seed.json` is **not** part of the
same collision — its content is genuinely Bulgarian (2872 Cyrillic characters found directly in the
file), confirmed rather than inferred from unreliable file-mtime forensics. Only the mismatched
schema archive pair was removed; the correct seed archive was left alone.
`fixtures/seed-response.facenote.json` looked like a possible third duplicate (different byte size
from the seed archive) but is a real, actively-used test fixture
(`tests/Genesis.Tests/CoreTests.cs` schema-validates it) — left untouched.

Not fixed (flagged only): the underlying archive-collision behavior — any two specs sharing a
`projectName` will keep silently clobbering each other's archive. Worth a naming fix
(e.g. keying by source file stem too) if it recurs; out of scope for a cleanup request.

## Part 2: file sizes shown everywhere in the GUI

Added `size: number` (exact bytes, main-process `statSync`) to the shared `FileHandle` type so a
fixture and a stale/edited copy of it are distinguishable at a glance without opening either file.
Shown as an exact byte count (not rounded to KB/MB — the stated goal is spotting identical vs.
different files, which a rounded size would hide) in every place a `FileHandle` reaches the
renderer: the spec/seed dropdown labels, the "well" display for the currently-selected file, the
upload-success toast, and the build-started toast.

## Affected files

- Removed: `prompts/out/schemas/facenote-schema.json`, `prompts/out/schemas/facenote-schema.lock.json`
- `gui/src/shared/ipc-types.ts` (`FileHandle.size`)
- `gui/src/main/files.ts` (populates `size` in `listSpecs`/`listSeeds`/upload)
- `gui/src/renderer/main.ts` (`formatBytes`, shown in `optionLabel`, the well displays, and both
  relevant toasts)
