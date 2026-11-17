# Added CLAUDE.md for the move to Claude Code / VS Code

**Type:** Feature / change
**Model:** Opus — final session before the migration to Claude Code.

## Summary
Stefan is moving from the claude.ai-chat zip/download/paste-error workflow to Claude Code in VS Code,
working directly on a permanent local copy instead of re-extracting a fresh zip every session. Added a
`CLAUDE.md` at the project root so a brand-new Claude Code session in that folder starts oriented
automatically instead of blind.

## Techniques
- `CLAUDE.md` points at `plan/state.md`/`plan/decisions.md` as required reading before any new work,
  restates the `changelog-discipline` skill's rules inline (so they apply even in an environment where
  that skill file isn't loaded), and points at `plan/v6-task-breakdown.md` as the current priority queue.
- Called out the one meaningful environment difference explicitly: prior sessions had no compiler
  available and verified changes by hand (bracket-delta counting, Scriban balance checks); Claude Code
  has a real `dotnet build`/`dotnet test` available and should just use it instead of reasoning blind.

## Affected files
- `CLAUDE.md` (new)
