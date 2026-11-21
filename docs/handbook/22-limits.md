# Capabilities and limits

A generator is defined as much by what it refuses to do as by what it does. This chapter is the
honest ledger: use it to decide whether a paper fits, and to say *why* when it does not.

## What the generator can express today

| Capability | How it appears in the spec | State |
|---|---|---|
| Entities with typed properties, keys, nullability, enums | `models[]` | exercised in every project |
| One-to-many relations, delete behavior | `relations[]` | exercised |
| Bulgarian labels and validation messages | `displayName`, `validations[]` | a missing label is rejected by semantic validation; leaked English is caught by the language gate |
| Date rules (not future / not past / after) | `customValidationAttributes` | emitted as real attributes |
| Audit timestamps, soft delete | `conventions.timestamps`, `deleteBehavior` | exercised |
| Roles, areas, role-scoped authorization | `identity.roles`, `areas[]` | exercised |
| Seed users with derived passwords | `identity.seedUsers` | exercised |
| Extended user profile + its admin controller | `identity.applicationUser` | exercised (FaceNote's profile fields and avatar) |
| Identity pages in Bulgarian | `identity.modules`, `localization` | exercised |
| CRUD over any model, in either area | `controllers[]` + admin-supervisor injection | exercised |
| Declarative deviations: removed actions, redirects, sorting, pagination, ownership | `controllers[]` | exercised |
| Landing / contact / card-grid / admin-table pages, search and sorting | `views.pages[]` | exercised |
| Image galleries with upload and caching | `models[].images` | exercised (`multiple`) |
| Reusable features: catalogues, orders (with its cart), posts, follows, rating, personalization | `features.<name>` | exercised together in the fixture |
| A cart without checkout | `features.cart` (without `orders`) | expansion and templates unit-tested; no full Windows run yet |
| Realistic seed data with symbolic references | `seeding`, the seed JSON | exercised |
| A machine-readable report of code pointers | `outputReport` | exercised when configured |
| An English-UI project | `meta.language: "en"` | partially (see caveats) |

## What it cannot express

These are structural gaps, not missing polish. Asking for one produces an `unsupportedRequests`
entry (agent mode) or a conversation (chat skill) — never a silent omission.

- **Anything time-recurring**: bookings, calendars, schedules, availability, reminders.
- **Payments and external integrations**: payment providers, shipping APIs, e-mail delivery, SMS,
  webhooks, OAuth providers other than the local Identity setup.
- **Real-time behavior**: chat, notifications, live updates, background jobs, queues.
- **Non-SQL-Server persistence.** `meta.database.provider` accepts only `SqlServer`; there is no
  SQLite/Postgres path, and generated apps expect LocalDB on a development machine.
- **Many-to-many without a join entity**, polymorphic relations, composite keys, GUID keys on domain
  models (Identity keeps its GUID key; domain entities use int identity keys).
- **Import/export, reporting, PDF generation, file formats** beyond image upload.
- **A second language at runtime.** There is no locale switch: Bulgarian text is *emitted*, not
  translated at request time.

## Caveats that surprise people

| Caveat | Detail | What to do |
|---|---|---|
| `personalization` is a layout switch, not a data feature | it adds no models; it turns on the navbar greeting and avatar (and a "my profile" link when `posts` or `catalogues` provides profile pages). Profiles themselves come from `posts`/`catalogues`, avatars from a `ProfilePicturePath` on the user model | turn it off for a plain navbar |
| `verify` checks access, not ownership | anonymous users must be refused admin and login-only pages, but nothing checks that one logged-in user cannot reach another user's records | declare ownership deviations for user-owned models, and test them by hand |
| `meta.language: "en"` is partial | the Bulgarian *passes* are gated (scaffolded-code residue, Identity map, error describer, validation injection, language gate), but many hand-written templates contain Bulgarian literals, so an "English" project is a mixed UI | treat EN as an experiment, not a supported product; Bulgarian is the supported path |
| DTOs are wired but unproven | `dtos[]` is schema-validated, emitted and manifest-checked, but no shipped fixture uses one | if you need one, run a full `run --all` + `verify` before relying on it |
| `images.mode: "single"` looks like multiple | the service deletes the old image correctly, but the admin/client UI renders a gallery in both modes | prefer `"multiple"` for primary imagery; use a plain `ImagePath` property for a single optional photo |
| Scope guidance is real | the drafting prompt asks for 5–10 models; a 30-model spec will generate, but the demo and the review burden grow faster than the value | cut to the demonstrable core, report the rest as out of scope |
| Windows is the supported runtime | LocalDB and the installer are Windows; the generator builds and unit-tests on Linux, generation does not complete | run generation on Windows or CI |
| Demo-grade security | no rate limiting, no CSP, no hardened lockout, development connection strings | never deploy a generated app as-is; say so if asked |
| Image providers are third-party | Pexels/Pixabay need keys; Picsum is key-free but generic; the cache prevents repeat requests, not first ones | use `--image-pack` for reproducible offline demos |
| The language gate is a heuristic | 2+ Latin words with one common English word; brand names alone pass, an English sentence fails | it catches leaks, it does not certify quality |

## Fit test: five questions

If all five are "yes", the tool will save days. If any is "no", say so early.

1. **Is the app a CRUD-shaped domain:** entities, relations, lists, admin tables, a few role-gated
   flows?
2. **Is a Bulgarian UI acceptable** (or is Bulgarian exactly what is wanted)?
3. **Is the demo's value in breadth and completeness** rather than in one bespoke algorithm?
4. **Can the project run on Windows** for the generation step?
5. **Is the owner willing to explain generated code** rather than treat it as a black box?

## How the ledger grows

`meta.unsupportedRequests` is the mechanism: every ask the schema cannot express is recorded, and
the recurring ones become new `templates/features/`. The list of gaps stays short and specific
because it is a roadmap, not a verdict.

## What the design already makes possible

The limits above are the current edge, not the ceiling. Three properties of the design are
worth more than they currently earn:

**The report is already a traceability map.** `report` resolves named code pointers to file, line
and snippet. That is the raw material for a generated project document — a chapter per requirement,
each pointing at the code that implements it. The generator already knows that a search box lives
in a specific `cardGrid` view and that sorting lives in a specific action. Turning that into the
written part of a diploma project is a template problem, not a research problem.

**`unsupportedRequests` is an open loop waiting to be closed.** Today it records what the
generator could not express. If the same request recurs across runs, the record is a specification
for the next feature fragment. The distance between "the tool cannot do X" and "the tool learned X"
is a review queue, not a rewrite.

**The verifier is the product.** Any generator can emit code; the reason to trust this one is that
`verify` refuses to call it done. The same gate can supervise other targets, other stacks, or a
second backend, because it checks outcomes (builds, boots, logs in, renders, speaks Bulgarian)
rather than internals. It now fails on any declared route that does not answer, and on any
protected page an anonymous request can open; a per-user ownership check is the next step.

The three moves that would raise the ceiling most, in order:

1. **A generated project document** (requirements → code pointers → prose), built from the report.
2. **A capability matrix checked by the tool**, so a spec can be rejected with "not supported"
   before an LLM spends a repair round discovering it.
3. **A second emission backend** behind the same spec, to prove the pipeline is a compiler rather
   than an ASP.NET template collection.

Each is an extension of something that exists. None requires abandoning the current shape.
