---
name: dps-nn-spec-authoring
description: Design or extend a project-spec.json for the dps-nn (ДПС-НН / Genesis) ASP.NET MVC generator - a declarative spec that turns into a full seeded app through dps-nn's 9-stage pipeline. Use whenever Stefan pastes raw project notes and asks for a project-spec.json, asks to add a model/controller/view/feature to an existing spec, asks "does this spec look right" for the dps-nn tool, or wants to talk through a schema design before committing to it. Default workflow is CONVERSATIONAL - discuss the domain, flag problems and judgment calls, converge, THEN emit the JSON - not a one-shot dump. Mirrors the pipeline's own spec-build stage and the real generator conventions (isSpecification/isLookup consolidation, removedActions, ownership, redirects), so the output matches or improves on what the automatic LLM path would produce. Works with or without spec.schema.json attached - if it's available, treat it as authoritative over this skill's summary.
---

# dps-nn spec authoring

Designs or extends `project-spec.json` for dps-nn/Genesis — the tool that turns one JSON spec into
a buildable, seeded, verified ASP.NET Core MVC + EF Core app. This mirrors the pipeline's own
`spec-build` stage, the concept groups in `docs/reference/spec-format.md`, and the real generator
conventions established through actual shipped work (not just the schema's bare shape) — so a
spec drafted here matches, or improves on, what the pipeline's automatic LLM path would produce.

**If `spec.schema.json` is attached to the conversation, it is the authoritative source of truth
for exact field names/types/enums** — this skill is a guide for structure and judgment, not a
substitute for the schema. Where this skill and the attached schema disagree, the schema wins.

## Workflow: discuss first, emit JSON when ready

**Default mode is conversational, not one-shot.** Read the person's raw notes/description, then:

1. Infer the domain's core models, their relations, and which controllers/views/features they
   imply. Don't over-scope — a diploma-work sample app is usually 5–10 models, not 30.
2. **Flag real judgment calls out loud before deciding them** rather than silently picking one:
   which models should consolidate into one admin page (see below — this one IS decided
   automatically per the rule, don't ask, just apply it and mention what you applied), which
   controllers need `removedActions`/`ownership`/custom `redirects`, whether a DTO is actually
   warranted, single vs multiple image mode per model.
3. Ask (don't guess) about `conventions.timestamps` (CreatedAt/UpdatedAt/DeletedAt: y/n each) if
   it matters and wasn't specified — mirrors the pipeline's own interactive prompt.
4. Converge with the person on anything ambiguous, THEN output the JSON.
5. **Terse JSON-only is an explicit opt-in**, not the default — if the person says "just give me
   the spec" or similar, skip the discussion and emit directly.
6. Output ONLY the JSON document when finalizing — no markdown fences, no prose commentary,
   unless asked for an explanation alongside it.

Scope stays bounded to `project-spec.json` itself. "Build the whole file system" means a
*comprehensive* spec (every model/controller/view/feature/identity concept fully fleshed out, not
a partial draft) — the actual Models/Controllers/Views file tree is what dps-nn's pipeline
generates *from* that spec; this skill never touches generator source or templates.

## Hard requirements (non-negotiable)

1. **Every non-key/non-FK model property needs a `displayName`.** This is dps-nn's hard
   Bulgarian-UI requirement — the generated app's `verify` stage scans every rendered page for
   leaked English text and fails the build if it finds any. A property with no `displayName`
   renders its bare C# name, which usually IS the English leak.
2. **Bulgarian by default, unless told otherwise.** If `meta.language` isn't specified as
   something else, default to `"bg"` and write ALL human-readable strings (names, titles,
   `displayName`s, `errorMessage`s, category names) in Bulgarian (Cyrillic). Internationally
   branded proper nouns (BMW, iPhone, Adidas) stay in their original script. Only write English
   strings if the person explicitly asks for an English-language project.
3. **`specVersion` must be `"1.0"` or `"1.1"`. `meta` and `models` are the only other top-level
   required keys** — everything else (`conventions`, `identity`, `controllers`, `views`,
   `features`, `seeding`, `outputReport`) is optional but almost always wanted for a real app.

## Model taxonomy — when small models consolidate into one admin page

dps-nn's admin surface auto-consolidates small models into a single "Спецификации" page (fixed
generator naming — not spec-configurable, don't try to rename it) with tabs by flavour. Apply
these flags **automatically, without asking** — this is a decided rule, not a judgment call:

| Flag | Trigger (apply automatically) | Tab behaviour |
|---|---|---|
| `isSpecification: true` (on the model AND its admin `ControllerSpec`, if you hand-author one) | Atomic reference model: **no outgoing `manyToOne` relations**, small property count (e.g. Brand, Category — just a `Name`) | Full CRUD tab, reuses the model's own scaffolded controller as a hidden write sub-controller |
| `isLookup: true` (model + controller) | Join/junction model: exists to link two other models (e.g. CartItem, OrderItem, PostComment, PostVote, CatalogueItem) | Read-only tab with auto-derived filters (every `manyToOne` relation → a select; any `UserId`-suffixed property → a user select) |
| (no flag) | A real content model with its own meaningful admin surface (Product, Order, Post, Catalogue) | Own full admin page, own nav entry |

If you hand-author a `ControllerSpec` for an `isSpecification`/`isLookup` model (rather than
letting the generator's admin-supervisor auto-inject one), also add `"redirects"` so its
Create/Edit/Delete land the user back on Спецификации instead of its own (nav-hidden but still
technically reachable) Index:

```json
"redirects": [
  { "after": "Create", "to": "SpecificationsController.Index" },
  { "after": "Edit", "to": "SpecificationsController.Index" },
  { "after": "DeleteConfirmed", "to": "SpecificationsController.Index" }
]
```

## Action customization — `removedActions`, `ownership`, `redirects`

Three real levers on `ControllerSpec`, each with a concrete trigger:

- **`removedActions`** — strip a CRUD action that doesn't make sense for this controller.
  - A model with **≤1 non-key/non-FK/non-computed property** never needs a Details page (the
    list view already shows everything) — always add `"removedActions": ["Details"]` for such
    models. (The generator's own admin-supervisor auto-injection applies this rule automatically
    for models it auto-covers; you only need to add it by hand if you're hand-authoring the
    controller entry yourself.)
  - A model that only ever originates from another flow (e.g. `Order` — created only via client
    checkout, never hand-created by an admin) should remove `"Create"`.
- **`ownership`** — `{"userProperty": "UserId"}` guards Edit/Delete server-side so only the
  owning user (or an admin) can mutate a record. Add this to any model a regular (non-admin) user
  can create for themselves and should exclusively control (posts, reviews, catalogues, cart
  items) — not to admin-only content models.
- **`redirects`** — `{"after": "ActionName", "to": "TargetController.TargetAction"}` overrides
  where a scaffolded action sends the user after success (default is always back to its own
  Index). The main real trigger is the Спецификации sub-controller pattern above; use sparingly
  otherwise.

## Images — `images` block vs a plain `ImagePath` string, and single vs multiple mode

Two genuinely different patterns exist — don't conflate them:

- **A plain nullable `string` property named `ImagePath`** (no `images` block at all) — for a
  lightweight, single, OPTIONAL attached photo with no dedicated management UI (e.g.
  `Review.ImagePath`, `Post.ImagePath`). Simple, no per-model image table, no ImageService
  involvement. Use this for anything secondary/optional.
- **A full `images` block** (`{"mode": "single"|"multiple", "editSelectable": bool, "storedIn": "wwwroot/images/..."}`)
  — for a model's PRIMARY content images, wired into a dedicated `{Model}Image` EF table, the
  shared `ImageService`, a real admin image-management page, and (for `mode: "multiple"`) a
  client-facing carousel. Use this only for a model where images are a first-class concern
  (Product-style listings).

**Known caveat, be honest about it**: `mode: "single"` is implemented correctly at the service
layer (uploading a new image deletes the old one) but the admin/client UI templates don't yet
visually differentiate single from multiple — as of this writing they always render a
multi-upload gallery / carousel regardless of the configured mode. In practice only `Product`
uses the `images` block today, always as `"multiple"`. If a real single-image-primary-content
case comes up, flag this explicitly rather than silently picking `"single"` and assuming the UI
will look right — check whether the generator fix has landed (`plan/decisions.md` / recent
changelogs), and until then prefer `"multiple"` even for something that's conceptually one photo,
or note the UI mismatch as a known limitation of that generate.

## DTOs — implemented, but unproven

`spec.dtos` (`composedOf` + a per-DTO `dto.sbn` emission) is fully wired into the pipeline
(schema-validated, emitted, checked by `verify`'s manifest) — but **no real shipped project has
ever actually used one**; every real fixture's `dtos` array is empty. Treat it as a real but
**untested** feature:

- It's reasonable to propose a DTO when the shape genuinely fits (a composed read-model
  projecting fields from 2+ related tables for a single view) — don't avoid it reflexively.
- When you do propose one, say so explicitly and recommend a real `run --all` + `verify` pass to
  confirm it actually renders/compiles correctly before relying on it in a real project — don't
  present it with the same confidence as the rest of the schema, which has been exercised
  end-to-end many times.

## Top-level concept groups (reference)

- **`meta`** — `projectName`, `solutionName`, `rootNamespace`, `domainHint` (English, used by the
  pipeline's own prompts), `domainHintBg` (Bulgarian version), `language`, `database`, `style`
  (Bootstrap theme + design preset), `routing`.
- **`conventions`** — `timestamps` (ask y/n per field) and `deleteBehavior` (`"soft"` wires an EF
  global query filter so admins can undo deletes without losing data).
- **`models`** — the domain entities. Each property: `name`, `type` (`string`/`int`/`decimal`/
  `DateTime`/`DateOnly`/`bool`/`double`/`enum`), `key`/`foreignKey`/`nullable` flags,
  `validations` (Bulgarian `errorMessage`s), `enumValues`, optional `seedHint` (a sentence the seed
  LLM is told to honour — real ranges, units, plausible values), and **`displayName`** (required — see
  Hard Requirements). `relations` describe FK/navigation shape. `images` — see Images above.
  `isSpecification`/`isLookup` — see Model Taxonomy above.
- **`identity`** — `modules` (which Identity pages to generate), `roles`, and `seedUsers` (email +
  role **only** — never write a password field; the pipeline derives seed-user passwords
  deterministically from the project's own variation seed).
- **`controllers`** — `generatedBy` is exactly one of `"scaffold"` (codegen + declarative
  deviations — `removedActions`/`redirects`/`sorting`/`ownership`), `"template"`
  (framework-emitted), or `"writtenFromScratch"` (fully custom).
- **`views`** — `layout` (shared chrome) and `pages`, each `kind` exactly one of `"landing"`,
  `"contactFormStatic"`, `"cardGrid"`, or `"adminTable"`.
- **`features`** — reusable fragments (cart, posts, catalogues, orders, rating, follows...)
  toggled with `{"enabled": true}`. Only enable a feature actually relevant to the domain hint.
- **`seeding`** — just make sure `rowsPerModel` exists for every model that should get seed
  rows; the actual data generation is the OTHER skill, `dps-nn-seed-authoring`.
- **`outputReport`** — only needed for a downstream doc/report step consuming `codePointer`s.

## When something looks wrong

If asked to review an existing spec: check the hard requirements first (displayName coverage, no
invented passwords in `seedUsers`, `generatedBy`/`kind` values are exactly the allowed enum
strings), then check internal consistency (every `manyToOne` relation's `target` actually names a
model that exists), then check whether the model taxonomy (isSpecification/isLookup) and action
customization (removedActions/ownership/redirects) are being applied where the rules above say
they should be.
