# codebase-map (Opus session, comprehension pass)

## What it is
`genesis` / ДПС-НН: a CLI that turns one `project-spec.json` into a complete, buildable,
seeded ASP.NET Core 10 MVC + EF Core app with Bulgarian-only UI. 9-stage resumable pipeline.
Target framework net10.0. **No .NET SDK / no NuGet available in this container** → nothing here
is compile-verified; brace/quote balance is the only local check (same as prior sessions).

## Load-bearing modules
- `Pipeline/Pipeline.cs` — StageContext, StateStore (resumable), PipelineRunner (timings + jsonl log
  `dps-nn.log.jsonl`), Proc (dotnet wrapper, fail-loud).
- `Stages/EmitControllersViewsStage.cs` — the crux. 3 controller provenances:
  scaffold (regex transforms over aspnet-codegenerator output), template (Scriban), written-from-scratch
  (UsersController). All transforms are idempotent (marker check) + loud (throw on missing anchor).
- `Stages/VerifyAndReportStages.cs` — VerifyStage (THE gate: build→ef→manifest→boot→http→EN-leak scan),
  ReportStage (project-summary.json for downstream doc tooling).
- `Util/TemplateEngine.cs` — single Scriban surface + helpers.
- `Spec/ProjectSpec.cs` + `SpecLoader.cs` — POCOs, JSON-schema + semantic validation.

## Controller provenance (decides feature-work risk)
- **template controllers** (cart, catalogues, posts, follows, reviews) are 100% authored by
  `templates/controllers/*.sbn` → I can read the exact output and extend deterministically → LOW risk blind.
- **scaffold controllers** depend on unseen aspnet-codegenerator output, calibrated by regex → HIGH risk blind
  (needs a live scaffold to recalibrate, per state.md "Next, locally").

## State/code drift found
- state.md "Session 2" lists C1 (per-stage timings + dps-nn.log.jsonl) and C2 (completion banner ДПС-НН)
  as done. **Confirmed present**: C1 in `PipelineRunner.RunAllAsync`, C2 in `Program.PrintCompletionBanner`.
  (My initial suspicion they were missing was wrong — banner lives in Program.cs, not Pipeline.cs.)
- README + architecture.md describe AdminCannotDeleteSelf and an EN-leak scan as present; the EN-leak scan
  exists but only over 3 fixed pages (the real B4 gap). AdminCannotDeleteSelf lives in users_controller.sbn (see below).

## This session's changes
- **B4**: VerifyStage EN-leak gate widened from 3 fixed pages to all discovered routes
  (all cardGrids anon + all adminTables + enabled Client-feature routes, authed), best-effort fetch.
- **A6**: order details — new action on cart_controller.sbn + cart_order_details.sbn view + emission wiring + list link.
- **C4 (bounded)**: user-visible genesis→dps-nn naming only; C# namespaces deliberately NOT renamed (ADR-009).

## Deferred (need a live scaffold to calibrate) — handover spec at end of session
A1 client product CRUD + cart button on cards; A4 catalogue reorder/Position; A5 public catalogues;
A7/A11 admin delete everywhere + self-guard finish; A8 post images + author delete; A9 user page + linking.
