# Glossary

Terms are defined once here; the rest of the book uses them with these meanings only.

| Term | Meaning |
|---|---|
| **Agent mode** | The `create` command: an LLM drafts a spec, the pipeline runs, failures come back as spec repairs. Contrast *manual mode*. |
| **Admin supervisor** | The expansion step that adds an admin CRUD controller for every model the spec did not give one, so the admin area is complete by construction. |
| **Automatic mode** | An LLM exchange the tool performs itself, when `ANTHROPIC_API_KEY` is set. |
| **Build gate** | The `dotnet build` a stage must pass before it counts as done. |
| **Checkpoint** | A stage name recorded in the project's `plan/state.json` after that stage succeeds. |
| **Code pointer** | A named anchor (`searchEngine`, `sorting`, `imageCrudLogic`, …) that the `report` stage resolves to a file, a line range and a snippet in `project-summary.json`. |
| **Design preset** | A named token set (accent, neutrals, fonts, depth) layered over a Bootstrap theme, pinned in `meta.style.preset` or picked per project with the same seed hash as the variation axes (Chapter 12). |
| **Deviation** | A declarative change to a scaffolded controller: `removedActions`, `redirects`, `sorting`, `pagination`, `ownership`. Never code in the spec. |
| **displayName** | The Bulgarian label for a model property. Required on every visible stored property; the enforcement point of the Bulgarian-only rule. |
| **DTO** | A composed read model over several entities. Wired end to end, but no shipped fixture uses one yet, so treat it as unproven. |
| **Feature fragment** | A JSON file under `templates/features/` that injects models (occasionally controllers) when a feature is enabled; the feature-gated pages themselves come from templates. |
| **File contract** | The ADR-005 rule that every LLM exchange is a request file plus a response file, identical in manual and automatic mode. |
| **Gate** | Any check that stops a run from continuing: schema, build, migration, verify. |
| **Generated output** | Everything under `out/`. Disposable, and never edited by hand. |
| **Idempotent (stage)** | Safe to re-run: it detects its own finished work instead of duplicating it. |
| **Install root** | The read-only directory holding the installed program. Contrast *workspace*. |
| **`isLookup` / `isSpecification`** | Model flags that move small models onto the admin "Specifications" page, as a read-only filtered tab or a CRUD tab, instead of separate nav entries. |
| **Language gate** | The visible-text English scan in `verify`; skipped entirely for non-Bulgarian projects. |
| **Lock file** | `project-spec.lock.json`, the expanded spec that every stage from `scaffold` on actually reads. |
| **Manifest check** | `verify`'s comparison of the files the spec promised against the files on disk. |
| **Manual mode** | The LLM path without an API key: the tool writes a request and exits (code 3) until you place a response file. |
| **Ownership** | A per-controller rule (`{ "userProperty": "UserId" }`) enforcing owner-or-admin on mutations, server-side. |
| **`parentRef_<Target>`** | In a seed row, the `refKey` of the parent this row belongs to. |
| **Provenance** | A controller's `generatedBy`: `scaffold`, `template`, or `writtenFromScratch`. |
| **`refKey`** | A seed row's unique symbolic name, used by children instead of a database key. |
| **Resume** | Continuing a run from the first stage not recorded as done. |
| **Seed** | The second input: one array of rows per model, with symbolic references and image phrases. Also the stage that consumes it. |
| **Seed user** | A user created by `IdentitySeeder` from `identity.seedUsers`. Unless the spec sets one, its password is derived from the variation seed and the email; all are written to `logs/<Project>/seed-credentials.txt`, and the banner shows one admin and one ordinary user. |
| **Spec** | `project-spec.json`, the declarative description of the app. |
| **Stage** | One step of the pipeline: an `IStage` with a name and an idempotent `RunAsync`. |
| **`$seedUser:N`** | A placeholder in seed rows meaning "the N-th seeded user", resolved to a real Identity GUID at insert time. |
| **`$primaryModel`** | A token in a feature fragment, replaced by the project's main model when the fragment is merged. |
| **`$variation:axis`** | A placeholder anywhere in a string that expansion replaces with that project's resolved value for the axis. |
| **Unsupported request** | An entry in `meta.unsupportedRequests`, recording something the description asked for that the generator cannot express. |
| **Variation axis** | One of eleven style knobs (naming, DI style, role names, theme, …) resolved once per project from its seed. |
| **Verify** | Stage 8, and the product's definition of done: build, migrate, manifest, boot, `/`, admin login, the declared routes (each must answer 200), anonymous refusal of protected pages, language. |
| **Workspace** | The writable directory a run works in: `Documents\DPS-NN` when installed, the repo root in development. |
