<!-- last-verified: 1b5fea4319805d9a0571619a0318462c896ee4ba -->
# ADR-005: LLM stages are file contracts

**Status**: accepted

**Context**: LLM steps must work with or without an API key, be auditable, and never inject unvalidated output into the pipeline.

**Decision**: every exchange is `prompts/out/<stage>-request.md` -> `prompts/in/<stage>-response.json`. Manual mode: exit code 3 with paste instructions. Automatic mode (`ANTHROPIC_API_KEY`; Sonnet for spec exchanges, Haiku for seed — `LlmModels`, overridable via `DPS_NN_MODEL_SPEC`/`DPS_NN_MODEL_SEED`) writes the same files. Responses are JSON-Schema validated, max 3 attempts, violation reports written, exchanges hash-logged to `genesis.log.jsonl`.

**Consequences**: byte-identical artifact trail in both modes; canned response files double as test fixtures (replay mode).
