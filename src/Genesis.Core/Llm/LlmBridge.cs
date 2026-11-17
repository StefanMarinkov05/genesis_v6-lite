// FILE-MAP: the ADR-005 LLM file contract — request/response files, automatic vs manual (exit-3) mode, schema-validated retries, hash-logged exchanges.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Json.Schema;

namespace GenesisV6.Core.Llm;

/// <summary>
/// Every LLM stage is a FILE CONTRACT (ADR-005):
///   prompts/out/{stage}-request.md   <- genesis writes (instructions + JSON schema + input)
///   prompts/in/{stage}-response.json <- something fills (human paste OR API call)
///
/// Two fill modes, one validation path:
///   manual    - genesis exits with instructions; user pastes request into a Claude
///               session, saves the response file, re-runs `genesis <stage> --resume`.
///   automatic - ANTHROPIC_API_KEY set: genesis calls the Messages API itself (model per
///               LlmModels: spec/repair vs seed) and writes the response file, so the
///               artifact trail is identical. ANTHROPIC_BASE_URL overrides the endpoint.
///
/// Validation: response must parse as JSON AND satisfy the stage's response schema.
/// On violation: a violation report is written to prompts/out/ and (in automatic
/// mode) the request is retried with the violations appended, max 3 attempts.
/// Every exchange is hashed + logged to dps-nn.log.jsonl.
/// </summary>
public sealed class LlmBridge(string promptsDir, string logPath, string? apiKey, string? inputDir = null,
    string? model = null, string? baseUrl = null)
{
    /// <summary>The model this bridge calls in automatic mode (default: <see cref="LlmModels.Spec"/>).</summary>
    public string Model { get; } = model ?? LlmModels.Spec;
    private const int MaxAttempts = 3;
    // Large enough for a full spec or a multi-model seed; long generations need the raised
    // HTTP timeout below (HttpClient's 100 s default cut off big seed responses).
    private const int MaxTokens = 32000;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromMinutes(10);

    public string RequestPath(string stage) => Path.Combine(promptsDir, "out", $"{stage}-request.md");
    public string ResponsePath(string stage) => Path.Combine(promptsDir, "in", $"{stage}-response.json");

    public sealed class LlmPendingException(string stage, string requestPath, string responsePath)
        : Exception($"LLM stage '{stage}' is waiting for a manual response.\n" +
                    $"1. Open {requestPath} and paste its contents into a Claude session.\n" +
                    $"2. Save the model's JSON reply to {responsePath}.\n" +
                    $"3. Re-run with --resume.");

    /// <summary>
    /// Returns validated response JSON for the stage. Throws LlmPendingException in
    /// manual mode when no (valid) response file exists yet.
    /// </summary>
    public async Task<JsonDocument> ExchangeAsync(string stage, string instructions, JsonSchema responseSchema,
        object inputData, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.Combine(promptsDir, "out"));
        Directory.CreateDirectory(Path.Combine(promptsDir, "in"));

        var schemaJson = JsonSerializer.Serialize(responseSchema, new JsonSerializerOptions { WriteIndented = true });
        var inputJson = JsonSerializer.Serialize(inputData, new JsonSerializerOptions { WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        // Token-optimized request: schema-in/schema-out, zero prose padding.
        var request = $"""
            # genesis stage: {stage}

            {instructions}

            Respond with ONLY a JSON document valid against this JSON Schema - no markdown fences, no prose:

            ```json-schema
            {schemaJson}
            ```

            Input data:

            ```json
            {inputJson}
            ```
            """;
        var reqPath = RequestPath(stage);
        await File.WriteAllTextAsync(reqPath, request, ct);
        Log(stage, "request-written", Hash(request), null);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            string responseText;
            var respPath = ResponsePath(stage);

            if (apiKey is not null)
            {
                try
                {
                    responseText = await CallApiAsync(request, ct);
                    await File.WriteAllTextAsync(respPath, responseText, ct); // same artifact trail as manual
                }
                catch (InvalidOperationException apiEx)
                {
                    // API call failed (credits exhausted, rate limit, network) - fall back to
                    // manual/offline paths instead of crashing the whole pipeline. The person
                    // can still supply a response file while the API is unavailable.
                    Log(stage, "api-call-failed-falling-back", Hash(apiEx.Message), [apiEx.Message]);
                    if (File.Exists(respPath))
                        responseText = await File.ReadAllTextAsync(respPath, ct);
                    else if (inputDir is not null && File.Exists(Path.Combine(inputDir, $"{stage}-response.json")))
                        responseText = await File.ReadAllTextAsync(Path.Combine(inputDir, $"{stage}-response.json"), ct);
                    else
                        throw new LlmPendingException(stage, reqPath, respPath);
                }
            }
            else if (File.Exists(respPath))
            {
                responseText = await File.ReadAllTextAsync(respPath, ct);
            }
            else if (inputDir is not null && File.Exists(Path.Combine(inputDir, $"{stage}-response.json")))
            {
                // Offline mode: a pre-generated response dropped into input/{Project}/ by the
                // user (pasted from a chat session) - same validation path as everything else.
                responseText = await File.ReadAllTextAsync(Path.Combine(inputDir, $"{stage}-response.json"), ct);
                await File.WriteAllTextAsync(respPath, responseText, ct); // keep the artifact trail identical
            }
            else
            {
                throw new LlmPendingException(stage, reqPath, respPath);
            }

            var (doc, violations) = Validate(responseText, responseSchema);
            Log(stage, violations.Count == 0 ? "response-valid" : "response-invalid", Hash(responseText),
                violations.Count == 0 ? null : violations);

            if (doc is not null && violations.Count == 0) return doc;

            // Write the violation report next to the request for retry (manual or auto).
            var reportPath = Path.Combine(promptsDir, "out", $"{stage}-violations-attempt{attempt}.md");
            await File.WriteAllTextAsync(reportPath,
                $"# Validation failures (attempt {attempt}/{MaxAttempts})\n\n- " + string.Join("\n- ", violations), ct);

            if (apiKey is null)
            {
                File.Delete(respPath); // force a fresh manual paste
                throw new LlmPendingException(stage, reqPath, respPath);
            }
            request += $"\n\nYour previous response was rejected:\n- {string.Join("\n- ", violations)}\nFix and respond again, JSON only.";
        }
        throw new InvalidOperationException($"LLM stage '{stage}' failed schema validation {MaxAttempts} times. See prompts/out/ violation reports.");
    }

    internal static (JsonDocument? doc, List<string> violations) Validate(string responseText, JsonSchema schema)
    {
        // Tolerate accidental markdown fences even though we forbid them - cheap robustness.
        var cleaned = responseText.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            cleaned = cleaned[(firstNewline + 1)..].TrimEnd('`', '\n', ' ');
        }
        try
        {
            var doc = JsonDocument.Parse(cleaned);
            var result = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            if (result.IsValid) return (doc, []);
            var violations = result.Details.Where(d => d.HasErrors)
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")).Distinct().ToList();
            return (null, violations);
        }
        catch (JsonException ex)
        {
            return (null, [$"not valid JSON: {ex.Message}"]);
        }
    }

    private async Task<string> CallApiAsync(string prompt, CancellationToken ct)
    {
        var endpoint = (baseUrl ?? Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL") ?? "https://api.anthropic.com").TrimEnd('/') + "/";
        using var http = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = HttpTimeout };
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        var body = new
        {
            model = Model,
            max_tokens = MaxTokens,
            messages = new[] { new { role = "user", content = prompt } }
        };
        var resp = await http.PostAsync("v1/messages",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Anthropic API error {(int)resp.StatusCode}: {text}");
        using var doc = JsonDocument.Parse(text);
        var sb = new StringBuilder();
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
            if (block.GetProperty("type").GetString() == "text")
                sb.Append(block.GetProperty("text").GetString());
        return sb.ToString();
    }

    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)))[..16];

    private void Log(string stage, string @event, string hash, List<string>? violations)
    {
        var line = JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow, stage, @event, hash,
            violations = violations is { Count: > 0 } ? violations : null
        });
        File.AppendAllText(logPath, line + Environment.NewLine);
    }
}
