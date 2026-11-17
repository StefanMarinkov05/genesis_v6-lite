// FILE-MAP: which Claude model each LLM exchange uses — Sonnet for spec drafting/repair (reasoning), Haiku for bulk seed rows; both overridable via env.
namespace GenesisV6.Core.Llm;

/// <summary>
/// Model routing for automatic-mode exchanges. Reasoning-heavy work (drafting a spec from a
/// description, repairing it from a build/verify failure, the expand review) goes to Sonnet;
/// high-volume seed rows go to the cheaper, faster Haiku. Override per deployment with
/// <c>DPS_NN_MODEL_SPEC</c> / <c>DPS_NN_MODEL_SEED</c>.
/// </summary>
public static class LlmModels
{
    public const string DefaultSpec = "claude-sonnet-5-5";
    public const string DefaultSeed = "claude-haiku-5-5";

    public static string Spec => Env("DPS_NN_MODEL_SPEC") ?? DefaultSpec;
    public static string Seed => Env("DPS_NN_MODEL_SEED") ?? DefaultSeed;

    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v.Trim() : null;
}
