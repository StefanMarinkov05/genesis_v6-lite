<!-- last-verified: e95fedf -->
# Techniques — hard-won patterns

**Diataxis type:** explanation. **Source of truth:** the code + `changelogs/*` (distilled here, not
copied — the dated blow-by-blow stays in `plan/decisions.md`; this doc extracts the durable "do it
this way" pattern). This is the highest-value doc in the set: it captures knowledge that is *not*
recoverable from the code alone.

## The Scriban rendering surface

`Util/TemplateEngine.cs` is the **only** place any stage touches Scriban — every template sees
exactly the same helper surface, which is what makes intra-project consistency enforceable instead
of hoped-for:

- `collection_init "Car"` → `"new()"` / `"new List<Car>()"` / `"[]"`, switched on the
  `collectionInit` variation axis. A template never hard-codes a collection-init style.
- `ns_open ns` / `ns_close` → file-scoped (`namespace X;`) vs block-scoped (`namespace X { ... }`)
  namespace tokens, switched on the `namespaceStyle` axis.
- `clr_type prop` → the C# type including nullability (`PropertySpec.Nullable` appends `?`).
- `subst` → `variation.Substitute`, the same function that resolves `$variation:axis` placeholders
  anywhere in the spec (see `docs/reference/spec-format.md`).
- `pascal` / `camel` → simple casing helpers.
- `ordered_models` → a topological sort of `spec.Models` by `manyToOne` relation, so seed emission
  can insert parents before children in one linear pass. This replaced an earlier two-pass
  heuristic that broke the moment a model was simultaneously a child and a parent of other models
  (e.g. `Product` depends on `Brand`/`Category` but is itself depended on by `CartItem`/`OrderItem`)
  — if you're touching seed ordering, don't reintroduce a two-pass approach.

**Trap — never pick anything with `GetHashCode()`.** It is process-randomized in .NET, so a pick
made with it changes between two runs of the same spec. `TemplateEngine.ResolveDesignPreset` did
exactly that for the unpinned design preset until changelog 141; it now uses
`VariationResolver.DeterministicIndex(seed, "designPreset", count)`, the same
`SHA256(seed:axis)` pick as every ADR-004 axis, pinned by a golden-value test
(`Unpinned_design_preset_is_sha256_of_seed`). Any new seed-derived choice goes through
`DeterministicIndex` too.

## Razor `<style>` blocks: escape `@` as `@@`

Razor does **not** treat an inline `<style>` block as raw text the way it does `<script>`. A bare
CSS at-rule like `@media (...)` inside a `.cshtml` `<style>` block gets parsed as a Razor code
transition (`@` + identifier `media`), which then fails to compile (`CS0103: The name 'media' does
not exist`). Any template that embeds CSS containing an `@`-rule inside markup must escape it as
`@@media`. This has only ever bitten `templates/views/design_overlay.sbn` (the one place a
template's *rendered CSS output* gets embedded as a raw string inside another `.cshtml` file) — but
the same escaping applies anywhere else CSS is embedded in Razor markup, not just that file.

## The `dotnet ef` "deps.json does not exist" saga — read before touching `SeedStage`'s migrations

If you see `Build succeeded.` immediately followed by `The specified deps.json [...] does not
exist.` from an automated `dotnet ef` call, **this has already been investigated at length — read
this before proposing a new theory.** Four theories were tried in sequence, each ruled out or only
partially fixing it:

1. **dotnet-ef version mismatch** (changelog 020) — plausible textbook match for the symptom, and
   worth checking first (`dotnet ef --version` must be 10.0.* to match the generated project's EF
   Core packages), but confirmed *not* the cause here: the installed version was already correct.
2. **File-flush/antivirus race** (changelog 022) — an explicit `dotnet build` was added in
   `SeedStage` before any `dotnet ef` call, on the theory that a fresh project folder's just-written
   DLL could race a virus scanner. Reduced but did not eliminate the failure.
3. **MSBuild build-server node reuse** (changelog 023) — `DOTNET_CLI_DISABLE_BUILD_SERVERS=1` set on
   every `dotnet build`/`dotnet ef` invocation, plus a widened retry (up to 3 attempts, increasing
   delays). Still did not eliminate it.
4. **Diagnostics instead of a fourth guess** (changelog 024) — after three theories failed, `
   --verbose` was added to every automated `ef` invocation rather than proposing another blind fix.
   This is the debugging-discipline lesson worth keeping: **after 2-3 failed theories for the same
   symptom, stop guessing and add instrumentation** (this exact rule is now codified in
   `CLAUDE.md`'s changelog discipline).

**Current status:** the issue stopped recurring (`plan/decisions.md` Session 18) before the
`--verbose` instrumentation was ever actually exercised against a real failure — so **no theory was
ever conclusively confirmed as the root cause**. All four mitigations (pre-build,
`DOTNET_CLI_DISABLE_BUILD_SERVERS=1`, the retry backstop, and the `--verbose` flags) remain in place
in `SeedStage.cs` as defense-in-depth; none were removed once the symptom went away. If it
resurfaces, the `--verbose` output is already there — compare it line-by-line against a manual
`dotnet ef migrations add Initial --verbose` run in the same project folder (the "Using application
base" / "Using working directory" lines are where a real difference would show up).

Separately, unique per-run database names (`{Project}_{yyyyMMddHHmmss}`, computed once in
`ScaffoldStage`) were added in the same cluster of work to stop a fresh generation from silently
colliding with a stale LocalDB database left by an older run — unrelated to the race, but landed
alongside it (changelog 023).

## The BG residue-map trap: never blind-replace inside C# identifiers

`identity`'s static EN→BG replacement map (`templates/identity/identity-bg-map.json`, ADR-002)
string-replaces English fragments in scaffolded Razor *markup*. It is a blind, longest-key-first
text replacement with no syntax awareness — which means it will just as happily corrupt a **C#
identifier that happens to contain an English word as a substring** as it will correctly translate
prose. Confirmed in practice (changelog 070): `"Email" → "Имейл"` turned `EmailModel` into
`ИмейлModel` and `ManageNavPages.Email` into `ManageNavPages.Имейл` — both compile errors. There is
no reliable text-only way to distinguish "translatable prose" from "a bare identifier reference" on
the same line, so the fix was to **exclude the whole `Manage/` subtree from the blind map** rather
than attempt fragile heuristics. If you add a new Identity page group to `identity.modules`, check
whether its scaffolded code-behind or markup contains identifiers built from words the residue map
would match, and exclude that subtree rather than trying to make the map smarter.

## Two separate `_ViewImports.cshtml` layers

A new `using` that a Razor page needs (e.g. a new `Infrastructure` type referenced from a Manage
page) must be added to **both** of these, because they are genuinely separate cascades, not one:

- The per-MVC-area ones under `Areas/{Admin,Client}/Views/_ViewImports.cshtml` — generated by
  dps-nn itself.
- The Identity Razor Pages one under `Areas/Identity/Pages/_ViewImports.cshtml` — scaffolder-
  generated, a completely separate convention, and it cascades down *that* folder tree only. A page
  under `Areas/Identity/Pages/Account/Manage/` needs the top-level Identity one specifically, not
  either MVC-area file.

Missing either one produces a `using`-not-found compile error scoped to exactly the pages under that
cascade — confirmed the hard way in changelog 070.

## Pipeline stage-ordering: a helper must exist before the build-check that needs it

Every code-emitting stage ends in a `dotnet build` gate (`docs/reference/control-flow.md`). If stage
N scaffolds or emits something that **references** a type your work is adding, and that type is
currently emitted by a *later* stage, stage N's own build gate fails — the type doesn't exist yet at
that point in the pipeline, even though it will by the time the whole run finishes. This bit
`ProfileHash.cs` directly (changelog 070): it was originally emitted in
`EmitControllersViewsStage` (stage 6), but `IdentityStage` (stage 5) scaffolds and build-checks
`_ManageNav.cshtml`, which references `ProfileHash`. The fix was moving the emission earlier, into
`IdentityStage` itself, before its own scaffold call — not weakening the build gate. When adding a
new cross-cutting helper type, emit it in (or before) the earliest stage that will reference it, not
wherever feels most topically related.

## "You must install .NET to run this application" from a global tool, only when installed

`dotnet-aspnet-codegenerator` and `dotnet-ef` are framework-dependent apphosts: they find the
runtime through `DOTNET_ROOT`, else the registered/default install location. `dotnet run` sets
`DOTNET_ROOT` for the app it launches, so from a checkout they always worked. The installed,
self-contained `dps-nn.exe` has no `dotnet` parent, and with .NET installed anywhere but the
default location (a per-user install) `identity` failed with "App host version … .NET location:
Not found". `DotnetHost.EnsureDotnetRoot` (called first thing in `Program.cs`) now sets it from the
`dotnet` on `PATH` when unset. Any future child process that is itself a .NET apphost inherits the
fix; one that needs a *different* runtime must set its own. (ADR-014, changelog 134.)

## Where to look next

For *why* the pipeline is staged and verify-gated this way (which is why these build-gate-ordering
traps exist at all): `docs/explanation/architecture-decisions.md`. For the dated, blow-by-blow
history these patterns were distilled from: `plan/decisions.md` — check it before proposing a fix
for a recurring symptom, per CLAUDE.md's changelog discipline.
