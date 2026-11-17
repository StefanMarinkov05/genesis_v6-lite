# Changelogs

One `.md` file per distinct request/change, numbered in chronological order so they sort naturally
in any file browser. Two formats, depending on what the change was:

**Error fixes** use: Where / How it happened / Why / What it affects / How to reproduce / How to fix.

**Features / aesthetic / refactor changes** use: Summary / Techniques / Affected files - kept brief,
no need for the full error template when nothing was actually broken.

Both templates carry a `**Model:**` line right after `**Type:**`, e.g.:

```
**Type:** Feature / change
**Model:** Sonnet — standard, well-scoped feature work following an existing pattern.
```

Record which Claude model authored the change and *why that model*, not just which one - the reason
is what makes the line useful later (routing rubric from the `coding-standards` skill: Opus for
cross-cutting/architectural work or root-cause debugging with real ambiguity; Sonnet for standard
multi-file feature work following an established pattern; Haiku for mechanical, single-file,
pattern-copy tasks). If a session mixes models, or the model was explicitly requested/switched
mid-conversation, say so. Backfilled across 001-052 in changelog 053 (best-effort reconstruction from
`plan/state.md` session headers + the v6 task-breakdown's own routing table where the model wasn't
directly recorded).

Add a new file here for every future request that changes the project, following whichever template
fits. Keep the numbering sequential (`017-...`, `018-...`, ...).
