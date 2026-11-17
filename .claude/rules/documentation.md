<!-- .claude/rules/documentation.md - auto-loaded every session. Keep lean. -->
# Documentation rules

- **Single source of truth.** State each fact in exactly one doc. Cross-reference; never duplicate.
- **Same-commit rule.** Change a doc in the same commit as the code it describes. Changing control flow, the pipeline, or the spec schema without touching the matching doc is a review smell.
- **Staleness stamp.** Every doc header carries `last-verified: <commit-sha>`. Update it when you verify the doc against the code.
- **Generate the generatable.** File maps, stage lists, API signatures are generated from code/spec, never hand-maintained. `docs/reference/file-map.md` is generated from per-file header comments - do not hand-edit it.
- **Diagrams are Mermaid source**, never flat images.
- **CLAUDE.md is a router**, not a container. Do not grow it past ~150 lines; push detail into `docs/`.
- **One source, two audiences.** Write the *why* once in prose for both humans and agents. Do not maintain parallel human/AI doc sets.
- **Detail loads on demand.** Do not move large reference or explanation content into `CLAUDE.md` or `.claude/rules/`; those are auto-loaded and tax every session.
