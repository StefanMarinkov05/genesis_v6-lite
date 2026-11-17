<!-- last-verified: e95fedf -->
# How to add a Scriban template

**Diataxis type:** how-to (task steps). **Source of truth:** `templates/` + `feature-templates/` +
`src/Genesis.Core/Util/TemplateEngine.cs`. For the helper surface and its traps (e.g. the `@@media`
Razor escape), see `docs/explanation/techniques.md` — this doc holds steps only, not the patterns.

## Steps

1. **Place the `.sbn` file** under the matching subfolder of `templates/` — `models/`,
   `controllers/`, `views/`, `identity/`, `project/` — mirroring what it emits. A reusable fragment
   that only activates when a spec's `features.<name>.enabled` is true belongs under
   `feature-templates/<name>.json` instead (see `docs/reference/spec-format.md` "Feature expansion"
   for how those get merged into the spec at `spec-expand` time — that's a spec-fragment file, not a
   Scriban template, even though it's JSON).

2. **Know what's available inside the template.** Every template rendered via `TemplateEngine` sees
   the same globals: `spec`, `meta`, `conventions`, `variation.axes.*`, `effective_theme`,
   `features.<name>` (booleans), `ordered_models`, plus the helper functions `collection_init`,
   `ns_open`/`ns_close`, `clr_type`, `pascal`/`camel`, `subst`. Don't invent a new variation channel
   — if a template needs to vary per-project, it should be reading a `variation.axes.*` value or an
   existing helper, not computing its own randomness (that would break ADR-004's determinism
   guarantee the same way the flagged `ResolveDesignPreset` deviation does — see
   `docs/explanation/techniques.md`).

3. **Render it from a stage.** Call `templateEngine.RenderFile(templatePath, extra)` (or
   `RenderString` for an inline template) from the stage that owns this emission, and write the
   result to the target path under `ctx.ProjectDir`. `extra` is how you pass template-specific data
   beyond the standard globals (e.g. a single model being rendered).

4. **If the template embeds CSS inside Razor markup** (a `<style>` block), escape any `@`-rule as
   `@@` — see the `@@media` trap in `docs/explanation/techniques.md`. This is the one template
   category with a real, previously-hit failure mode.

5. **End its stage in a `dotnet build` gate** (unchanged if you're adding to an existing stage;
   required if you're wiring a new one — see `docs/how-to/add-a-stage.md`).

## Testing it

- `dotnet test --filter TemplateEmissionTests` — the dedicated suite for template rendering. Add a
  case here before running a full pipeline; it's much faster feedback than a full `run --all`.
- A real `run --all` against the FaceNote fixture to confirm the emitted file actually compiles in
  context (a template can pass isolated rendering tests and still fail to compile once real
  variation values and real model data are substituted in).
- If the template is Bulgarian-facing, check it doesn't leak English strings — the `verify` stage's
  English-leak scan (`docs/reference/control-flow.md`) is the final gate, but catching it earlier is
  faster than waiting for a full pipeline run to fail at stage 8.
