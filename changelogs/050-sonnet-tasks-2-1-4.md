## 050 — Sonnet batch: Tasks #2 (DB lifecycle) + #1 (smart resume) + #4 (rename/move)

**Type:** feature, chore
**Model:** Sonnet — explicit in the session/commit label - standard feature work plus a repo-wide but mechanical rename.
**Date:** 2026-07-11
**Tasks completed:** GitHub issues #2, #1, #4

---

### Task #2: DB lifecycle — spec-hash-based database reuse

**What changed:** `src/Genesis.Core/Stages/BuildStages.cs` (ScaffoldStage), `src/Genesis.Core/Pipeline/Pipeline.cs`

- New `DbLifecycleRecord` class in `Pipeline.cs` persists `{specHash, dbName, migrationsApplied}` to
  `logs/<Project>/db.json`.
- `ScaffoldStage` computes a deterministic SHA-256 spec hash (models + schema + row counts, all
  OrderBy'd to be stable). If the hash matches the stored record, the existing LocalDB database name
  is reused — no new timestamp name, no re-seeding needed.
- When the spec changes, the OLD database is dropped via `sqlcmd` best-effort before creating the new one.
  Prevents old `FaceNote_20260710...` databases piling up across runs.

---

### Task #1: Smart resume — skip migrations when already applied

**What changed:** `src/Genesis.Core/Stages/SeedStage.cs`

- Before running EF migrations, SeedStage reads `db.json` and checks `MigrationsApplied == true`
  AND `DbName` matches the current `appsettings.json` connection string.
- If both match → entire migration block (pre-build + `ef migrations add` + `ef database update`) is
  skipped with an info log. Saves ~2–3 minutes on pipeline re-runs that fail after migrations.
- After successful `ef database update`, `MigrationsApplied = true` is written back to `db.json`.
- New `GetCurrentDbName()` helper reads the DB name from `appsettings.json` connection string.

---

### Task #4: Rename to genesis_v6 — reverse ADR-009

**What changed:** every .cs source file, all csproj files, dps-nn.slnx, 1-build-and-check.bat, plan/decisions.md

- **Namespaces renamed:** `Genesis.Core.*` → `GenesisV6.Core.*` across all 14 source files.
- **csproj files renamed:**
  - `Genesis.Core.csproj` → `GenesisV6.Core.csproj`
  - `Genesis.Cli.csproj` → `GenesisV6.Cli.csproj`
  - `Genesis.Tests.csproj` → `GenesisV6.Tests.csproj`
- **InternalsVisibleTo** updated: `Genesis.Tests` → `GenesisV6.Tests`
- **ProjectReferences** updated in Cli + Tests csproj files
- **dps-nn.slnx** updated with new csproj paths
- **1-build-and-check.bat** updated with new csproj names
- **VERSION** file added: `6.0.0`
- **GitHub repo** renamed: `genesis-v5_26` → `genesis_v6`
- **Git remote** updated to `https://github.com/StefanMarinkov05/genesis_v6.git`
- **decisions.md** records ADR-009 reversal
- Working directory move to `D:/Projects/ДПС-НН`: copy the folder manually (D: drive permission-denied from tool process); git history + remote are already correct.

### Verified

`dotnet build dps-nn.slnx -c Release` → `Build succeeded. 0 Warning(s) 0 Error(s)`
Outputs: `GenesisV6.Core.dll`, `GenesisV6.Cli.dll`, `GenesisV6.Tests.dll`
`dotnet test` → `Passed! 79/79`
