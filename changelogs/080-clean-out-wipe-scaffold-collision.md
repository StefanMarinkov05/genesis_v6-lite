# 080 — Scaffold MSB1011 collision; full out/ wipe for a clean regenerate

**Type:** Error fix + Feature/change (both — a real bug diagnosed, plus the clean-slate action requested)

## Where
`out/FaceNote/`, `out/FaceNoteEN/`, and stray files at `out/` root itself.

## How it happened
Stefan's `scaffold` stage failed: `MSBUILD : error MSB1011: Specify which project or solution file
to use because this folder contains more than one project or solution file.` The run's own log line
("FaceNote_20260711193819 — използва се повторно, спецификацията не е променена" / "reused, the
spec hasn't changed") shows the pipeline reused an existing output directory rather than starting
clean.

## Why
`out/FaceNote/` directly contained **both** a freshly re-scaffolded `FaceNote.csproj` (timestamp
matching this run) **and** a stale `FaceNoteSolution.slnx` left over from an earlier, apparently
interrupted run at ~00:32 the same day. `dotnet build` with no explicit target refuses to guess
between two valid project/solution files in the same directory — hence MSB1011.
`ScaffoldStage`'s idempotency check ("presence of the `.csproj` short-circuits `dotnet new`") only
verifies the `.csproj` exists before skipping re-scaffold; it does not verify the directory is
otherwise internally consistent (no leftover `.slnx` from a prior broken attempt). **This is a real
gap, flagged but not fixed here** — the requested action was a clean wipe, not a scaffold-idempotency
hardening pass. Worth revisiting if this recurs: `ScaffoldStage` could additionally check for
exactly one solution file before trusting the "already scaffolded" fast path.

Separately: Stefan asked why he couldn't copy the error from his terminal. `logs/last-generate.log`
(the fixed-path capture added in changelog 078) was still just 18 bytes of stale test content while
`logs/global-errors.jsonl` had a fresh multi-KB write at the same timestamp — meaning **this run did
not go through `2-generate.bat`**, so the clipboard-copy-on-failure wrapper never engaged. The
capture mechanism only wraps the two `.bat` entry points; a direct CLI invocation (or any other
launch path) bypasses it entirely. Told Stefan to use `2-generate.bat` going forward for that to work.

## What it affects
Any resumed run whose output directory holds a leftover solution/project file from a prior
interrupted attempt.

## How to reproduce
Interrupt a `run --all` mid-scaffold (or otherwise leave a stale `.slnx` in a project's `out/`
directory), then re-run against the same output directory with an unrelated `.slnx` also present at
the project root.

## How to fix (this instance)
Per explicit request, wiped generated output only — spec/seed sources untouched throughout:
- Removed `out/FaceNote/`, `out/FaceNoteEN/` (both generated project trees, including their
  `logs/<Project>/actions.log.jsonl` + `db.json` equivalents, which live inside `out/`).
- Removed `logs/FaceNote/` (the CLI's separate, cwd-rooted per-project action log — NOT inside
  `out/`, per `StageContext.ActionLogPath`).
- Removed two stray files sitting at `out/` root itself (`FaceNoteSolution.slnx`, `global.json`) —
  leftover artifacts outside any project subfolder, from the same broken state.
- **Confirmed untouched** (verified directly, not assumed): `fixtures/*.json` (both spec variants +
  their locks + the small test-fixture seed), `prompts/in/seed-response.json` (the active seed
  exchange the next run will read), `prompts/out/seeds/facenote-seed.json` (the archived real seed).

`out/` is now completely empty — the next `run --all` (via `2-generate.bat`, so this session's error
capture actually engages if it fails again) starts genuinely from stage 1.
