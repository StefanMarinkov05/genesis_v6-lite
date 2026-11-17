# Bundle changelog-discipline as a project-level Claude Code skill

**Type:** Feature / change
**Model:** Opus — same migration session.

## Summary
Stefan asked whether skills carry over automatically into Claude Code. They don't - Claude Code reads
from its own `.claude/skills/` (project) or `~/.claude/skills/` (personal) locations, a different
storage location from this chat environment's skills, even though both use the same open SKILL.md
format. Rather than just explain that, bundled the skill directly into the project.

## Techniques
- Copied `changelog-discipline`'s `SKILL.md` into `.claude/skills/changelog-discipline/` inside the
  project itself. Since Claude Code auto-discovers project-scoped skills on open, this means the
  discipline (document every change, check history before proposing fixes) is already active the
  moment Stefan opens this folder in VS Code - no manual setup step required on his end.

## Affected files
- `.claude/skills/changelog-discipline/SKILL.md` (new)
