<!-- last-verified: ad7411e -->
# Generated app anatomy — what comes out of a run

**Diataxis type:** explanation. **Source of truth:** the templates under `templates/` plus the
stage that emits each one (`src/Genesis.Core/Stages/*.cs`); a real, complete example is
`out/run_bg/FaceNote/` after a successful `run --all` (regenerate it — never hand-edit `out/`).

This is the *output* side of the generator. The *input* side is [spec format](../reference/spec-format.md)
and [seed format](../reference/seed-format.md); the engine that connects them is
[components](../reference/components.md).

## The one-sentence model

A generated project is **one ASP.NET Core MVC app with two role-scoped MVC areas** (`Admin`,
`Client`), an `Identity` area whose pages are emitted in Bulgarian, an EF Core context over
spec-declared models, three small services that only exist when the spec asks for them, and a
runtime-read seed dataset — nothing else. Everything in it traces back to a spec field; if a file
is in the output, some stage wrote it deliberately.

## The tree

| Path | Holds |
|---|---|
| `<Project>.csproj`, `Program.cs`, `appsettings*.json` | scaffold output + `program.sbn` — DI, routes, migrate-then-seed startup |
| `<Project>Context.cs` | `dbcontext.sbn` — `DbSet`s, relations, soft-delete filter |
| `Models/` | one file per spec model, plus `AuditableEntity` and `ApplicationUser` |
| `Controllers/HomeController.cs` | the app's default (non-area) controller |
| `Infrastructure/Validation/` | the `customValidationAttributes`' attribute classes |
| `Infrastructure/Identity/BgIdentityErrorDescriber.cs` | Bulgarian Identity error strings (Bulgarian projects only) |
| `Data/IdentitySeeder.cs` | roles + seed users, resolves `$seedUser:N` |
| `Data/SeedData.cs` + `Data/seed-data.json` | runtime seed insert over the seed JSON |
| `Services/` | `ImageService` / `FileUploadService` / `UserProfileService`, each only when needed |
| `Areas/Admin/` | admin controllers + views, Спецификации page, users, image management |
| `Areas/Client/` | landing, contact, card grids, feature pages |
| `Areas/Identity/Pages/Account/` | login, register, logout, manage/* |
| `Views/Shared/` | layout, login partial, image partials, error page |
| `wwwroot/` | `css/site.css`, downloaded images, default fallbacks |
| `Migrations/` | EF migrations — `Initial`, then timestamped autos |
| `plan/state.json`, `dps-nn.log.jsonl` | pipeline resume state and the stage-boundary log the GUI tails |

The `wwwroot/lib/` folder (Bootstrap, jQuery, validation scripts) is the MVC template's own, left
untouched.

## Where each part comes from

| Part | Emitted by | Notes |
|---|---|---|
| Project skeleton, packages, connection string, static image fallbacks | `scaffold` (`BuildStages.cs`) | `dotnet new sln` + `mvc`, then `dotnet sln add`; idempotent — an existing `.csproj` short-circuits it. Ends in a build gate. |
| `Models/*.cs`, DTOs, validation attributes, `AuditableEntity`, the DbContext | `emit-models` | All through Scriban (`models/*.sbn`). Bulgarian `[Display(Name)]` and `[Required(ErrorMessage)]` come straight from the spec, no literal text in the template. |
| `ApplicationUser`, `Program.cs`, `Data/IdentitySeeder.cs`, `BgIdentityErrorDescriber`, Identity Razor pages, roles, seed users | `identity` | Razor pages are emitted wholesale for the core account pages ([ADR-002b](../adr/adr-002b-identity-pages-wholesale.md)); anything left from the scaffolder goes through the static EN→BG map ([ADR-002](../adr/adr-002-identity-localization-static-map.md)). |
| Controllers + views in both areas | `emit-controllers-views` | Three provenances — see below. |
| `Data/seed-data.json`, `Data/SeedData.cs`, `wwwroot/images/**`, `Migrations/*` | `seed` | See [seed format](../reference/seed-format.md) and [image pipeline](image-pipeline.md). |
| `wwwroot/output/project-summary.json` | `report` | Only when the spec has an `outputReport` section; resolves `codePointer`s to file+line+snippet. |
| `plan/state.json`, `dps-nn.log.jsonl` | `PipelineRunner` | Written by the engine, not by a template. |

## The three controller provenances

`emit-controllers-views` is the one stage where a controller can arrive three different ways, and
the spec's `controllers[].generatedBy` picks which:

1. **`scaffold`** — `aspnet-codegenerator` produces a normal MVC CRUD controller, then *declarative
   deviations* are applied as anchored regex transforms on the known scaffolder shape
   (`removedActions`, `redirects`, `sorting`, `ownership`). Regex rather than a Roslyn rewrite is
   deliberate ([ADR-006](../adr/adr-006-scaffold-deviations-regex.md)), and a transform that cannot
   find its anchor fails loud instead of silently emitting the wrong code.
2. **`template`** — Scriban emission for controllers the scaffolder cannot produce (feature
   controllers, `HomeController`, the specifications page).
3. **`writtenFromScratch`** — a dedicated template with server-side rules, e.g. the users controller
   enforcing `AdminCannotDeleteSelf`.

Everything downstream treats all three identically: they are ordinary MVC controllers in
`Areas/<Area>/Controllers/`, registered by convention, exercised by `verify`'s route discovery.
Authorization is uniform too: the stage gives every admin-area controller
`[Authorize(Roles = "<admin role>")]` and every client-area controller `[Authorize]` with
`[AllowAnonymous]` on its read-only `Index`/`Details` (`InjectAreaAuthorization`, changelog 141),
and `verify` checks that an anonymous request to each protected page is refused.

## The two areas, and the "Спецификации" page

`areas` in the spec maps each MVC area to a role: `Admin` → the admin role, `Client` → the user
role. Both names come from the variation profile (`$variation:adminRoleName`,
`$variation:userRoleName`), so role names differ between projects while staying stable within one.

The admin area does not get one nav entry per model. Small models are consolidated onto a single
**Спецификации** page ([ADR-012](../adr/adr-012-specifications-consolidation-and-filters.md),
[design spec](specifications-consolidation.md)):

- `isSpecification` models (atomic reference data with no outgoing FK — Brand, Category) get a CRUD
  tab.
- `isLookup` models (join rows — `CartItem`, `OrderItem`, `PostComment`) get a read-only tab with
  filters derived from their `manyToOne` relations and any `…UserId` property.
- Every other model keeps its own admin page and nav entry. `spec-expand` additionally injects an
  admin CRUD surface for any model the spec forgot, so "full CRUD over everything" needs no extra
  spec work.

## Identity, in Bulgarian

The Identity area is ordinary ASP.NET Core Identity (cookie auth, `IdentityUser` extended by
`ApplicationUser`, a GUID key), but every user-visible string is Bulgarian:

- **Pages** — login/register/logout/manage are emitted from `templates/identity/*.sbn` with
  Bulgarian copy and the project's own layout, rather than translated at runtime
  ([ADR-002b](../adr/adr-002b-identity-pages-wholesale.md)).
- **Errors** — `BgIdentityErrorDescriber` replaces Identity's English messages
  ([ADR-002](../adr/adr-002-identity-localization-static-map.md)).
- **Seeded users** — `IdentitySeeder` creates the roles and users from the spec and derives each
  password deterministically from the project's variation seed, so the banner's credentials are
  reproducible ([ADR-004](../adr/adr-004-variation-profile.md)). `verify` logs in through the real
  form with exactly those credentials.

## Services that exist only when needed

| Service | Emitted when | Purpose |
|---|---|---|
| `Services/ImageService.cs` | any model declares an `images` block | Store/read images through the `{Model}Image` tables; serves files from disk, never a hot-linked URL ([ADR-011](../adr/adr-011-image-pipeline-cache-and-fallbacks.md)). |
| `Services/FileUploadService.cs` | profile pictures, the rating feature, or the posts feature is on | One shared single-file upload helper for secondary images (a plain `ImagePath` string, not the `images` system). |
| `Services/UserProfileService.cs` + `UserProfileViewModel` | the `posts` or `catalogues` feature is on | Public profile pages (also reused on the admin user details page). The `personalization` flag adds no files: it gates the navbar greeting, avatar and "Моят профил" link in `_Layout.cshtml`. |

`verify`'s manifest check only expects a file when the spec says it should exist, so an English or
minimal project does not fail over a service it never needed.

## What the app does at startup

`Program.cs` (emitted by `program.sbn`) wires, in order: EF Core with the project's connection
string → Identity with the Bulgarian describer and role policies → area routes (a single
`{area=Client}/…` route when `meta.routing.defaultArea` is set, otherwise the classic pair) →
DI for whichever services were emitted → `Database.Migrate()` when `migrateOnStartup` → the
identity seeder → the data seeder. That order is why a fresh generated app can be opened and logged
into with no manual setup, and why `verify` can boot it on a random port and immediately scan pages.

## What is *not* in a generated app

- No test project, no Dockerfile, no CI file — the app is the deliverable, not its own pipeline.
- No JavaScript framework; card-grid search, sorting and the gallery are the page's own inline
  script plus server-rendered markup.
- No runtime translation layer: Bulgarian text is emitted as text. `meta.language: "en"` skips the
  Bulgarian passes rather than switching a locale ([spec format](../reference/spec-format.md)).

## Where to look next

- Emitting more of it: [add a template](../how-to/add-a-template.md), [techniques](techniques.md).
- Checking it: [control flow](../reference/control-flow.md) for the `verify` gate.
- The GUI that drives the run: [GUI architecture](gui-architecture.md).
