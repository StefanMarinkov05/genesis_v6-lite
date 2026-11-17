// FILE-MAP: agent mode's ISpecAuthor over the ADR-005 LLM file contract — builds the draft/repair prompts from the spec-authoring guide + a verified example spec.
using System.Text.Json.Nodes;
using GenesisV6.Core.Llm;
using Json.Schema;

namespace GenesisV6.Core.Agent;

/// <summary>
/// Drafts and repairs specs through <see cref="LlmBridge"/>, so every agent exchange leaves the
/// same request/response/hash trail as the existing LLM stages (ADR-005) and gets the same
/// schema-validated retries. The authoring guide is the repo's own Claude skill
/// (utils/claude-skills/dps-nn-spec-authoring) — one source for humans-in-a-chat and the agent.
/// </summary>
public sealed class LlmSpecAuthor(LlmBridge llm, JsonSchema specSchema, string authoringGuide, string exampleSpecJson)
    : ISpecAuthor
{
    public const string DraftStage = "agent-spec-draft";
    public const string RepairStage = "agent-spec-repair";

    // The guide is written for a conversation (ask first, then emit). The agent has nobody to ask,
    // so this preamble overrides exactly those parts and adds the capability-gap contract.
    private const string AgentPreamble = """
        You are the spec author inside dps-nn's autonomous agent mode. Nobody is available to
        answer questions: make every judgment call yourself, following the guide below, and
        output the final project-spec JSON directly.

        Rules for this mode:
        - Decide conventions.timestamps yourself (sensible defaults for the domain).
        - Only use what the JSON Schema and the generator support. When the description asks for
          something the spec cannot express (a capability the generator does not have), do NOT
          invent fields or misuse existing ones: leave it out and record it in
          meta.unsupportedRequests as { "request": <what was asked>, "reason": <missing capability> }.
        - Keep the scope a sample app: 5-10 core models unless the description clearly needs more.
        - Give every model a seeding.rowsPerModel entry with modest counts (5-20 rows), so the
          seed exchange stays well within one response.
        """;

    public async Task<string> DraftAsync(string description, CancellationToken ct)
    {
        var instructions = $"""
            {AgentPreamble}

            Draft a complete project-spec for the project described in the input data.

            {Guide()}
            """;
        var doc = await llm.ExchangeAsync(DraftStage, instructions, specSchema, new { description }, ct);
        return Pretty(doc.RootElement.GetRawText());
    }

    public async Task<string> RepairAsync(string description, string currentSpecJson, AgentFailure failure, CancellationToken ct)
    {
        var instructions = $"""
            {AgentPreamble}

            The spec in the input data was run through the generator and FAILED at stage
            "{failure.Stage}". Return the complete corrected spec. Change only what the error points
            at; keep everything else identical. You may only change the spec - generated code is
            off-limits. If the failure comes from a feature the generator cannot handle, remove that
            feature from the spec and record it in meta.unsupportedRequests instead of retrying it.

            {Guide()}
            """;
        var input = new
        {
            description,
            failedStage = failure.Stage,
            error = failure.Message,
            currentSpec = JsonNode.Parse(currentSpecJson),
        };
        var doc = await llm.ExchangeAsync(RepairStage, instructions, specSchema, input, ct);
        return Pretty(doc.RootElement.GetRawText());
    }

    private string Guide() => $"""
        ## Authoring guide

        {StripFrontmatter(authoringGuide)}

        ## Reference: a complete spec that passes every stage of the generator

        {exampleSpecJson}
        """;

    internal static string StripFrontmatter(string markdown)
    {
        if (!markdown.StartsWith("---")) return markdown;
        var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        return end < 0 ? markdown : markdown[(end + 4)..].TrimStart();
    }

    private static string Pretty(string json) => JsonNode.Parse(json)!.ToJsonString(Spec.ProjectSpec.JsonOpts);
}
