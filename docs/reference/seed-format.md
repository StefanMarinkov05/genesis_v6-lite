<!-- last-verified: ad7411e -->
# Seed format — the second input JSON

**Diataxis type:** reference. **Source of truth:** `SeedStage.BuildSeedSchema` (the per-project
schema), `SeedStage.RunAsync` (the exchange + reuse rules) in
`src/Genesis.Core/Stages/SeedStage.cs`, and `templates/models/seed_data.sbn` (what consumes it).
There is no static schema file: the seed schema is **derived from the spec at run time**, which is
why this doc describes the contract rather than pointing at a JSON Schema.

## What it is

A generation run has exactly **two JSON inputs**, and they answer different questions:

| Input | Owns | Written by |
|---|---|---|
| `project-spec.json` | the app's **shape** — models, properties, display names, controllers, views, identity, features, and *how many* seed rows each model gets (`seeding.rowsPerModel`) | a human, `spec build`, or agent mode ([spec format](spec-format.md)) |
| the seed JSON | the app's **contents** — one array of realistic rows per model, with symbolic cross-references instead of database IDs | the `seed` LLM exchange, a hand-written file, `utils/claude-skills/dps-nn-seed-authoring`, or the archived copy of a previous run |

The spec decides which models exist; the seed fills them. A seed file is therefore **spec-specific**:
change the models and the old seed stops validating (see "Reuse and validation").

## Where the seed comes from, and where it goes

| Location | Role |
|---|---|
| `prompts/in/seed-response.json` | **The canonical hand-off file.** The `seed` stage reads it as the LLM response in the ADR-005 file contract ([agent mode](../explanation/agent-mode.md) / [control flow](control-flow.md)). |
| `seeds/*.json` | Shipped candidates (the repo has `seeds/facenote-rich-seed.json`). The CLI's seed picker copies the chosen file to `prompts/in/seed-response.json`; a *single* candidate is deliberately not auto-applied. |
| `prompts/out/seeds/<project>-seed.json` | Archive of the seed a successful run used, written by the CLI after `run --all` succeeds. Findable again by the picker. Gitignored. |
| `<out>/<Project>/Data/seed-data.json` | The seed as it lands **inside the generated app**. `Data/SeedData.cs` reads it at startup and inserts the rows — so the file is data, not compiled values. |
| `<out>/<Project>/Data/SeedData.cs` | Emitted C# that reads `Data/seed-data.json`, inserts parents → children in `ordered_models` order, and resolves refs. |

The GUI's "upload seed" control fills the same `prompts/in/seed-response.json`; all four entry
points consume one shape.

## Shape

One JSON **object**. One key per model that has a `seeding.rowsPerModel` entry — each an array of
row objects:

```json
{
  "Brand":    [ { "refKey": "brand-1", "Name": "Samsung" } ],
  "Product":  [ {
      "refKey": "prod-1",
      "parentRef_Brand": "brand-1",
      "parentRef_Category": "cat-1",
      "Title": "Смартфон Samsung Galaxy A54 128GB",
      "Price": 520.0,
      "UserId": "$seedUser:1",
      "imageKeywords": ["samsung galaxy smartphone front view", "samsung galaxy smartphone side view"]
  } ],
  "OrderItem": [ { "parentRef_Order": "order-1", "parentRef_Product": "prod-1", "Quantity": 1, "UnitPrice": 520.0 } ]
}
```

(Shortened from `seeds/facenote-rich-seed.json`, which is the richest real example — 13 models,
~1000 rows.)

### Row fields

| Field | Required when | Meaning |
|---|---|---|
| `refKey` | the model is a **parent** — some other model has a `manyToOne` relation targeting it | A unique, kebab-case, Latin identifier for the row, invented by the seed (e.g. `brand-1`). The database assigns the real key at insert time, so **no numeric ID is ever written here** ([ADR-003](../adr/adr-003-seeding-symbolic-refs.md)). |
| `parentRef_<Target>` | the model has a `manyToOne` relation to `<Target>` | The `refKey` of the parent row this one belongs to. Must match a `refKey` emitted in the same file. |
| every declared property | unless the property is the key, a foreign key, or nullable | The row's value, using the property's declared `type`. Nullable properties may be omitted — `seed_data.sbn` reads them with `TryGetProperty`. |
| the owner property (`…UserId`) | the spec declares one | One of the seed-user placeholders (below). |
| `imageKeywords` | the model declares an `images` block | An **array** of English stock-photo search phrases: exactly 1 for `mode: "single"`, 2–4 for `mode: "multiple"`. This is the one intentionally English field — it is a search query, not UI text ([image pipeline](../explanation/image-pipeline.md), [ADR-011](../adr/adr-011-image-pipeline-cache-and-fallbacks.md)). |

Objects are closed (`additionalProperties: false`): an extra field is a validation failure, not a
harmless annotation.

### Seed-user placeholders

`identity.seedUsers.users` lists emails and roles, never passwords or IDs. A seed row that needs an
owner writes the literal string `"$seedUser:<index>"`, counting from 0 in the spec's array order.
`Data/SeedData.cs` resolves it to the real Identity GUID of the N-th seeded user after
`IdentitySeeder` has created them. Never invent a GUID.

### Counts

Each model's array must have **at least** `seeding.rowsPerModel[model]` rows (`MinItems`); more is
allowed. Models that only exist because a feature fragment injected them (`Catalogue`, `Order`,
`PostComment`, …) are given a default count by `SeedStage` before the schema is built, so feature
models are seeded too even though the raw spec never mentioned them.

## How the seed is validated

1. `SeedStage.BuildSeedSchema` builds a JSON Schema **from the current (expanded) spec**: the model
   keys, each row's required properties, `parentRef_*` per `manyToOne` relation, `refKey` for
   parents, and the `imageKeywords` array bounds.
2. The response is validated against it (`LlmBridge.Validate`, ADR-005). In automatic mode a failure
   is retried up to 3 times with the violations appended to the request.
3. **Reuse**: if `<out>/<Project>/Data/seed-data.json` already exists *and* validates against the
   schema just built, it is reused and the LLM exchange is skipped entirely. This is why a re-run
   after a crash later in the pipeline does not force a fresh seed — and why editing the models
   makes the old seed invalid and regenerates it.
4. The stage then downloads each `imageKeywords` entry (cache keyed by provider+keyword, ADR-011),
   emits `Data/SeedData.cs`, and runs `ef migrations add Initial` + `ef database update` when the
   database has not already been migrated for the current spec hash.

## Rules the seed content must follow

The stage's own prompt states them; the authoring skill restates them for manual use
(`utils/claude-skills/dps-nn-seed-authoring/SKILL.md`):

1. Bulgarian everywhere (Cyrillic) when `meta.language` is `bg`; proper nouns stay in their own
   script.
2. Plausible, market-real data — real cities, realistic prices, believable dates; no placeholders.
3. No two rows share the same descriptive string.
4. Honour a property's `seedHint` when the spec provides one.
5. Unique `refKey` per parent row; `parentRef_*` only to emitted refs; `imageKeywords` on every row
   of an image-bearing model.
6. Dates ISO-8601, decimals as JSON numbers.
7. Owner fields use `$seedUser:N`, distributed across users.
8. **Scenario coverage** — the generated app must be demoable with zero manual clicking: catalogues
   per user with items, a non-empty cart, past orders with items, a mutual follow, reviews, and
   spread comments/votes when those models exist.

## What the seed does *not* contain

- **No seed users** — those live in the spec (`identity.seedUsers`), and their passwords are derived
  deterministically at expand time ([spec format](spec-format.md)).
- **No IDs** — neither primary nor foreign keys.
- **No UI text** — labels, page titles and error messages come from the spec's `displayName`/
  `validations`; the seed only carries row content.

## Where to look next

- The other input: [spec format](spec-format.md).
- Why symbolic refs: [ADR-003](../adr/adr-003-seeding-symbolic-refs.md).
- What the rows turn into inside the app: [generated app anatomy](../explanation/generated-app-anatomy.md)
  and [image pipeline](../explanation/image-pipeline.md).
- Manually authoring one in a chat: `utils/claude-skills/dps-nn-seed-authoring/SKILL.md`.
