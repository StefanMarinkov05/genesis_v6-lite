# Claude Code SessionEnd hook: per-session usage stats to logs/

**Type:** Feature / change
**Model:** Sonnet — mechanical, well-scoped infra hook.

## Summary
Stefan asked for automatic per-session usage logging. Added a Claude Code `SessionEnd` hook that
reads the session transcript on exit and appends one JSON line of usage stats to
`logs/claude-code-usage.jsonl`, reusing this project's existing append-only, best-effort `logs/`
convention (see `logs/README.md`). This is Claude Code harness config + a helper script — it does NOT
touch the dps-nn generator or pipeline.

## Techniques
- **`.claude/hooks/log-usage.py`** — reads the SessionEnd hook payload from stdin (`session_id`,
  `transcript_path`, `cwd`, `reason`), walks the transcript JSONL once, and emits:
  `input_tokens`, `output_tokens`, `cache_read_tokens`, `cache_write_tokens`, `total_tokens`,
  `models`, `assistant_turns`, `cache_hit_rate`, `duration_seconds`, `estimated_cost_usd`,
  `cost_is_complete`. Implementation notes:
  - Token totals come from each `assistant` line's `message.usage` block (the real transcript shape,
    confirmed by inspecting a live transcript first, not guessed). Deduped by `message.id` so streamed
    duplicates aren't double-counted.
  - Cache-creation is split into 5m/1h TTL buckets when the transcript provides the breakdown, because
    they're priced differently (1h > 5m); falls back to 5m when only the total is present.
  - `cache_hit_rate` = cache_read / (input + cache_read + cache_write).
  - `duration_seconds` = last − first transcript timestamp.
  - Cost is priced per-model by family substring (opus/sonnet/haiku) against a list-price table in the
    script; unknown families (e.g. fable) yield `cost_is_complete=false` rather than a wrong number.
  - **Never fails a session:** any parse/IO error is caught, a minimal line (with the error) is still
    written, and the script always `exit(0)`. Same discipline as the CLI's best-effort logging.
  - logs/ dir is resolved from the hook's `cwd` / `CLAUDE_PROJECT_DIR`, falling back to two levels up
    from the script — so it works regardless of the hook process's working directory.
- **`.claude/settings.json`** (new, project-scoped) — registers the hook under `SessionEnd` (no matcher;
  SessionEnd isn't tool-scoped), `shell: bash`, `python "$CLAUDE_PROJECT_DIR/.claude/hooks/log-usage.py"`,
  30s timeout. `.claude/settings.local.json` (personal permissions) left intact.
- **`logs/README.md`** — documented the new file + flagged it as the one hook-written (not CLI-written)
  member of logs/.

## Verification
- Pipe-tested the raw script against THIS session's real transcript (synthesized SessionEnd payload):
  exit 0, produced a well-formed line — e.g. 15,541 input / 82,945 output tokens, cache hit rate 0.9788,
  duration 2251.9s, models [claude-opus-4-8], estimated_cost_usd 30.4981, cost_is_complete true. Numbers
  are self-consistent and in the expected range for an Opus session.
- `.claude/settings.json` validated as well-formed with the SessionEnd command present.
- Test lines cleared from `logs/claude-code-usage.jsonl` afterward so the first persisted entry is a
  genuine session.
- **Not provable in-turn:** SessionEnd fires when the session actually ends, outside this turn, so the
  live hook firing can't be demonstrated mid-session. Because `.claude/settings.json` is a NEW file, the
  settings watcher may not pick it up until `/hooks` is opened once (reloads config) or Claude Code is
  restarted — after that it fires automatically on every session end.

## Affected files
- `.claude/hooks/log-usage.py` (new)
- `.claude/settings.json` (new)
- `logs/README.md`
