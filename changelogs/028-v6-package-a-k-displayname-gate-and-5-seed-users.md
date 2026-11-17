# v6 Package A + K: displayName hard gate, localized sort labels, 5 seed users

**Type:** Feature / change
**Model:** Sonnet — standard feature work following an existing warning-to-gate pattern; the first Claude-Code session, per the v6 breakdown's own Sonnet routing for tasks 1/4.

## Summary
First v6 work packages per `plan/v6-task-breakdown.md`, built in the recommended dependency-first order
(K + A are the foundation everything else displays on). Two threads:

- **Package A (localization)** — task 1: the missing-`displayName` check was a soft warning (`[warn]`
  prefix, non-fatal); Bulgarian-only UI is a hard product requirement, so it is now a **hard gate** that
  fails generation. Task 24b: the client card-grid sort dropdown rendered raw English property names as
  option labels; it now renders each field's Bulgarian `displayName`. (Task 3a — Identity `Account/Manage/*`
  localization — was deliberately deferred to its own focused session; it's the historically-difficult
  scaffolded-Identity item and shouldn't destabilize the foundation work.)
- **Package K (seed data & schema)** — task 4: expanded `identity.seedUsers` from 2 → 5 (1 admin, 4
  regular) and regenerated the full 13-model canned seed so ownership is distributed across all five users,
  with row counts satisfying every model's minimum (including the `Catalogue` coupling below).

## Techniques
- **`src/Genesis.Core/Spec/SpecLoader.cs`** — the displayName check in `SemanticCheck` dropped its `[warn]`
  prefix, so it now flows into `semanticErrors` and throws `SpecValidationException` instead of just printing
  to stderr. Added `Computed is null` to the predicate: computed props are read-only derived values whose
  `[Display(Name)]` is already optional in `model.sbn` (`{{~ if prop.display_name ~}}`), so they shouldn't be
  force-gated. The check runs on BOTH the raw spec (`SpecStages.cs:64`) and the fully-expanded lock
  (`SpecStages.cs:136`), so feature-injected models (Post/Order/Review/…) are covered too — verified by
  running `spec expand` on the fixture, which wrote + validated the lock with zero displayName errors.
- **`templates/views/card_grid.sbn`** — the `<option>` loop now resolves a label per field: `CreatedAt`/
  `UpdatedAt` map to their fixed Bulgarian labels from `auditable_entity.sbn` (they live on `AuditableEntity`,
  not in `model.properties`), any other field looks up its `display_name` in `model.properties`, else falls
  back to the raw name. The `value` attribute stays the raw field name so sorting still works. FaceNote's
  Products sort on `Title`/`Price`/`CreatedAt` — all three now render Bulgarian.
- **`fixtures/project-spec.facenote.json`** — three new regular seed users (georgi/maria/ivan@facenote.bg).
  `IdentitySeeder`/`ResolveSeedUserIdsAsync` already iterate the users list generically, so `$seedUser:2..4`
  resolve automatically and each new user gets a rotating `FirstName` + ui-avatars profile picture.
- **`prompts/in/seed-response.json`** + **`prompts/out/seeds/facenote-seed.json`** (archive copy) — regenerated
  via a deterministic transform. Ownership of Products/Posts/Catalogues/Cart/Orders/Comments/Votes/Reviews/
  Follows now spreads round-robin across `$seedUser:0..4` (every user owns something). **Key coupling honored:**
  `SeedStage` auto-fills `Catalogue`'s row minimum to `max(4, seedUserCount*2)`, which is now **10** — the
  seed grew from 4 → 10 Catalogues (2 per user) so seed-schema validation (`MinItems`) can't fail. All other
  feature-model minimums (CatalogueItem 6, CartItem 3, Order 2, OrderItem 3, Follow 2, Review 2, PostComment 8,
  PostVote 12) still met. `fixtures/seed-response.facenote.json` (the raw-3-model TEST fixture) was left
  untouched, same boundary as Session 8.

## Verification
- `dotnet build` clean; `dotnet test` green — **43/43** (was 41; +2 new in `SpecValidationTests`:
  `Missing_displayName_on_visible_property_is_now_a_hard_error` proves the gate is fatal,
  `Fixture_has_full_displayName_coverage` guards the backfill).
- `dotnet run -- spec expand --spec fixtures/project-spec.facenote.json --no-interactive` wrote + validated
  the expanded lock with zero displayName errors — proves the gate passes on all 13 models, not just the raw 3.
- Independent script replicating `BuildSeedSchema`'s rules against the expanded lock + new seed: all row-count
  minimums met (Catalogue 10/10), required fields present, no forbidden extras, every `parentRef_*` resolves
  to a real `refKey`, every `$seedUser:N` within 0..4.

## Affected files
- `src/Genesis.Core/Spec/SpecLoader.cs`
- `templates/views/card_grid.sbn`
- `fixtures/project-spec.facenote.json`
- `prompts/in/seed-response.json`
- `prompts/out/seeds/facenote-seed.json`
- `tests/Genesis.Tests/CoreTests.cs`
