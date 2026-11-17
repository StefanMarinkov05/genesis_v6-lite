#!/usr/bin/env python3
"""SessionEnd hook: append one JSON line of usage stats per session to
logs/claude-code-usage.jsonl.

WHY this exists: Claude Code shows live token/cost only in the UI; nothing is
persisted per-session for later analysis. This hook reads the session transcript
(the JSONL Claude Code already writes) at SessionEnd and distills it into one
append-only audit line, deliberately mirroring this project's existing logs/
convention (registry.jsonl etc. - see logs/README.md): append-only, best-effort,
never allowed to break the thing it observes.

CONTRACT (Claude Code SessionEnd hook):
  - stdin  : JSON with at least {session_id, transcript_path, cwd, reason}
  - stdout : ignored here (we keep it quiet); side effect is the appended log line
  - exit   : ALWAYS 0. A telemetry logger must never fail a session. On any error
             we still write a minimal line carrying the error, then exit clean.

Token accounting notes:
  - Each `assistant` transcript entry carries a `message.usage` block. We dedupe by
    `message.id` because streamed responses can appear on multiple lines sharing one
    id - counting each id once avoids inflating the totals.
  - "input" here is the true prompt tokens (usage.input_tokens); cache reads/writes
    are tracked separately so the cache hit rate and cost are correct.
  - Cache-creation is split into 5-minute vs 1-hour TTL buckets when the transcript
    provides the breakdown, because they are priced differently (1h costs more).
"""

import sys
import os
import json
from datetime import datetime, timezone

# ---------------------------------------------------------------------------
# Pricing table - USD per 1,000,000 tokens. ESTIMATE ONLY (list prices; ignores
# tier discounts/batch). Matched by substring against the model id so new point
# releases in a family are picked up automatically. Edit here if prices change.
#   in       = prompt input           out    = generated output
#   cw5m/cw1h= cache write 5min/1h    cr     = cache read
# ---------------------------------------------------------------------------
PRICING = {
    "opus":   {"in": 15.0, "out": 75.0, "cw5m": 18.75, "cw1h": 30.0, "cr": 1.50},
    "sonnet": {"in":  3.0, "out": 15.0, "cw5m":  3.75, "cw1h":  6.0, "cr": 0.30},
    "haiku":  {"in":  1.0, "out":  5.0, "cw5m":  1.25, "cw1h":  2.0, "cr": 0.10},
}


def rates_for(model: str):
    """Pick a pricing row by family substring; None if the family is unknown
    (e.g. fable), so we can emit a null cost rather than a wrong one."""
    m = (model or "").lower()
    for family, row in PRICING.items():
        if family in m:
            return family, row
    return None, None


def parse_ts(raw):
    """Transcript timestamps look like 2026-07-10T00:27:10.438Z. Return an aware
    datetime, or None if unparseable (so duration math can skip it safely)."""
    if not raw:
        return None
    try:
        return datetime.fromisoformat(raw.replace("Z", "+00:00"))
    except (ValueError, AttributeError):
        return None


def summarize(transcript_path: str) -> dict:
    """Walk the transcript JSONL once and fold it into a stats dict."""
    totals = {"input": 0, "output": 0, "cache_read": 0, "cw5m": 0, "cw1h": 0}
    models = {}                 # model id -> its own token totals (for per-model cost)
    seen_msg_ids = set()        # dedupe streamed duplicates by message.id
    assistant_turns = 0
    first_ts = last_ts = None

    with open(transcript_path, "r", encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                obj = json.loads(line)
            except json.JSONDecodeError:
                continue  # a partially-flushed line shouldn't sink the whole report

            ts = parse_ts(obj.get("timestamp"))
            if ts:
                first_ts = first_ts or ts
                last_ts = ts

            if obj.get("type") != "assistant":
                continue
            msg = obj.get("message") or {}
            usage = msg.get("usage")
            if not isinstance(usage, dict):
                continue

            # Dedupe: one id counted once. If a message somehow lacks an id, fall
            # back to counting it (better a slight over-count than dropping data).
            mid = msg.get("id")
            if mid is not None:
                if mid in seen_msg_ids:
                    continue
                seen_msg_ids.add(mid)

            assistant_turns += 1
            model = msg.get("model", "unknown")

            inp = int(usage.get("input_tokens", 0) or 0)
            out = int(usage.get("output_tokens", 0) or 0)
            cr = int(usage.get("cache_read_input_tokens", 0) or 0)
            cc_total = int(usage.get("cache_creation_input_tokens", 0) or 0)
            # Prefer the explicit 5m/1h split when present; else treat all cache
            # creation as 5m (the cheaper/default TTL) to avoid over-estimating.
            cc = usage.get("cache_creation") or {}
            cw5m = int(cc.get("ephemeral_5m_input_tokens", 0) or 0)
            cw1h = int(cc.get("ephemeral_1h_input_tokens", 0) or 0)
            if cw5m == 0 and cw1h == 0 and cc_total:
                cw5m = cc_total

            for k, v in (("input", inp), ("output", out), ("cache_read", cr),
                         ("cw5m", cw5m), ("cw1h", cw1h)):
                totals[k] += v
            mt = models.setdefault(model, {"input": 0, "output": 0, "cache_read": 0,
                                           "cw5m": 0, "cw1h": 0})
            for k, v in (("input", inp), ("output", out), ("cache_read", cr),
                         ("cw5m", cw5m), ("cw1h", cw1h)):
                mt[k] += v

    # Cost: price each model's own tokens with its family's rates, then sum.
    cost = 0.0
    priced_all = True
    for model, t in models.items():
        _, r = rates_for(model)
        if r is None:
            priced_all = False
            continue
        cost += (t["input"] * r["in"] + t["output"] * r["out"]
                 + t["cache_read"] * r["cr"] + t["cw5m"] * r["cw5m"]
                 + t["cw1h"] * r["cw1h"]) / 1_000_000

    # Cache hit rate = cached-read input / all input the model was billed to read.
    read_surface = totals["input"] + totals["cache_read"] + totals["cw5m"] + totals["cw1h"]
    cache_hit_rate = round(totals["cache_read"] / read_surface, 4) if read_surface else 0.0

    duration_s = None
    if first_ts and last_ts:
        duration_s = round((last_ts - first_ts).total_seconds(), 1)

    return {
        "input_tokens": totals["input"],
        "output_tokens": totals["output"],
        "cache_read_tokens": totals["cache_read"],
        "cache_write_tokens": totals["cw5m"] + totals["cw1h"],
        "total_tokens": (totals["input"] + totals["output"]
                         + totals["cache_read"] + totals["cw5m"] + totals["cw1h"]),
        "models": sorted(models.keys()),
        "assistant_turns": assistant_turns,
        "cache_hit_rate": cache_hit_rate,
        "duration_seconds": duration_s,
        "estimated_cost_usd": round(cost, 4),
        "cost_is_complete": priced_all,  # false if any model's family had no price row
    }


def resolve_logs_dir(hook_input: dict) -> str:
    """logs/ lives at the project root. Prefer the hook's own cwd/CLAUDE_PROJECT_DIR;
    fall back to two levels up from this script (.claude/hooks/ -> project root)."""
    root = (hook_input.get("cwd")
            or os.environ.get("CLAUDE_PROJECT_DIR")
            or os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
    return os.path.join(root, "logs")


def main():
    # Read the hook payload. Anything malformed still yields a (minimal) log line.
    try:
        raw = sys.stdin.read()
        hook_input = json.loads(raw) if raw.strip() else {}
    except (json.JSONDecodeError, ValueError):
        hook_input = {}

    record = {
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "session_id": hook_input.get("session_id"),
        "reason": hook_input.get("reason"),
    }

    transcript = hook_input.get("transcript_path")
    try:
        if transcript and os.path.isfile(transcript):
            record.update(summarize(transcript))
        else:
            record["error"] = f"transcript not found: {transcript!r}"
    except Exception as exc:  # noqa: BLE001 - telemetry must never raise
        record["error"] = f"{type(exc).__name__}: {exc}"

    # Append one line. Best-effort, exactly like the rest of logs/.
    try:
        logs_dir = resolve_logs_dir(hook_input)
        os.makedirs(logs_dir, exist_ok=True)
        with open(os.path.join(logs_dir, "claude-code-usage.jsonl"), "a", encoding="utf-8") as fh:
            fh.write(json.dumps(record, ensure_ascii=False) + "\n")
    except OSError:
        pass  # never fail the session over a log write

    sys.exit(0)


if __name__ == "__main__":
    main()
