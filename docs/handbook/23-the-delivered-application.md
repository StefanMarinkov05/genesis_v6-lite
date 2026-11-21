# The delivered application

The rest of the book describes how the application is produced. This chapter describes what a buyer
actually receives, and one property of delivery that does not appear anywhere else: the project can
be marked.

## What is handed over

| Item | What it is |
|---|---|
| the project folder | a complete ASP.NET Core MVC solution, buildable with `dotnet build` |
| `Migrations/` | the schema, ready for `dotnet ef database update` |
| `Data/seed-data.json` + `Data/SeedData.cs` | the demo data and the code that inserts it at startup |
| `wwwroot/images/` | downloaded images plus shipped fallbacks |
| `wwwroot/output/project-summary.json` | the report: settings, resolved variation, and code pointers |
| `wwwroot/output/verify-language-report.txt` | the language gate's verdict |
| the completion banner + `logs/<Project>/seed-credentials.txt` | the URL, one admin and one ordinary account on screen, and every seeded account with its password in the file |
| `dps-nn.log.jsonl`, `plan/state.json` | how the run went and what it completed |

The buyer does not receive a build script for the pipeline, the templates, or the spec tooling —
those stay with the seller. The delivered app is a normal .NET project that the buyer owns and can
extend.

## The functional inventory

Use this as a checklist against a requirements paper. "Enabled by" names what in the spec turns the
capability on.

| Capability | Enabled by | What is delivered |
|---|---|---|
| Accounts | `identity` | registration, login, logout, password change, email change, profile page |
| Roles and areas | `identity.roles`, `areas[]` | admin and client areas, each mapped to a role, role-scoped authorization |
| Per-record ownership | `controllers[].ownership` | owner-or-admin enforcement inside the action, not in the view |
| Self-deletion guard | application-user controller | an administrator cannot delete their own account |
| Content models | `models[]` | entities, relations, validation with Bulgarian messages, audit fields, optional soft delete |
| Posts, comments, votes | `features.posts` | create/read/delete posts, comments with an optional image, up/down voting |
| Catalogue | `features.catalogues` | personal catalogues with items, public catalogue pages |
| Cart and orders | `features.orders` | cart with quantities, checkout, order history, order items |
| Reviews and ratings | `features.rating` | reviews with optional image, tied to the domain's primary model |
| Follows | `features.follows` | follow/unfollow, follower and following lists; following yourself is refused |
| Public profiles | `features.posts` or `features.catalogues` | a public profile page per user aggregating their content, reused on the admin user details page |
| Avatars | a `ProfilePicturePath` on `identity.applicationUser` | profile picture upload and the user widget in the layout |
| Multi-image support | `models[].images` | per-model image table, upload/replace/delete, gallery, carousel for `multiple` mode, local cache |
| Secondary single image | a plain `ImagePath` property | one optional image without the image tables |
| Search and sorting | `views.pages[]` (`cardGrid`), `controllers[].sorting` | server-side search over the model's text fields and server-side ordering, both as query-string parameters |
| Pagination | `controllers[].pagination` | paged admin tables |
| Admin CRUD for every model | automatic | one admin surface per model, including models the spec did not mention |
| Consolidated reference page | `isSpecification`, `isLookup` | the Specifications page: CRUD tabs for small reference models, read-only filtered tabs for join rows |
| Responsive presentation | `meta.style` | Bootstrap layout, theme, design preset, shared layout, login partial, flash messages |
| Seeded database | `seeding`, the seed JSON | realistic rows, ownership, prices, dates, images, orders |
| Machine-readable report | `outputReport` | code pointers resolved to file, line range and snippet |

## What it is not

A demo-grade application. No test project, no Dockerfile, no CI file, no payment provider, no mail
delivery, no real-time behavior, no runtime locale switch. Security is the default of the framework:
no rate limiting, no content-security policy, no hardened lockout tuning. [Chapter 22](22-limits.md)
has the complete list, including the caveats that surprise people.

## Ownership marks

A delivered project can be **marked**: the seller's authorship is embedded in the code, so the app
can be traced back to the person who produced it without relying on a written contract, a file
name, or a client's memory.

Three facts belong in any honest description of delivery:

1. **Genesis does not mark anything.** The pipeline has no marking code, no spec field and no
   command-line switch for it; every project it generates is a clean build that behaves exactly as
   this book describes.
2. **Marks are applied afterwards, deliberately, per project**, with separate tooling the seller
   keeps private. A buyer cannot request a mark through the spec, and a third party cannot
   reproduce one from it.
3. **A mark is evidence, not a feature.** It exists so that authorship can be shown if a delivery is
   disputed or left unpaid. The remedy in that case is the agreement between buyer and seller, not
   the software.

The marking tooling is not documented here. What a buyer should be told before paying:

- whether the project they receive carries a mark;
- verification results reported by the seller come from clean builds unless stated otherwise.

## Evidence that travels with the project

Two things make a delivered project auditable after the fact: the report
(`project-summary.json`) records the settings, the resolved variation and where each named behavior
lives in the code; the project's run log records the stages and their timings. The hash of every LLM
exchange is in the workspace log (`dps-nn.log.jsonl` at the workspace root), which stays with the
seller unless it is handed over.
Together they answer "what produced this, and when" without needing the seller present.

What they do **not** prove is that the application is correct in the business sense. `verify` shows
that it builds, migrates, boots, logs in, that every declared page answers, that protected pages
refuse anonymous users, and that the pages show no English ([Chapter 9](09-workflow.md)). Everything past
that — whether the price rule is the right rule — is the buyer's responsibility, and should be
raised before acceptance, not after.
