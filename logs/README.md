# logs/

Created and maintained automatically by the CLI - nothing to set up by hand. (One exception:
`claude-code-usage.jsonl` below is written by a Claude Code SessionEnd hook, not the CLI.)

- **`registry.jsonl`** - full history, one JSON line per invocation: timestamp, project name, the
  spec file path used, the seed-response file present at the time (if any), and the output directory.
  Append-only, never rewritten - a complete audit trail of every run.
- **`registry.json`** - the same information, but keyed by project name and only the LATEST run per
  project, for a quick "what spec/seed is this project currently on" lookup without grepping the jsonl.
- **`<ProjectName>/actions.log.jsonl`** - a per-project, leveled action log (`info`/`debug`/`error`)
  capturing every stage's own logging (`ctx.Info(...)`/`ctx.Debug(...)`/`ctx.Error(...)`), for debugging
  a specific project's generation history in isolation. A stage failure's full exception detail (not
  just the message) lands here at `error` level. Separate from that project's own
  `out/<Project>/dps-nn.log.jsonl`, which only records stage start/end/error timings.
- **`global-errors.jsonl`** - catch-all for anything that fails BEFORE a project context exists (spec
  resolution itself, a missing `templates/` folder) - there's no project name to file those under, so
  they land here instead of being lost.

- **`claude-code-usage.jsonl`** - one JSON line per Claude Code *session* (not per pipeline run):
  input/output tokens, cache read/write tokens, cache hit rate, model(s), assistant turn count,
  wall-clock duration, and an estimated USD cost. Written by the `SessionEnd` hook
  (`.claude/hooks/log-usage.py`, registered in `.claude/settings.json`), which reads the session
  transcript on exit. Same append-only, best-effort spirit as the rest of this folder - the hook
  always exits 0 and never fails a session over a log write. Cost is a list-price ESTIMATE (see the
  pricing table in the script); `cost_is_complete=false` flags a model whose price wasn't known.

All writes here are best-effort - a logging failure never breaks the pipeline itself.
