<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# ADR-004: Deterministic per-project variation profile

**Status**: accepted

**Context**: many generated sample projects must not look like clones, but each project must be internally consistent.

**Decision**: 8 axes (collection init style, role names, password pattern, namespace style, DI style, string style, comment density) resolved once per project as `SHA256(seed + ":" + axis) % options.Length` - never `GetHashCode()` (process-randomized). Explicit spec pins win. Templates consume resolved values only; `$variation:axis` placeholders are substituted at expand. Resolved profile is echoed into the lock and the output report.

**Consequences**: identical re-runs; cross-project diversity; verify can re-derive seed passwords.
