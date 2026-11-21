---
name: dps-nn-seed-authoring
description: Generate a seed-response.json for the dps-nn (ДПС-НН / Genesis) ASP.NET MVC generator, given a project-spec.json (or its .lock.json) - the realistic demo data that seeds a generated app's database. Use whenever Stefan pastes/attaches a dps-nn spec file and asks for seed data, mock data, demo data, or "generate the seed". ALWAYS ask about seed-user admin/non-admin count before generating - never assume a default. Faithfully reproduces the pipeline's own automatic seed-generation prompt (SeedStage), so the output is byte-shape-identical to what the tool's built-in LLM call would produce - this lets seeding happen in any Claude chat instead of running the pipeline's own LLM exchange.
---

# dps-nn seed authoring

Generates `seed-response.json` for a dps-nn/Genesis project, given its `project-spec.json` (or
`.lock.json`). This is the manual-mode path of the pipeline's own ADR-005 file contract — normally
the tool writes `prompts/out/seed-request.md` (this exact prompt) and waits for
`prompts/in/seed-response.json` to be filled by hand or by its own API call. This skill lets any
Claude client fill that response directly from just the spec, no pipeline run required first.

**Default mode stays terse** — unlike the sister skill (`dps-nn-spec-authoring`, which discusses
design first), seed generation is mechanical once a few required questions are answered: ask what
you must (below), then emit the JSON directly, no extended back-and-forth.

## Before generating: always ask

**Seed-user admin/non-admin count — ALWAYS ask, never assume a default.** Even if the spec's
`identity.seedUsers.users` already lists a fixed count, confirm before generating rows that
reference `$seedUser:N` — the person may want more/fewer seed users than the spec currently
declares. Don't silently pick a number (not even the "1 admin + 1 non-admin" shown in the CLI's
completion banner — that's a display convention, not a seeding default).

**Language exceptions — ask ONLY if `identity.roles` names look Latin-script/English.** The
default (see Rule 1 below) is Bulgarian for everything. The one specific thing worth double-
checking: if the spec's role names themselves are Latin (e.g. `"Admin"`, `"User"` rather than a
Cyrillic role name), ask whether role-related seed content should stay Latin-transliterated to
match, or go fully Cyrillic. Don't ask about anything else — don't itemize every content category
one by one; Bulgarian-by-default already covers names/titles/descriptions/cities/comments/etc.
unless the person's own message already said "all Bulgarian" or similar, in which case skip even
this one check.

## Input

A `project-spec.json` (or `.lock.json`) — read its `models`, `seeding.rowsPerModel`,
`identity.seedUsers`, and `meta.language`/`meta.domainHint`.

## Output shape (exact contract — do not deviate)

One JSON object. One key per model named in `seeding.rowsPerModel`, each an array of row objects,
with **at least** as many rows as that model's configured count. For each model:

- **If the model is a "parent"** (something else has a `manyToOne` relation targeting it): every
  row needs a unique **`refKey`** — kebab-case, Latin characters only (e.g. `"bmw-x5-2023"`), even
  if every other field is Bulgarian. This is how child rows reference it; the real database ID
  doesn't exist yet at generation time (SQL Server assigns it on insert) — never invent a numeric
  ID.
- **For every `manyToOne` relation the model itself has** (i.e. it's a "child" of `Target`): the
  row needs **`parentRef_{Target}`** set to one of the referenced parent's emitted `refKey`
  values. Never invent a ref that wasn't emitted somewhere in this same response.
- **Every regular property** (not a key, not a foreign key) from the spec's model definition,
  using its declared `type`.
- **If the model has `images` configured**: add **`imageKeywords`** — an **array** of 1–2-word ENGLISH
  stock-photo search phrases. The array length is fixed by the model's `images.mode`: **exactly 1**
  entry for `"single"`, **2–4** entries for `"multiple"` (one photo each). This is the one field
  that's intentionally English even in a Bulgarian project — it's a search query, not UI text.
  **Always include it, every row, never skip or leave a model image-less**, and give each entry a
  *different* angle on the same item (e.g. `["red sneakers", "sneakers side view", "sneakers on
  foot"]`) so the photos in one row don't look identical. (The field is `imageKeywords` — plural,
  array. An older revision of this skill said `imageKeyword`/string; that fails the seed schema.)
- Every property flagged as a user-reference (name ends in `UserId`, or is otherwise clearly an
  owner/author field) must use one of the seed users' placeholder strings — see below. Never
  invent a user ID or GUID.

## Seed-user placeholders

`identity.seedUsers.users` in the spec has no passwords or IDs (dps-nn derives those
deterministically later). Reference seed users in your generated rows as the string
`"$seedUser:{index}"` — `$seedUser:0` for the first user listed, `$seedUser:1` for the second, and
so on, matching the order they appear in the spec's `seedUsers.users` array. Distribute ownership
realistically across the available users — not everything owned by `$seedUser:0`.

## The 8 rules (verbatim from the pipeline's own generation prompt — follow all of them)

1. **Language**: if the spec's `meta.language` is `"bg"` (or unspecified — Bulgarian is the
   default), ALL human-readable string values are in Bulgarian (Cyrillic): names, titles,
   descriptions, categories, cities, comments. Internationally-branded proper nouns (BMW, iPhone,
   Adidas) stay in their original script. If the project is explicitly English-language, write
   everything in English instead. (See "Before generating: always ask" above for the one
   role-name exception worth double-checking.)
2. **Real-world plausibility**: real city names, realistic prices, real category names, believable
   dates. No placeholder text ("Test 1", "Lorem ipsum"), ever.
3. **Variety**: no two rows share the same descriptive string.
4. **Respect each property's `seedHint`** when the spec provides one — it constrains realistic
   ranges/content for that field.
5. Every parent row gets a unique `refKey`; every child row gets its `parentRef_{Target}`; every
   image-bearing model's rows get `imageKeywords` — all as described above.
6. Dates in ISO-8601; decimals as plain JSON numbers (not strings).
7. User-reference properties use the `$seedUser:N` placeholders — never an invented ID.
8. **Scenario coverage — make the generated app demoable with zero manual clicking.** If present
   in the spec's models:
   - `Catalogue`/`CatalogueItem`: give each seed user 2-3 catalogues, and put 2-3 items in at
     least one of them.
   - `CartItem`: give at least one user a non-empty cart.
   - `Order`/`OrderItem`: give at least one user 1-2 past orders, each with items.
   - `Follow`: create at least one mutual follow relationship between seed users.
   - `Review`: add a few reviews targeting different users.
   - `PostComment`/`PostVote`: spread several comments and upvotes/downvotes across seeded posts
     so engagement looks organic, not uniformly flat.

## Output

Respond with **only** the JSON document — no markdown fences, no prose, unless explicitly asked to
explain the data. Stefan will save it as `prompts/in/seed-response.json` (to resume a paused
pipeline run with `--resume`), upload it via the GUI's "Upload seed" control, or drop it under
`seeds/` for the CLI's interactive seed picker — any of the three consume the exact same JSON
shape, so don't tailor the output to a specific one of them.
