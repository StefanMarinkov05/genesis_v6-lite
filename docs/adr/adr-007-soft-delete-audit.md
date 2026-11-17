<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# ADR-007: Soft delete via global query filters + audit base entity

**Status**: accepted

**Context**: FaceNote-class apps require admin soft delete (relational data preserved, hidden from
public views) and universal CreatedAt/UpdatedAt for "date added" sorting.

**Decision**:
- `conventions.deleteBehavior: "soft"` (or per-model `softDelete: true`) adds IsDeleted+DeletedAt to
  the model, an EF `HasQueryFilter(x => !x.IsDeleted)` in the context, and a source transform that
  rewrites scaffolded DeleteConfirmed bodies from `Remove()` to flag-set.
- `conventions.timestamps` emits an abstract `AuditableEntity` base (all models inherit) and a
  `SaveChangesAsync` override that maintains UpdatedAt automatically.
- **User soft-delete does NOT auto-cascade** to their content via query filters: EF global filters
  cannot join to AspNetUsers cheaply. Content of soft-deleted users is hidden at the controller
  level where a spec explicitly demands it; the default is content stays visible. Documented
  trade-off, revisit if a project requires strict cascade.

**Consequences**: sorting by CreatedAt is universally available; admins never lose relational data;
templates never touch timestamps manually.
