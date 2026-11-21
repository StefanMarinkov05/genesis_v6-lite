<!-- last-verified: b987364 -->
# ADR-013: Agent mode — the LLM authors and repairs the spec, never the generated code

**Status**: accepted (changelog 132)

**Context**: Running dps-nn meant writing (or LLM-drafting in a chat) a `project-spec.json`,
feeding it to `run --all`, reading the failure, fixing the spec by hand and running again. The
pipeline already had every tool an agent needs — a schema, semantic validation, a resumable
generator, a verify gate — but no loop that closes the feedback cycle. Two ways to close it were
on the table: let the model **edit the generated C#/Razor** when something fails, or let it edit
**only the spec** and treat anything the spec cannot express as a generator gap.

**Decision**: `dps-nn create` runs an autonomous loop (`AgentRunner`): draft the spec from a
free-text description → validate (schema + semantic rules) → run the pipeline → on failure,
condense the error and ask the model for a corrected **spec**, wipe the generated project, run
again — at most `--max-repairs` rounds (default 3). The model never touches generated code.
Requests the generator cannot satisfy are recorded in `meta.unsupportedRequests`
(`{ request, reason }`) instead of being forced into the spec, and surface in `agent-report.json`
as the backlog for new templates/features. Failures caused by the machine (missing SDK, outdated
`dotnet-ef`, no LocalDB, rejected API key) stop the loop immediately with the fix — no spec edit
can repair them. Every exchange goes through `LlmBridge`, so ADR-005's file contract, schema
validation and hash log apply unchanged. Default models: Sonnet for spec draft/repair (reasoning
over a large schema), Haiku for seed data (bulk, cheap) — overridable via `DPS_NN_MODEL_SPEC` /
`DPS_NN_MODEL_SEED`.

**Consequences**:
- Every generated app is still 100% template output, so the verify gate, the Bulgarian-UI
  guarantee and the design overlay keep holding — an agent that patched code would make each app
  a one-off that no template change could ever fix.
- Token cost per repair is one spec round-trip, not a read of the whole generated codebase.
- Capabilities grow by extending the generator (once, for everyone), driven by the
  `unsupportedRequests` backlog — the agent's limit is visible, never silent.
- The loop is pure orchestration over delegates and is unit-tested without dotnet or an API key.

Full design: `docs/explanation/agent-mode.md`.
