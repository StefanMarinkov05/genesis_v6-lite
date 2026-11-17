## 049 — Haiku batch: Tasks #3 (log hygiene) + #6 (access banner)

**Type:** enhancement, fix
**Model:** Haiku — explicit in the session/commit label - mechanical, well-scoped tasks.
**Date:** 2026-07-11
**Tasks completed:** GitHub issue #3 (log hygiene), GitHub issue #6 (access banner)

### Task #3: Log hygiene — actions.log.jsonl resets each build

**What changed:**
- At the start of each `run --all`, `logs/<ProjectName>/actions.log.jsonl` is deleted before the pipeline starts
- Old log entries no longer accumulate across runs
- Each run has a clean, isolated log file for easy inspection

**Where:** `src/Genesis.Cli/Program.cs` (run command handler), lines 356-363

### Task #6: Access banner — print credentials + URL

**What changed:**
- `PrintCompletionBanner()` now displays a Unicode box with:
  - Project name (centered)
  - Application URL (from launchSettings.json)
  - All seed users with role, email, and password (formatted as "email / password")
  - Database name
  - Start command
- Formatted with Unicode box drawing (╔═╗║╚═╝) for visual clarity
- Already sourced credentials from seed data; no changes to the data path

**Example output:**
```
╔════════════════════════════════════════════════════════╗
║  FaceNote                                              ║
║  🔗 http://localhost:5000                              ║
╠════════════════════════════════════════════════════════╣
║  Admin              admin@facenote.bg / Admin1!        ║
║  User               user@facenote.bg / Test123!        ║
╚════════════════════════════════════════════════════════╝
Database: FaceNote_20260711_130500
Start: cd c:\out\FaceNote && dotnet run
```

**Where:** `src/Genesis.Cli/Program.cs` (PrintCompletionBanner function), lines 376-414

### Verified

Both builds succeeded locally (no syntax/compilation errors). Changelog 049 marks completion of the Haiku batch.
