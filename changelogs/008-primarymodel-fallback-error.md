# Pipeline error: '$primaryModel' relation target not a declared model

**Type:** Error fix
**Model:** Opus — cross-stage bug (two code paths computing the same concept differently) - needed reasoning across multiple files to find the shared root cause.

## Where
`src/Genesis.Core/Stages/SpecStages.cs`, `SpecExpandStage`'s feature-fragment token substitution.

## How it happened
When a feature (e.g. `rating`) is enabled WITHOUT an explicit `"model"` key in its config block (legal per the schema), the fragment's `$primaryModel` placeholder was never substituted, so the literal string `"$primaryModel"` ended up as a relation target in the expanded spec - which semantic validation correctly rejected.

## Why
Two separate code paths computed "the feature's primary model" with different rules. `EmitControllersViewsStage.FeaturePrimaryModel` (used at emit time) already had a fallback: if no explicit model is set, use the first model with an images block. `SpecExpandStage`'s separate, earlier substitution logic had no such fallback - it only substituted when `cfg.model` was present. FaceNote's `rating` feature has no explicit model (catalogues/orders happen to set one, so they never exposed the gap).

## What it affects
Any spec that enables a feature needing `$primaryModel` without giving that feature an explicit `"model"` key. Blocks `spec expand` entirely with a semantic-validation exception.

## How to reproduce
Run `spec expand` (or `run --all`) against a spec where an enabled feature (e.g. `rating`) has `{"enabled": true}` with no `"model"` key, and no OTHER model in the spec happens to already match by coincidence. Fails with: `Model Review: relation target '$primaryModel' is not a declared model.`

## How to fix
Extracted one shared `SpecExpandStage.ResolvePrimaryModel(spec, cfg)` with the fallback, and made `EmitControllersViewsStage.FeaturePrimaryModel` delegate to it instead of duplicating the logic, so the two code paths can no longer drift apart. Added 2 regression tests against the real FaceNote fixture.
