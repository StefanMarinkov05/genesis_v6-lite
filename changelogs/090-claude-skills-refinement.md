# 090 — Refined the spec/seed-authoring Claude Skills against tonight's real work

**Type:** Feature / change (documentation-as-tooling, not generator code)
**Model:** Sonnet, discussed via `AskUserQuestion` rather than assumed — Stefan explicitly wanted
to talk through the design before anything was written, per the brainstorming discipline.

## Summary

`utils/claude-skills/dps-nn-spec-authoring` and `dps-nn-seed-authoring` (changelog 077) predated
essentially all of tonight's session (A4 image pipeline, A5 Спецификации consolidation, the
single-property-Details rule, the credential-display convention) — neither skill knew any of it
existed. Refined both against real, shipped generator behavior rather than re-deriving from
scratch, after a real discussion (7 `AskUserQuestion` rounds) locking in the actual design:

### `dps-nn-spec-authoring` — now conversational by default, and materially expanded

- **Workflow flip**: was strictly "output only JSON, no commentary" — now discusses the domain,
  flags real judgment calls, and converges BEFORE emitting JSON. Terse one-shot output becomes an
  explicit opt-in ("just give me the spec"), not the default.
- **New: model taxonomy section** — codifies the `isSpecification` (atomic reference model → CRUD
  tab) vs `isLookup` (join/junction model → read-only tab) rule from A5, applied automatically
  (not asked about — this was Stefan's explicit call), including the `redirects` boilerplate
  needed when hand-authoring a consolidated sub-controller.
- **New: action-customization section** — `removedActions` (single-property models never need
  Details; Order-style client-only-origin models never need admin Create),
  `ownership` (owner-only Edit/Delete guard), `redirects` — each with the real trigger condition,
  not just the field's existence.
- **New: images section** — draws the line between a plain `ImagePath` string (lightweight,
  optional, no management UI) and the full `images` block (dedicated table + ImageService +
  admin/carousel UI), AND flags the real, just-discovered gap: `mode: "single"` works correctly
  server-side but the admin/client UI templates don't yet visually differentiate it from
  `"multiple"` (see the new tasks.md entry below) — the skill now tells the truth about this
  instead of presenting `"single"` with false confidence.
- **New: DTO section** — DTOs are fully wired into the pipeline but no real project has ever
  exercised one (every real fixture's `dtos: []` is empty). Skill now proposes them when the shape
  genuinely fits, but explicitly flags them as unproven and recommends a real verify pass.

### `dps-nn-seed-authoring` — two new hard "always ask" rules, stays terse otherwise

- **Always ask the admin/non-admin seed-user count before generating** — never assumes a default
  (not even the CLI banner's own "1 admin + 1 non-admin" display convention, which is unrelated —
  that's a *display* rule, not a *seeding* rule).
- **Ask about role-name language ONLY if the spec's roles look Latin-script** — narrowed from "ask
  about everything" to one specific, real check; Bulgarian-by-default already covers everything
  else per the existing rule 1.
- Kept the terse-by-default workflow (deliberately NOT matched to spec-authoring's new
  conversational default — seed generation is mechanical once the required questions are
  answered, per Stefan's explicit call).
- Clarified the existing "always set imageKeyword" rule's rationale in terms of the new A4
  persistent download cache (a real, specific keyword per row is what makes the cache worth
  having — an empty/generic one wastes the fetch).

## Real generator gap found while designing this (not fixed here, logged as a task)

Confirmed by reading the actual templates: `admin_product_images.sbn` (the only admin
image-management view, emitted for every image-bearing model regardless of `mode`) always renders
a multi-file-upload gallery UI, even for `mode: "single"` models; `product_details.sbn`'s carousel
never branches on mode either. `ImageService` itself IS mode-aware server-side. In practice only
`Product` uses the `images` block today, always as `"multiple"` — so `"single"` mode is exactly as
untested end-to-end as DTOs. Logged in `tasks.md` (Category 3) rather than fixed in this session,
per explicit instruction to keep this session's focus on the skills.

## Verification

Both files are pure Markdown (Claude Skill instructions, no code) — no build/test applies. Every
factual claim in the new content was verified against the real, current source before writing
(`ImageService.Save{Model}ImageAsync`'s single-mode delete-then-add logic,
`admin_product_images.sbn`'s unconditional `multiple` attribute, `EmitControllersViewsStage`'s
emission gate showing zero mode branching, the real `SpecificationsController`/`isSpecification`/
`isLookup` shapes from changelog 088, the DTO wiring in `SpecLoader.cs`/`BuildStages.cs`/
`VerifyAndReportStages.cs` confirming it's real code, not a documented-but-dead field).

## Also this session

Saved a standing memory (`feedback_task_list_engine_and_eta`): whenever Stefan hands over a task
list, proactively state the recommended model/effort/thinking + an estimated completion time,
without waiting to be asked.

## Affected files

- `utils/claude-skills/dps-nn-spec-authoring/SKILL.md` (materially expanded)
- `utils/claude-skills/dps-nn-seed-authoring/SKILL.md` (two new hard rules)
- `utils/claude-skills/README.md` (summaries updated to match)
- `tasks.md` (new task: `images.mode: "single"` UI gap)
