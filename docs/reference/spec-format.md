<!-- last-verified: e95fedf -->
# Spec format — project-spec.json concepts

**Diataxis type:** reference (concepts companion). **Source of truth for shape:**
[`spec.schema.json`](../../spec.schema.json) — JSON Schema, `additionalProperties: false`
everywhere, so an unknown key is a hard validation error, never silently ignored. This doc explains
the concept groups and the non-obvious resolution mechanisms; it does **not** enumerate every field
(that would duplicate and rot against the schema — see `plan/task-a-doc-architecture.md` decision
D3). When schema and prose ever disagree, the schema wins.

## The three files, and which one you write

Per ADR-001, a spec passes through three files with three different owners:

- **`project-spec.json`** — the one you write. Immutable once the pipeline starts.
- **`project-spec.lock.json`** — written by the `spec-expand` stage. Every stage from `scaffold`
  onward reads *only* this file. Feature fragments are merged in, `$variation:*` placeholders are
  resolved, seed-user passwords are injected.
- **`plan/state.json`** — pipeline run state (completed stages). Never part of the spec itself.

## Concept groups

- **`meta`** — project identity (`projectName`, `solutionName`, `rootNamespace`), `language`
  (`"bg"` drives the whole Bulgarian-UI requirement and the verify English-leak gate;
  anything else opts out of both), `database`, `style` (Bootstrap theme + design-overlay preset,
  ADR-008), `routing`. `unsupportedRequests` (`[{ request, reason }]`, optional) is written by
  agent mode (ADR-013) for asks the generator cannot express; the pipeline ignores it.
- **`conventions`** — cross-cutting toggles resolved once for the whole project: `timestamps`
  (emits the `AuditableEntity` base, ADR-007) and `deleteBehavior` (`"soft"` wires the EF global
  query filter, also ADR-007).
- **`variationProfile` / `variationProfileResolved`** — the deterministic-variation input (a seed +
  optional axis pins) and its resolved output (echoed back for auditability). See ADR-004; you
  rarely hand-write the resolved side.
- **`models`** — the domain entities. Each property uses the shared `property` schema definition
  (`name`, `type`, `key`/`foreignKey`/`nullable` flags, `validations`, `enumValues`). **Every
  non-key/FK property needs a `displayName`** — this is the mechanical enforcement of the
  Bulgarian-UI hard requirement (CLAUDE.md). `relations` describe FK/navigation shape; `images`
  wires a model into the central `ImageAsset`/`ImageService` system.
- **`dtos`** — view-model shapes composed from one or more models' properties, for pages that
  don't want a raw entity.
- **`identity`** — Identity scaffold config: `modules` (which Identity pages to generate),
  `localization` (ADR-002/002b), `applicationUser` (extra properties + its own controller, with
  `writtenFromScratch` server-side rules like `AdminCannotDeleteSelf`), `errorDescriber`, `roles`,
  and `seedUsers` (email + role; **not** a password — see below).
- **`areas`** — MVC areas mapped to roles (e.g. an Admin area, a Client area).
- **`controllers`** — `generatedBy` picks the provenance (`scaffold` = codegen + deviations,
  ADR-006; `template` = Scriban-emitted; or the hand-written Identity/ApplicationUser controller).
  `removedActions`, `redirects`, `sorting`, `pagination`, `ownership` are declarative deviations
  applied on top of whichever provenance generated the base controller.
- **`views`** — `layout` (shared chrome) and `pages` (`kind` distinguishes e.g. `cardGrid` from an
  admin table; `card`/`search`/`rowButtons` configure page-specific behavior).
- **`features`** — optional, reusable fragments (cart, posts, catalogues, orders, rating,
  follows...) toggled with `enabled: true`. See "Feature expansion" below.
- **`seeding`** — how the `seed` stage generates rows (LLM vs manual), image sourcing, and the FK
  strategy (always symbolic refs — ADR-003).
- **`outputReport`** — which `codePointer`s the `report` stage resolves into
  `wwwroot/output/project-summary.json` for the downstream documentation tooling.

## Non-obvious mechanisms

### `$variation:axisName` placeholders

Any string value anywhere in the spec may contain `$variation:axisName` (e.g.
`$variation:adminRoleName`). `spec-expand` resolves every occurrence to that project's
deterministically-derived value for that axis (ADR-004) — the same mechanism the fixture uses for
role names (`fixtures/project-spec.facenote.json`'s `seedUsers[].role`). Resolution happens exactly
once, at expand time; nothing downstream re-interprets the placeholder syntax.

### Feature expansion (`$primaryModel`)

An enabled entry under `features` (`{"enabled": true}`, optionally with a `model` override) merges
a whole fragment of models/controllers/views from `feature-templates/<name>.json` into the spec at
expand time. A fragment's relations may reference the literal token `"$primaryModel"` instead of a
hard-coded model name (e.g. the `catalogues` feature joins to whatever the project's "main" model
is — `Product` in FaceNote, `Destination` in a travel spec). Resolution order: an explicit
`cfg.model` on the feature wins; otherwise it falls back to the first model with `images`
configured. **This fallback must stay in sync with `EmitControllersViewsStage.FeaturePrimaryModel`**
— a feature enabled with no explicit `model` and no image-bearing model in the spec leaves the
literal string `"$primaryModel"` in a relation target, which fails semantic validation downstream.

### `seedUsers` has no password field

`identity.seedUsers.users[]` carries `email` + `role` only — deliberately no `password` field in
the spec itself (see `spec.schema.json`'s `seedUsers.users` item shape). Seed-user passwords are
**derived**, not authored: `spec-expand` resolves them via the same deterministic-variation
mechanism as everything else (`ctx.Variation.SeedPassword(email)`), so they're reproducible across
re-runs of the same spec without ever being hand-written or checked in.

## Where to look next

- Field-by-field shape, validation rules, enums: `spec.schema.json` directly.
- Why the three-file split, why variation is deterministic, why seeding uses symbolic refs:
  `docs/explanation/architecture-decisions.md` (ADR-001, ADR-004, ADR-003).
- The Bulgarian-UI `displayName` requirement's enforcement point: the `verify` stage's English-leak
  scan, `docs/reference/control-flow.md`.
