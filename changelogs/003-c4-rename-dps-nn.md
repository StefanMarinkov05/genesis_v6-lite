# C4: rename genesis -> dps-nn (user-visible surface only)

**Type:** Feature / change
**Model:** Opus — scoping judgement call (user-visible rename only, explicitly NOT touching C# namespaces) - an architectural boundary decision.

## Summary
Renamed the tool's user-visible surface from "genesis" to "dps-nn" (ДПС-НН): installed command name, console log prefix, CLI help text, the pipeline/LLM log filename, and the README/architecture doc titles. Deliberately did NOT rename C# namespaces/assemblies/the .sln file/the internal GenesisRoot identifier - that's a large, purely-internal diff with zero user-facing benefit and no way to compile-verify it at the time. Recorded as ADR-009 so the boundary is a documented decision, not an inconsistency.

## Techniques
- Renamed `<ToolCommandName>`, `[genesis]` -> `[dps-nn]` log prefix, CLI header/help text
- Renamed `genesis.log.jsonl` -> `dps-nn.log.jsonl`
- Wrote ADR-009 documenting the scope boundary

## Affected files
- `src/Genesis.Cli/Genesis.Cli.csproj`
- `src/Genesis.Cli/Program.cs`
- `src/Genesis.Core/Pipeline/Pipeline.cs`
- `src/Genesis.Core/Spec/SpecLoader.cs`
- `src/Genesis.Core/Llm/LlmBridge.cs`
- `README.md`
- `docs/adr/adr-009-dps-nn-rename-boundary.md`
