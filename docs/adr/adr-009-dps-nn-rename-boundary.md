<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# ADR-009: dps-nn / ДПС-НН rename boundary (user-visible surface only)

**Status**: accepted

**Context**: The project is presented for the software course under the name ДПС-НН (ASCII: dps-nn),
not "genesis". C4 asked to rename genesis -> dps-nn. A literal, whole-repo rename would also touch the
C# namespaces (`Genesis.Core`, `Genesis.Cli`, `Genesis.Tests`), assembly names, csproj file names, the
solution file, and `InternalsVisibleTo`. None of those are ever seen by an end user of the tool or of a
generated project; renaming them is a very large diff (every `using`, every file, the sln, all test refs)
whose only failure mode is breaking the build - for zero user-facing benefit - and it cannot be compile-
verified in the current authoring environment (no .NET SDK).

**Decision**: rename only the **user-visible surface**, leave internal identifiers as `Genesis.*`.

Renamed:
- installed tool command: `<ToolCommandName>` genesis -> `dps-nn`
- console log prefix on every line: `[genesis]` -> `[dps-nn]` (Pipeline + SpecLoader)
- CLI root help text and command-surface header comment -> `dps-nn (ДПС-НН)`
- LLM exchange log filename `genesis.log.jsonl` -> `dps-nn.log.jsonl` (now consistent with the
  already-renamed pipeline log `dps-nn.log.jsonl`)
- README title + architecture doc title + the log-filename mentions

Deliberately NOT renamed (internal, invisible to users):
- C# namespaces / assembly names / csproj + sln file names / `InternalsVisibleTo`
- the internal `GenesisRoot` context field and `GenesisRoot()` helper (identifiers, not output)
- historical ADR bodies (adr-002b/005/008) that mention "genesis" - ADRs are a record of what was
  decided at the time; rewriting them is revisionism, not a rename.

The completion banner (ДПС-НН, Cyrillic) and generated project display strings were already correct.
Log prefixes use ASCII `dps-nn` to avoid any Windows console codepage risk on high-volume stage output;
the human-facing banner stays Cyrillic ДПС-НН.

**Consequences**: users install and invoke `dps-nn`, see `[dps-nn]` logs and a `dps-nn.log.jsonl` file, and
read a ДПС-НН-branded README/banner. The codebase still compiles as `Genesis.*` with no namespace churn.
Tripwire: if the assembly/namespace name ever becomes user-visible (e.g. published as a NuGet package whose
package id matters), revisit and rename assemblies too - as a separate, compile-verified change.
