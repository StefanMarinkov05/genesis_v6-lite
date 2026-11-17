# 109 — Log tag: "нишка Кольо" instead of bare "Кольо"

**Type:** Bug fix / small update
**Model:** Sonnet, low effort, thinking off — one-line log-format change.

## Background

Stefan asked that the parallel-worker log prefix (issue #5 / changelog 051's "Bulgarian thread
names") read as "нишка Пешо" (naming the thread) rather than a bare "Пешо" — the noun was missing,
just the name alone.

## What shipped

`StageContext.Tag` (`Pipeline.cs`) now prefixes Bulgarian-crew log lines with "нишка " before the
worker name — e.g. `нишка Кольо записа Models/Product.cs` instead of `Кольо записа
Models/Product.cs`. Gated on which crew produced the name (`Array.IndexOf(ParallelEmit.
WorkerNames, worker)`), not applied unconditionally: the English crew
(`DPS_NN_LOG_LANGUAGE=en`, changelog 053's "Bubba/Chuck/Duke/Rusty/Slim") keeps its own bare-name
form — prefixing "нишка" onto an English name would mix languages mid-line, the same failure
class changelog 070/094 already caught elsewhere (a blind transform colliding with a value that
must render in its own register).

## Verified

`dotnet build` clean, `dotnet test` 121/121 green. Live-checked both paths against a real
`emit-models` run (not just trusting the unit suite): Bulgarian crew now shows `нишка Кольо
записа ...` / `нишка Мишо записа ...` / etc. across all 5 workers; English crew
(`DPS_NN_LOG_LANGUAGE=en`) shows the unchanged bare form `Chuck записа ...` / `Bubba записа ...`
with no "нишка" bleeding in.

## Affected files

- `src/Genesis.Core/Pipeline/Pipeline.cs` (`Tag` method)
