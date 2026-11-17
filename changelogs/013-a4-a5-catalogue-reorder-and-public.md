# A4 catalogue reorder + A5 public catalogues

**Type:** Feature / change
**Model:** Opus — scaffold-adjacent controller features from the same pre-Claude-Code build era.

## Summary
Added drag-free up/down reordering of catalogue items (a `Position` column, normalized then swapped on each move) and a public/anonymous catalogue browsing mode (an `IsPublic` toggle per catalogue, with a public listing + public details page). Both are plain model properties, so no schema change was needed.

## Techniques
- Owner-only `MoveItem`/`TogglePublic` actions on the existing template-authored CataloguesController
- Position-ordered views with disabled-state-aware up/down buttons
- New anonymous `Public()`/`PublicDetails()` actions + 2 new views + a nav link
- The new public route added to the B4 language-gate scan so it's covered by the English-leak check too

## Affected files
- `feature-templates/catalogues.json`
- `templates/controllers/catalogues_controller.sbn`
- `templates/views/catalogues_index.sbn`
- `templates/views/catalogues_public.sbn`
- `templates/views/catalogues_public_details.sbn`
- `templates/views/layout.sbn`
- `src/Genesis.Core/Stages/EmitControllersViewsStage.cs`
- `src/Genesis.Core/Stages/VerifyAndReportStages.cs`
