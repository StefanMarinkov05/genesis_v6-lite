using GenesisV6.Core.Agent;
using GenesisV6.Core.Llm;
using GenesisV6.Core.Pipeline;
using GenesisV6.Core.Spec;
using Json.Schema;
using Xunit;

namespace Genesis.Tests;

/// <summary>ADR-013 agent loop: draft → validate → run → repair-the-spec, with fakes for LLM + pipeline.</summary>
public sealed class AgentRunnerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("dpsnn-agent-").FullName;
    private string SpecPath => Path.Combine(_dir, "project-spec.json");
    private readonly List<string> _log = [];
    private readonly List<string> _resets = [];

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private sealed class FakeAuthor : ISpecAuthor
    {
        public int Drafts, Repairs;
        public readonly List<AgentFailure> Failures = [];
        public Task<string> DraftAsync(string description, CancellationToken ct) { Drafts++; return Task.FromResult("{\"v\":0}"); }
        public Task<string> RepairAsync(string description, string current, AgentFailure failure, CancellationToken ct)
        {
            Repairs++; Failures.Add(failure);
            return Task.FromResult($"{{\"v\":{Repairs}}}");
        }
    }

    private AgentRunner Runner(FakeAuthor author, Func<string, CancellationToken, Task> pipeline,
        Action<string>? validate = null, int maxRepairs = 3) =>
        new(author, pipeline, validate ?? (_ => { }), _resets.Add, _log.Add, maxRepairs);

    private static Func<string, CancellationToken, Task> FailAt(string stage, string message) =>
        (_, _) => throw new AgentStageException(stage, new InvalidOperationException(message));

    [Fact]
    public async Task Green_first_run_drafts_once_and_never_repairs()
    {
        var author = new FakeAuthor();
        var result = await Runner(author, (_, _) => Task.CompletedTask).RunAsync("a shop", SpecPath);

        Assert.True(result.Success);
        Assert.Single(result.Attempts);
        Assert.Equal((1, 0), (author.Drafts, author.Repairs));
        Assert.Empty(_resets);
    }

    [Fact]
    public async Task Existing_spec_is_reused_not_redrafted()
    {
        File.WriteAllText(SpecPath, "{\"mine\":true}");
        var author = new FakeAuthor();
        await Runner(author, (_, _) => Task.CompletedTask).RunAsync("a shop", SpecPath);
        Assert.Equal(0, author.Drafts);
    }

    [Fact]
    public async Task Stage_failure_is_repaired_reset_and_rerun()
    {
        var author = new FakeAuthor();
        var runs = 0;
        Task Pipeline(string _, CancellationToken __) =>
            ++runs == 1 ? throw new AgentStageException("emit-models", new InvalidOperationException("CS0246: type Foo not found"))
                        : Task.CompletedTask;

        var result = await Runner(author, Pipeline).RunAsync("a shop", SpecPath);

        Assert.True(result.Success);
        Assert.Equal(2, result.Attempts.Count);
        Assert.Equal("emit-models", result.Attempts[0].FailedStage);
        Assert.Equal("emit-models", author.Failures.Single().Stage);
        Assert.Contains("CS0246", author.Failures.Single().Message);
        Assert.Equal(["{\"v\":0}"], _resets);                          // reset gets the OLD spec (to find its output)
        Assert.Equal("{\"v\":1}", File.ReadAllText(SpecPath));         // repaired spec written
    }

    [Fact]
    public async Task Environment_failure_stops_without_spending_a_repair()
    {
        var author = new FakeAuthor();
        var result = await Runner(author, FailAt("seed", "dotnet ef failed: the specified deps.json does not exist"))
            .RunAsync("a shop", SpecPath);

        Assert.False(result.Success);
        Assert.Equal(0, author.Repairs);
        Assert.Contains("dotnet-ef", result.StopReason);
    }

    [Fact]
    public async Task Gives_up_after_max_repairs()
    {
        var author = new FakeAuthor();
        var result = await Runner(author, FailAt("verify", "boot check failed"), maxRepairs: 2).RunAsync("x", SpecPath);

        Assert.False(result.Success);
        Assert.Equal(2, author.Repairs);
        Assert.Equal(3, result.Attempts.Count); // initial + 2 repaired runs
        Assert.Contains("2 repair round", result.StopReason);
    }

    [Fact]
    public async Task Validation_failure_hands_the_rule_errors_to_the_model()
    {
        var author = new FakeAuthor();
        var calls = 0;
        void Validate(string _)
        {
            if (calls++ == 0) throw new SpecValidationException("Spec failed semantic validation.", ["Relation target 'Ghost' is not a model"]);
        }
        var pipelineRan = false;
        var result = await Runner(author, (_, _) => { pipelineRan = true; return Task.CompletedTask; }, Validate)
            .RunAsync("x", SpecPath);

        Assert.True(result.Success);
        Assert.True(pipelineRan);
        Assert.Equal("spec-validate", author.Failures.Single().Stage);
        Assert.Contains("Ghost", author.Failures.Single().Message); // lives in .Errors, not .Message
    }

    private sealed class BrokenDraftAuthor : ISpecAuthor
    {
        public Task<string> DraftAsync(string d, CancellationToken ct) =>
            throw new InvalidOperationException("LLM stage 'agent-spec-draft' failed schema validation 3 times.");
        public Task<string> RepairAsync(string d, string c, AgentFailure f, CancellationToken ct) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Draft_that_never_validates_is_a_clean_failure_not_a_crash()
    {
        var result = await new AgentRunner(new BrokenDraftAuthor(), (_, _) => Task.CompletedTask, _ => { }, _ => { }, _ => { })
            .RunAsync("x", SpecPath);

        Assert.False(result.Success);
        Assert.Contains("could not draft", result.StopReason);
        Assert.Equal("agent-spec-draft", result.Attempts.Single().FailedStage);
        Assert.False(File.Exists(SpecPath));
    }

    [Fact]
    public async Task Manual_paste_pending_propagates_instead_of_being_repaired()
    {
        var author = new FakeAuthor();
        var agent = Runner(author, (_, _) => throw new LlmBridge.LlmPendingException("seed", "req", "resp"));
        await Assert.ThrowsAsync<LlmBridge.LlmPendingException>(() => agent.RunAsync("x", SpecPath));
        Assert.Equal(0, author.Repairs);
    }

    [Theory]
    [InlineData("the specified deps.json does not exist", true)]
    [InlineData("A network-related or instance-specific error occurred while establishing a connection to SQL Server", true)]
    [InlineData("Anthropic API error 401: invalid x-api-key", true)]
    [InlineData("Controllers/FooController.cs(12,5): error CS0246: The type 'Bar' could not be found", false)]
    [InlineData("verify: page /Client/Products leaked English text 'Create New'", false)]
    public void Environment_triage(string message, bool environmental) =>
        Assert.Equal(environmental, AgentRunner.EnvironmentProblem(message) is not null);

    [Fact]
    public void Condense_keeps_the_errors_from_a_huge_build_log()
    {
        var noise = string.Concat(Enumerable.Repeat("  Restored /tmp/x.csproj (in 12 ms).\n", 2000));
        var err = "Models/Order.cs(3,1): error CS0101: duplicate definition 'Order'";
        var message = $"dotnet build failed (exit 1).\nSTDOUT:\n{noise}{err}\n{err}\nSTDERR:\n";

        var condensed = AgentRunner.Condense(message);

        Assert.True(condensed.Length <= 6000);
        Assert.StartsWith("dotnet build failed", condensed);
        Assert.Single(condensed.Split('\n'), l => l.Contains("CS0101")); // dotnet prints each error twice
        Assert.DoesNotContain("Restored", condensed);
    }

    [Fact]
    public void Condense_leaves_short_messages_untouched() =>
        Assert.Equal("boom", AgentRunner.Condense("boom"));
}

/// <summary>The real LlmBridge-backed author, exercised in manual mode (response file pre-placed, no network).</summary>
public sealed class LlmSpecAuthorTests : IDisposable
{
    private readonly string _prompts = Directory.CreateTempSubdirectory("dpsnn-prompts-").FullName;
    public void Dispose() => Directory.Delete(_prompts, recursive: true);

    private const string Guide = "---\nname: x\ndescription: y\n---\n# GUIDE BODY\nEvery property needs a displayName.";

    [Fact]
    public async Task Draft_builds_the_prompt_from_guide_and_example_and_returns_a_valid_spec()
    {
        var fixture = File.ReadAllText(Paths.Fixture);
        Directory.CreateDirectory(Path.Combine(_prompts, "in"));
        File.WriteAllText(Path.Combine(_prompts, "in", $"{LlmSpecAuthor.DraftStage}-response.json"), fixture);
        var bridge = new LlmBridge(_prompts, Path.Combine(_prompts, "log.jsonl"), apiKey: null);
        var author = new LlmSpecAuthor(bridge, JsonSchema.FromFile(Paths.Schema), Guide, "{\"example\":true}");

        var spec = await author.DraftAsync("Онлайн книжарница с отзиви", CancellationToken.None);

        Assert.Equal("FaceNote", new SpecLoader(Paths.Schema).LoadFromString(spec).Meta.ProjectName);
        var request = File.ReadAllText(bridge.RequestPath(LlmSpecAuthor.DraftStage));
        Assert.Contains("# GUIDE BODY", request);
        Assert.DoesNotContain("name: x", request);           // frontmatter stripped
        Assert.Contains("{\"example\":true}", request);
        Assert.Contains("Онлайн книжарница", request);
        Assert.Contains("meta.unsupportedRequests", request);
    }

    [Fact]
    public async Task Draft_without_a_response_in_manual_mode_is_pending()
    {
        var bridge = new LlmBridge(_prompts, Path.Combine(_prompts, "log.jsonl"), apiKey: null);
        var author = new LlmSpecAuthor(bridge, JsonSchema.FromFile(Paths.Schema), Guide, "{}");
        await Assert.ThrowsAsync<LlmBridge.LlmPendingException>(() => author.DraftAsync("x", CancellationToken.None));
    }
}

public class AgentSupportTests
{
    [Fact]
    public void Schema_accepts_unsupported_requests_and_loader_exposes_them()
    {
        var json = File.ReadAllText(Paths.Fixture).Replace("\"meta\": {",
            "\"meta\": { \"unsupportedRequests\": [ { \"request\": \"календар за резервации\", \"reason\": \"no booking feature\" } ],");
        var spec = new SpecLoader(Paths.Schema).LoadFromString(json);
        Assert.Equal("календар за резервации", spec.Meta.UnsupportedRequests!.Single().Request);
    }

    [Fact]
    public void Schema_rejects_an_unsupported_request_without_a_reason()
    {
        var json = File.ReadAllText(Paths.Fixture).Replace("\"meta\": {",
            "\"meta\": { \"unsupportedRequests\": [ { \"request\": \"x\" } ],");
        Assert.Throws<SpecValidationException>(() => new SpecLoader(Paths.Schema).LoadFromString(json));
    }

    [Fact]
    public void Models_default_to_sonnet_for_spec_and_haiku_for_seed()
    {
        Assert.Equal("claude-sonnet-5-5", LlmModels.DefaultSpec);
        Assert.Equal("claude-haiku-5-5", LlmModels.DefaultSeed);
        Assert.Equal(LlmModels.Spec, new LlmBridge(".", "log", null).Model);
        Assert.Equal("m-x", new LlmBridge(".", "log", null, model: "m-x").Model);
    }

    [Fact]
    public void Strip_frontmatter_only_removes_the_leading_block() =>
        Assert.Equal("# Body\n---\nkeep", LlmSpecAuthor.StripFrontmatter("---\na: 1\n---\n# Body\n---\nkeep"));

    private sealed class Boom(string name) : IStage
    {
        public string Name => name;
        public Task RunAsync(StageContext ctx, CancellationToken ct = default) =>
            name == "emit-models" ? throw new InvalidOperationException("boom") : Task.CompletedTask;
    }

    [Fact]
    public async Task Pipeline_runner_records_which_stage_failed()
    {
        var dir = Directory.CreateTempSubdirectory("dpsnn-runner-").FullName;
        try
        {
            var spec = new SpecLoader(Paths.Schema).Load(Paths.Fixture);
            var ctx = new StageContext(spec, GenesisV6.Core.Variation.VariationResolver.Resolve(spec), dir, ".",
                new StateStore(Path.Combine(dir, "state.json")), TextWriter.Null);
            var runner = new PipelineRunner([new Boom("scaffold"), new Boom("emit-models"), new Boom("identity")]);

            await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAllAsync(ctx));
            Assert.Equal("emit-models", runner.FailedStage);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
