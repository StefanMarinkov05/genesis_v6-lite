// FILE-MAP: agent mode core — the draft → validate → run → (repair spec on failure) loop behind `dps-nn create`, plus environment-error triage and error condensing for the LLM.
using System.Text.RegularExpressions;
using GenesisV6.Core.Llm;
using GenesisV6.Core.Spec;

namespace GenesisV6.Core.Agent;

/// <summary>What failed, where. <see cref="Stage"/> is a pipeline stage name or "spec-validate".</summary>
public sealed record AgentFailure(string Stage, string Message);

/// <summary>One pass through the loop, recorded in agent-report.json.</summary>
public sealed record AgentAttempt(int Number, string Outcome, string? FailedStage, string? Error);

public sealed record AgentResult(bool Success, string? StopReason, IReadOnlyList<AgentAttempt> Attempts);

/// <summary>Thrown by the pipeline delegate so the loop knows WHICH stage failed.</summary>
public sealed class AgentStageException(string stage, Exception inner)
    : Exception($"{stage} failed: {inner.Message}", inner)
{
    public string Stage { get; } = stage;
}

/// <summary>
/// The LLM side of the loop. The only things it may change are the spec (ADR-013: repair edits
/// spec + seed only, never generated code), so both operations return a full spec document.
/// </summary>
public interface ISpecAuthor
{
    Task<string> DraftAsync(string description, CancellationToken ct);
    Task<string> RepairAsync(string description, string currentSpecJson, AgentFailure failure, CancellationToken ct);
}

/// <summary>
/// Autonomous mode (ADR-013). Turns a free-text description into a running app:
/// <list type="number">
/// <item>draft a spec (skipped when one already exists — resume-friendly, same as SpecBuildStage)</item>
/// <item>validate it (schema + semantic rules) and run the pipeline</item>
/// <item>on failure: stop immediately if the cause is the machine (missing SDK/tool/LocalDB) —
///       no spec edit can fix that and retrying only burns tokens — otherwise hand the condensed
///       error to the author, write the repaired spec, reset the generated output, and go again</item>
/// </list>
/// Bounded by <paramref name="maxRepairs"/>. Pure orchestration: every side effect (pipeline run,
/// output reset, spec validation) is a delegate, so the loop is unit-testable without dotnet.
/// </summary>
public sealed class AgentRunner(
    ISpecAuthor author,
    Func<string, CancellationToken, Task> runPipeline,
    Action<string> validateSpec,
    Action<string> resetGeneratedOutput,
    Action<string> log,
    int maxRepairs = 3)
{
    public async Task<AgentResult> RunAsync(string description, string specPath, CancellationToken ct = default)
    {
        var attempts = new List<AgentAttempt>();

        if (!File.Exists(specPath))
        {
            log("agent: drafting spec from the description...");
            try { await File.WriteAllTextAsync(specPath, await author.DraftAsync(description, ct), ct); }
            catch (Exception ex) when (ex is not LlmBridge.LlmPendingException and not OperationCanceledException)
            {
                attempts.Add(new AgentAttempt(0, "failed", "agent-spec-draft", Condense(Describe(ex))));
                return new AgentResult(false, $"could not draft a valid spec: {ex.Message}", attempts);
            }
            log($"agent: spec drafted -> {specPath}");
        }
        else log($"agent: reusing existing spec {specPath}");

        for (var n = 1; ; n++)
        {
            AgentFailure? failure = null;
            try { validateSpec(specPath); }
            catch (Exception ex) { failure = new AgentFailure("spec-validate", Describe(ex)); }

            if (failure is null)
            {
                try
                {
                    await runPipeline(specPath, ct);
                    attempts.Add(new AgentAttempt(n, "success", null, null));
                    return new AgentResult(true, null, attempts);
                }
                catch (LlmBridge.LlmPendingException) { throw; } // manual-paste flow: not a spec problem
                catch (OperationCanceledException) { throw; }
                catch (AgentStageException ex) { failure = new AgentFailure(ex.Stage, Describe(ex.InnerException!)); }
                catch (Exception ex) { failure = new AgentFailure("pipeline", Describe(ex)); }
            }

            var condensed = Condense(failure.Message);
            attempts.Add(new AgentAttempt(n, "failed", failure.Stage, condensed));
            log($"agent: attempt {n} failed at {failure.Stage}");

            if (EnvironmentProblem(failure.Message) is { } envHint)
                return new AgentResult(false, $"environment problem, not a spec problem: {envHint}", attempts);
            if (n > maxRepairs)
                return new AgentResult(false, $"still failing after {maxRepairs} repair round(s)", attempts);

            log($"agent: repair round {n}/{maxRepairs} - asking the model to fix the spec...");
            var current = await File.ReadAllTextAsync(specPath, ct);
            var repaired = await author.RepairAsync(description, current, failure with { Message = condensed }, ct);
            resetGeneratedOutput(current); // old spec -> the output it produced (project name may change)
            await File.WriteAllTextAsync(specPath, repaired, ct);
        }
    }

    // Failures caused by the machine, not the spec. Matched on the raw message (before condensing)
    // so a long build log can't push the telltale line out of view. Each entry carries the fix.
    private static readonly (Regex Pattern, string Hint)[] EnvironmentPatterns =
    [
        (new(@"deps\.json does not exist", RegexOptions.IgnoreCase),
            "dotnet-ef is too old for net10.0 - run: dotnet tool update -g dotnet-ef --version 10.0.*"),
        (new(@"dotnet-aspnet-codegenerator.*(not found|could not execute)|could not execute because the specified command or file was not found.*codegenerator", RegexOptions.IgnoreCase | RegexOptions.Singleline),
            "the scaffolder is missing - run: dotnet tool install -g dotnet-aspnet-codegenerator"),
        (new(@"could not execute because the specified command or file was not found.*\bef\b|No executable found matching command ""dotnet-ef""", RegexOptions.IgnoreCase | RegexOptions.Singleline),
            "dotnet-ef is missing - run: dotnet tool install -g dotnet-ef --version 10.0.*"),
        (new(@"LocalDB is not supported|error: 5[02] - Local Database Runtime|SQL Network Interfaces, error: 26|A network-related or instance-specific error occurred", RegexOptions.IgnoreCase),
            "SQL Server LocalDB is not reachable - install SQL Server Express LocalDB (Windows) and run: sqllocaldb start mssqllocaldb"),
        (new(@"No \.NET SDKs were found|A compatible \.NET SDK was not found", RegexOptions.IgnoreCase),
            "the .NET 10 SDK is missing - install it from https://dotnet.microsoft.com/download/dotnet/10.0"),
        (new(@"Anthropic API error 40[13]", RegexOptions.IgnoreCase),
            "the Anthropic API rejected the key (check ANTHROPIC_API_KEY)"),
    ];

    /// <summary>Returns a fix hint when the failure is environmental, else null.</summary>
    internal static string? EnvironmentProblem(string message) =>
        EnvironmentPatterns.FirstOrDefault(p => p.Pattern.IsMatch(message)).Hint;

    /// <summary>
    /// The text the model needs. SpecValidationException keeps its per-rule errors out of
    /// .Message (they live in .Errors / ToString()), and those errors ARE the repair instructions.
    /// </summary>
    internal static string Describe(Exception ex) =>
        ex is SpecValidationException sv ? sv.ToString() : ex.Message;

    private const int MaxCondensedChars = 6000;
    private static readonly Regex ErrorLine = new(@"\b(error|fail(ed|ure)?|exception)\b|\bCS\d{4}\b", RegexOptions.IgnoreCase);

    /// <summary>
    /// Shrinks a failure for the repair prompt. Build failures embed the whole STDOUT/STDERR (see
    /// Proc.ThrowIfFailed) — thousands of lines of restore noise. The useful part is the first line
    /// (what failed) plus the error lines, de-duplicated (dotnet prints each CS error twice), capped.
    /// </summary>
    internal static string Condense(string message)
    {
        var lines = message.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (message.Length <= MaxCondensedChars) return message;
        var head = lines.FirstOrDefault() ?? "";
        var errors = lines.Skip(1).Where(l => ErrorLine.IsMatch(l)).Select(l => l.Trim()).Distinct().Take(60);
        var body = string.Join("\n", errors);
        if (body.Length == 0) body = message[^Math.Min(message.Length, MaxCondensedChars - head.Length - 32)..];
        var result = $"{head}\n...\n{body}";
        return result.Length <= MaxCondensedChars ? result : result[..MaxCondensedChars];
    }
}
