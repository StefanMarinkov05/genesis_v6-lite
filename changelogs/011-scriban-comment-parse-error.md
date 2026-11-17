# Build crash: multi-line comment in design_overlay.sbn / landing.sbn parsed as code

**Type:** Error fix
**Model:** Opus — root-cause diagnosis of a template-engine parsing bug invisible without a real render.

## Where
`templates/views/design_overlay.sbn` (line 2) and, found during a sweep, `templates/views/landing.sbn`.

## How it happened
Both had a multi-line prose comment written inside a single `{{~ # ... ~}}` block. Scriban's `#` is a SINGLE-LINE comment - it only comments to the end of that line, not to the block's closing `~}}`. The continuation lines were parsed as Scriban CODE, and crashed with a syntax error the first time either template was ever actually rendered.

## Why
Whoever wrote the comment (an earlier, uncompiled session) assumed `#` worked like a C-style block comment. It doesn't; each line needs its own `#`. Never caught because nothing had rendered these templates for real before this session.

## What it affects
Blocks the `emit-controllers-views` stage entirely for any project using the design-overlay system (i.e. every project, since it's always emitted) or hitting the landing page template.

## How to reproduce
Run the pipeline far enough to reach `emit-controllers-views`. Fails with: `Invalid token found ','. Expecting <EOL>/end of line` at the design_overlay.sbn comment's second line.

## How to fix
Gave every line of both comments its own `#`. Swept every other `.sbn` template for the same pattern (carefully distinguishing it from the legitimate, common "one `#` comment line then real code" idiom used throughout the codebase) - confirmed no other instances remain.
