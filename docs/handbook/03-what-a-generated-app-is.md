# A tour of what you get

Still no internals. This is what a person sees when they open a generated application, and what the
demo actually contains. The running example is the repository's own fixture, FaceNote — a small
social marketplace with products, posts, catalogues, a cart and orders.

## Getting in

The app starts with `dotnet run` inside the generated folder and shows a landing page with the
catalogue. FaceNote seeds six accounts — two in the admin role, four in the user role. The completion
banner prints one of each; all six, with their passwords, are in `logs/<Project>/seed-credentials.txt`:

| Account | Role | What it can do |
|---|---|---|
| the admin | the project's admin role (`Administrator` / `Admin` / `Owner`, chosen per project) | everything: catalogue, users, specifications, images |
| a reader | the project's user role (`User`, `Client`, or the Bulgarian word for “user”) | browse, search, own catalogue, cart, orders, profile |

Login, registration, logout and the "manage account" pages (email, password, profile) are all in
Bulgarian and styled like the rest of the site. There is no English fallback page anywhere — that is
the point.

## The client side

| Page | What it is |
|---|---|
| **Home** (landing) | branding, featured content, links into the catalogue |
| **Contact** | a static contact page with a real form layout |
| **Catalogue grid** | cards with images and price; a search box and sorting, both applied on the server |
| **Details** | one record with its relations, gallery, and feature actions |
| **Feature pages** | cart (with checkout and order history when `orders` is on), personal catalogues, post feed with comments and votes, public profiles, reviews, follows — each exists only if its feature was enabled |
| **Profile** | a public page per user aggregating their posts and catalogues; emitted when the `posts` or `catalogues` feature is on, with an avatar when the user model has a `ProfilePicturePath`. With `personalization` on, the navbar greets the signed-in user by name, shows the avatar and links to "my profile" |

Search and sorting are both query-string parameters the controller applies to the database query
(search over the model's text fields). Both are generated, not hand-wired, and both are listed in the report as code pointers.

## The admin side

| Page | What it is |
|---|---|
| **Dashboard-style navigation** | one entry per real content model, plus a consolidated page for the small ones |
| **Admin tables** | a CRUD table for every model, generated whether or not the spec asked; sorting and pagination appear where the spec configures them |
| **Specifications** | one page, tabs for small reference models (full CRUD) and join models (read-only, auto-filtered by their relations and owner) |
| **Users** | user administration, with a server-side rule preventing an admin from deleting themselves |
| **Images** | per-model image management: upload, replace, delete, with the files served locally |

The consolidation matters at demo time: a project with fourteen models still has a readable admin
menu instead of fourteen near-identical entries.

## The data you find already there

The seed is written to make every screen look used, not merely populated:

- catalogue rows with downloaded images and varied prices;
- catalogues per user containing items, so the catalogue screen is not empty;
- a non-empty cart for at least one user;
- one or two past orders with items, so the orders page has history;
- posts with comments and up/down votes spread across authors;
- reviews targeting different users;
- at least one mutual follow, so the follower and following lists show a relation.

The seed prompt and the seed-authoring skill ask for this coverage so that a demo needs no manual
clicking to look alive. It is an authoring rule, not a validation: a seed without a cart still
passes.

## What the app is made of, in the owner's terms

| Folder | The owner's mental model |
|---|---|
| `Models/` | the tables, one class each, with the Bulgarian labels attached to the columns |
| `Controllers/`, `Areas/…/Controllers/` | the pages' logic: one class per screen group |
| `Views/` | the markup; `_Layout.cshtml` is the shared shell |
| `Data/` | the seed: `seed-data.json` is readable and editable, `SeedData.cs` inserts it |
| `Migrations/` | the database schema history |
| `wwwroot/` | CSS, JavaScript and downloaded images |
| `plan/state.json` | the generator's own bookmark — deleting it forces a clean regeneration |

## What it is not

The visual design is Bootstrap plus a chosen theme and a design-token overlay: coherent and
presentable, not bespoke art direction. There is no SPA, no real-time behavior, no payment flow, no
mail sending. Every one of those is a deliberate boundary, and [Chapter 22](22-limits.md) lists them
together with what *is* supported — because the fastest way to lose trust in a generator is to
assume it did something it silently skipped.

## Why this tour matters before the architecture

Three expectations set here decide whether the rest of the book is read correctly:

1. **The output is complete.** Anything missing is a bug or a documented limit, never "you were
   supposed to finish it".
2. **The output is ordinary.** MVC, EF Core, Razor, Identity — all explainable.
3. **The output is Bulgarian by construction**, not by translation at runtime.

[Chapter 9](09-workflow.md) shows how a run produces this; [Chapter 10](10-generated-app-architecture.md)
shows how the produced app is layered.
