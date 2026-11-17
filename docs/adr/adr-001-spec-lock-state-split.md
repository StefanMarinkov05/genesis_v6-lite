<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# ADR-001: Three-file split - spec / lock / state

**Status**: accepted

**Context**: one JSON file serving as user input, expansion output, and pipeline state creates write-races and destroys reproducibility.

**Decision**: `project-spec.json` is immutable user input. `spec expand` writes `project-spec.lock.json` (feature fragments merged, $variation resolved, seed passwords injected, `variationProfileResolved` echoed) - the only spec later stages read and the manifest `verify` checks. Pipeline runtime state lives in `out/<Project>/plan/state.json`.

**Consequences**: re-running expand is safe and diffable; verify has a stable manifest; state resets never touch the spec.
