# 077 — Portable spec/seed-authoring Claude Skills for manual seeding

**Type:** Feature / change
**Model:** Sonnet — grounding two skills faithfully against existing, already-implemented pipeline
prompts (SeedStage's real prompt text, SpecBuildStage's) rather than inventing new behavior.

## Summary

Stefan is switching the seeding workflow: instead of running the pipeline's automatic LLM exchange
(or its planned in-app "Claude" button), he'll paste/attach a spec directly into any Claude chat
and ask for seed data, then feed the result back into the pipeline through an existing entry point
(GUI upload, or `seeds/` for the CLI picker). Wrote two portable Claude Skills supporting this,
under `utils/claude-skills/` (deliberately outside `.claude/skills/` — Stefan wants to manage
placement himself and also copy them into his personal `~/.claude/skills/`).

- **`dps-nn-spec-authoring`** — drafts/extends a `project-spec.json`, grounded in the real
  `spec.schema.json` (verified top-level `required: [specVersion, meta, models]`, the
  `controllers.generatedBy` enum `scaffold|template|writtenFromScratch`, the `views.pages.kind`
  enum `landing|contactFormStatic|cardGrid|adminTable`) and the concept groups already documented
  in `docs/reference/spec-format.md`.
- **`dps-nn-seed-authoring`** — generates a `seed-response.json`, faithfully reproducing
  `SeedStage`'s own real prompt text (the 8 numbered rules — language, plausibility, variety,
  `seedHint`, `refKey`/`parentRef_{Target}`/`imageKeyword`, ISO dates, `$seedUser:N` placeholders,
  scenario coverage) and `BuildSeedSchema`'s exact output shape, so a chat-generated response is
  shape-identical to what the pipeline's own automatic LLM call would produce.

Both are self-contained (work in a bare chat with no file access) but treat an attached
`spec.schema.json` as authoritative over their own summary when present.

## Techniques

- Read the actual prompt strings and schema-builder code (`SeedStage.RunAsync`'s LLM instructions,
  `BuildSeedSchema`, `SpecBuildStage.RunAsync`) rather than re-deriving the contract from
  documentation — the skills mirror the pipeline's real behavior, not an approximation of it.
- Matched the existing project skills' `SKILL.md` frontmatter/structure convention
  (`.claude/skills/changelog-discipline/SKILL.md`) for consistency with the rest of the skill set.
- Added `utils/claude-skills/README.md` with copy-to-`~/.claude/skills` instructions.

## Also this session: fixed `npm run dev` (see changelog 076)

Two real, unrelated bugs blocked the GUI from launching at all: a partial `extract-zip` failure
(likely an AV write-lock race) had left `node_modules/electron/dist` with only one file, and
`ELECTRON_RUN_AS_NODE=1` was leaking into the shell from a parent Electron process, which
`electron-vite`'s spawn call inherited unmodified. Full detail in 076; `tasks.md` rewritten per
Stefan's "keep only undone items" instruction.

## Affected files

- New `utils/claude-skills/{README.md, dps-nn-spec-authoring/SKILL.md, dps-nn-seed-authoring/SKILL.md}`.
- `tasks.md` — rewritten to keep only pending items; the seeding-workflow change noted.
