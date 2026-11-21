# Design principles

Five principles explain most of the decisions in this codebase. When a change feels like it fights
the architecture, one of these is usually the reason.

## 1. Determinism is the default; non-determinism must be a file

Two runs of the same lock file must produce the same app, except for LLM content and EF migrations.
In practice:

- variation is a **hash**, never `Random` or `GetHashCode()` (process-randomized in .NET) — the
  design preset included;
- every template reads the resolved profile instead of rolling its own;
- the LLM's output is a **file** the pipeline validates, so "non-deterministic" collapses to
  "a recorded input".

A diff therefore means something. If two runs differ, something real changed, and a bug reproduces
on the second run.

## 2. Determinism must not look like cloning

The same generator producing twenty demo apps that read identically is a bad generator. So one
project's *style* is varied by a seed, one value per axis:

| Axis | Options |
|---|---|
| `collectionInit` | `new()` · `new List<T>()` · `[]` |
| `adminRoleName` | Administrator · Admin · Owner |
| `userRoleName` | the Bulgarian word for "User" · User · Client |
| `seedPasswordPattern` | three patterns, all satisfying Identity's default policy |
| `namespaceStyle` | file-scoped · block-scoped |
| `diRegistrationStyle` | chained · separate statements |
| `stringInterpolationVsConcat` | interpolation · concatenation |
| `viewCommentDensity` | sparse · moderate |
| `bootstrapTheme` | eight Bootswatch themes |
| `navbarStyle` | three navbar conventions |
| `buttonShape` | pill · rounded |

Eleven axes, one value each, chosen as `SHA256(seed + ":" + axis)`. A spec may pin any axis
explicitly; unpinned axes vary across projects and never within one. Visual identity beyond
Bootstrap comes from a design-preset overlay (ADR-008), so "look" is a token choice rather than
per-template styling. When `meta.style.preset` is not pinned, the preset is picked with the same
hash under the name `designPreset`, so it varies across projects and never between runs of one
spec. (It used to be picked with `GetHashCode()`, which .NET randomises per process — fixed in
changelog 141, with a golden-value test.)

The use case behind these axes is concrete: one developer often delivers several projects to the
same school, sometimes to the same class, and those submissions are compared. Without variation
every student would receive the same app, which is useless as a graded artifact and
unfair to the students who submitted it.

What the axes buy is limited and worth stating: style, naming, presentation, content and which
features are composed all change, while page structure stays the same, because one template set
produces it. That is enough to make two submissions visibly different; it is not enough to make
them structurally different apps.

## 3. Every stage is a checkpoint, and every stage is idempotent

A run costs minutes of `dotnet build`, EF migrations and LLM calls. It will be interrupted — by a
crash, a missing tool, a manual LLM hand-off. So:

- the runner records completion per stage and can resume;
- each stage detects its own finished work (an existing `.csproj`, valid seed data, applied
  migrations) instead of duplicating it;
- `verify` is never marked done on failure, so a fixed problem is retried, not skipped.

Idempotency is a contract on the *stage* (`IStage.RunAsync`), because the runner knows nothing about
what a stage does.

## 4. Fail loud, fail early, fail with the fix

- **Loud:** a regex deviation that cannot find its anchor throws rather than silently leaving the
  scaffolded code unchanged (ADR-006).
- **Early:** `spec-expand` validates the input before any generation; `verify` runs the cheap
  manifest check before booting the app.
- **With the fix:** `doctor` prints the command that repairs each missing prerequisite; the agent
  loop stops on environment problems and returns that same fix instead of burning repairs; a build
  failure's condensed form keeps the error lines and drops the restore noise.

The alternative — a soft warning that scrolls past — is how a generation run ends with a subtly wrong
app.

## 5. One implementation, many front ends

The CLI is the pipeline. The GUI spawns it and reconstructs progress from `dps-nn.log.jsonl`; it does
not call stage classes and does not re-implement resume. Agent mode calls the same `run --all`
machinery, minus interactive pickers. Consequences:

- a behavior fix lands once;
- the GUI can only be as correct as the log, so the log format is a contract;
- anything the GUI needs to know must be observable in a file.

## The layering rule

Dependencies point **inward**, and there is only one direction across modules:

```
Genesis.Cli  ->  Agent, Stages, Diagnostics, Pipeline   (the composition root)
Agent        ->  Llm, Spec          (reaches the pipeline only through a delegate)
Stages       ->  Pipeline, Llm, Util, Variation, Spec
Diagnostics  ->  Pipeline
Pipeline     ->  Variation, Spec
Util         ->  Variation, Spec
Variation    ->  Spec
Spec, Llm    ->  (nothing inside the core)
```

- `Spec`, `Variation`, `Util` depend on nothing in the pipeline.
- `Llm` knows the file contract, not the stages.
- `Stages` may use all of the above; nothing but the CLI uses `Stages`. The agent runs the pipeline through a delegate the CLI hands it, which is what makes its loop testable without `dotnet`.
- `Diagnostics` is a leaf: it checks the world, it does not generate.

A new abstraction that makes `Spec` or `Util` import a stage is a sign the change belongs in the
stage instead.

## Extension points, ranked by cost

| Want to… | Add | Cost | Risk |
|---|---|---|---|
| change what an emitted file contains | a template edit | low | must keep the build gate green |
| add a whole new emitted file | a template + one stage call | low | manifest check will demand it exists |
| add a reusable capability (models + pages) | a `templates/features/<name>.json` + feature-gated emission | medium | `$primaryModel` and view wiring must match |
| add a stage | an `IStage` + CLI wiring + docs | medium | ordering and idempotency are yours |
| add a new spec capability | schema + POCO + semantics + emitting stage + doc | high | four places must agree |
| change how the LLM is used | prompt/schema in `Llm` or the stage | medium | keep the file contract intact |
