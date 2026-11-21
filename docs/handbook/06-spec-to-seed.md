# From a spec to seed data

A generated app that opens to empty tables is not a demo, it is a database. The seed stage exists
to make the first boot look like a used product: rows in every list, images on every card, an order
history, a few comments and votes, a couple of users who own things.

The seed is the **second input file**, and it is written against the spec's models.

## The contract in one screen

```json
{
  "Brand":    [ { "refKey": "brand-1", "Name": "Samsung" } ],
  "Product":  [ {
      "refKey": "prod-1",
      "parentRef_Brand": "brand-1",
      "parentRef_Category": "cat-1",
      "Title": "Samsung Galaxy A54 128GB smartphone",
      "Price": 520.0,
      "Quantity": 1,
      "UserId": "$seedUser:1",
      "imageKeywords": ["samsung galaxy smartphone front view",
                        "samsung galaxy smartphone side view"]
  } ],
  "OrderItem": [ { "parentRef_Order": "order-1", "parentRef_Product": "prod-1",
                   "Quantity": 1, "UnitPrice": 520.0 } ]
}
```

| Field | Required when | Rule |
|---|---|---|
| model key | the model has a `seeding.rowsPerModel` entry | one array per model, **at least** that many rows (more is fine) |
| `refKey` | the model is a **parent** (something has a `manyToOne` to it) | unique, kebab-case, Latin — `brand-1`, not `1` |
| `parentRef_<Target>` | the model has a `manyToOne` relation named `<Target>` | must equal a `refKey` emitted **in this same file** |
| a declared property | unless it is the key, a foreign key, or nullable | the property's declared type; dates ISO-8601, money as a plain number |
| `…UserId` owner fields | the spec declares one | `"$seedUser:0"`, `:1`, … in the spec's `seedUsers` order |
| `imageKeywords` | the model has an `images` block | an **array** of English search phrases: exactly **1** for `mode: "single"`, **2–4** for `mode: "multiple"` |

Objects are closed: an extra field fails validation. No ID of any kind is ever written — the
database assigns keys at insert time, and `SeedData.cs` maps `refKey` → real key in a dictionary as
it inserts parents first.

## Why symbolic references, and not IDs

An LLM cannot know the identity value SQL Server will assign, and a human editing rows by hand
cannot keep numbers in sync through a re-run. So the seed references rows the way a spreadsheet does:
by name. The generated `Data/SeedData.cs` inserts models in a **topological order** (`ordered_models`
in the template engine: any model whose parents are already inserted), captures each parent's real
key, then resolves children through it. That ordering is why a model can be both a child (Product →
Brand) and a parent (Product ← OrderItem) without a second pass.

## Where the file goes

| Path | Role |
|---|---|
| `prompts/in/seed-response.json` | the canonical hand-off — what the seed stage reads in the LLM file contract |
| `seeds/*.json` | shipped candidates; the CLI's picker copies one to the path above |
| `prompts/out/seeds/<project>-seed.json` | archive of what a successful run actually used |
| `<out>/<Project>/Data/seed-data.json` | the same data **inside** the generated app (read at startup, not compiled in) |

The GUI's "upload seed" writes the same first path. All four entry points accept one shape.

## Writing it by hand (or in a chat)

The repo ships a portable skill, `utils/claude-skills/dps-nn-seed-authoring`, that reproduces the
pipeline's own seed prompt so a chat client can produce the file without running anything. Its two
behaviours that matter:

- **It asks for the seed-user count before writing anything.** Never assume one admin and one user;
  the count decides how ownership is distributed.
- **It stays terse.** Unlike the spec skill, seed generation is mechanical once the spec exists.

If you write rows yourself, follow the same content rules the stage's prompt states:

1. Bulgarian for every human-readable value when `meta.language` is `bg`; proper nouns keep their
   script.
2. Real cities, believable prices and dates — no `Test 1`, no lorem ipsum.
3. No two rows share the same descriptive string (variety is visible on the page).
4. Honour a property's `seedHint` when the spec gives one.
5. Dates ISO-8601; decimals as numbers.
6. Spread ownership across seed users.
7. **Scenario coverage**: if catalogues exist, each user has 2–3 with items; if a cart exists, one
   user has a non-empty one; if orders exist, one user has 1–2 past orders with items; if follows
   exist, at least one mutual pair; if reviews exist, reviews targeting different users; comments and votes spread
   across posts.

Rule 7 separates a demo from a table dump, and it is the one most often skipped.

## Images

Every row of an image-bearing model carries `imageKeywords`. The stage downloads one file per
phrase, names it `{model}-{row}-{img}.jpg`, and caches downloads in `.image-cache/` keyed by
provider + phrase, so a regenerate costs no requests. Providers: Pexels (`PEXELS_API_KEY`), Pixabay
(`PIXABAY_API_KEY`), Picsum as the key-free fallback, or a local directory via `--image-pack`.
The generated app serves the files from `wwwroot/images/…`; it never hot-links a remote URL.

Shipped fallbacks (`avatar-default.svg`, `product-default.svg`) cover a failed download, so a page
never renders a broken image.

## Validation, reuse, and the failure you will actually see

The stage builds a JSON Schema from the current spec and validates the response against it. Two
consequences:

- **A valid `Data/seed-data.json` is reused** and the LLM exchange is skipped — re-running after a
  crash does not ask for new data.
- **Changing the models invalidates the old seed**, which is then regenerated. That is intentional:
  rows for a renamed property cannot be silently translated.

The validator's messages are the JSON Schema library's own, prefixed with the path of the row that
failed; the table names the failure rather than quoting it.

| Failure (what the message says) | Cause | Fix |
|---|---|---|
| a required property `parentRef_Brand` is missing | the model is a child but the row has no parent link | add the link to a `refKey` in the same file |
| a required property `refKey` is missing | the model is a parent (another model points at it) | give every row a unique `refKey` |
| `Value is "string" but should be "array"` on `imageKeywords`, or a wrong item count | singular string, or 3 phrases for `single` | 1 phrase for `single`, 2–4 for `multiple` |
| `All values fail against the false schema` | a field not in the model (closed objects reject unknown keys this way) | remove it — content-only notes belong in `seedHint` or nowhere |
| too few rows | fewer than `rowsPerModel` | add rows or lower the count in the spec |
| `verify` fails on English | an English description slipped in | Bulgarian content (keywords are the single allowed English field) |

## After it lands

The generated app seeds itself at startup, guarded by an "already seeded?" check on the first model,
so restarting does not duplicate rows. To re-seed deliberately: drop the database (or change its
name) and run again — `SeedStage` tracks the DB it migrated in `logs/<Project>/db.json` and will
otherwise skip the migration work.

Next: [Chapter 7](07-agent-mode.md) automates the loop that produces both JSONs; [Chapter 11](11-contracts.md)
is the field-by-field reference when the worked example here is not enough.
