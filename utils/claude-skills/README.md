# dps-nn Claude Skills

Two portable Claude Skills supporting the manual spec/seed authoring workflow (Stefan pastes a
spec into a Claude chat and asks for seed data directly, instead of running the pipeline's own
LLM exchange). Each is a self-contained folder with one `SKILL.md` — the same format as every
skill under `~/.claude/skills/`, so they can be copied there (or into any other Claude client's
skills folder) with no changes.

- **`dps-nn-spec-authoring/`** — designs/extends a `project-spec.json` from raw notes, mirroring
  the pipeline's own `spec-build` stage, `docs/reference/spec-format.md`, and real generator
  conventions (isSpecification/isLookup admin-page consolidation, removedActions/ownership/
  redirects, images-block vs plain ImagePath, the DTO-is-untested caveat). **Conversational by
  default** — discusses the domain and flags judgment calls before emitting the JSON; terse
  one-shot output is an explicit opt-in ("just give me the spec").
- **`dps-nn-seed-authoring/`** — generates a `seed-response.json` from a given spec, faithfully
  reproducing the pipeline's own `seed` stage prompt (ADR-005's manual-mode file contract) so the
  output matches what the automatic LLM path would have produced. **Stays terse by default** —
  always asks the admin/non-admin seed-user count first (never assumes), asks about role-name
  language only if the spec's roles look Latin-script, then emits JSON directly.

## To use them elsewhere

Copy the whole folder (not just `SKILL.md`) into the target location, e.g.:

```
cp -r dps-nn-spec-authoring ~/.claude/skills/
cp -r dps-nn-seed-authoring ~/.claude/skills/
```

Both stay useful without any project files attached — a bare spec/seed conversation in claude.ai
works fine. If `spec.schema.json` is attached, the spec-authoring skill treats it as authoritative
over its own summary.
