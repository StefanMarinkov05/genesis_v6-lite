// FILE-MAP: CLI entry point — command surface, interactive spec/seed pickers, registry + archive bookkeeping, the completion banner.
using System.CommandLine;
using GenesisV6.Core.Agent;
using GenesisV6.Core.Cli;
using GenesisV6.Core.Diagnostics;
using GenesisV6.Core.Llm;
using GenesisV6.Core.Pipeline;
using GenesisV6.Core.Spec;
using GenesisV6.Core.Stages;
using GenesisV6.Core.Util;
using GenesisV6.Core.Variation;

// dps-nn (ДПС-НН) - declarative ASP.NET MVC project generator. (internal assembly name: Genesis.Cli)
// Command surface:
//   dps-nn spec build --notes notes.md --spec project-spec.json
//   dps-nn spec expand --spec project-spec.json
//   dps-nn scaffold|emit-models|identity|emit-controllers-views|seed|verify|report --spec ...
//   dps-nn run --all [--from <stage>] [--spec ...] [--out <dir>] [--image-pack <dir>] [--image-provider pexels|pixabay|picsum]
//   dps-nn doctor                                                     (prerequisite check)
//   dps-nn create --describe "<text>" | --describe-file <file> [--max-repairs 3] [--out <dir>]   (agent mode, ADR-013)
// LLM mode: automatic when ANTHROPIC_API_KEY is set, else manual file contract (--resume to continue).
// Models: Sonnet for spec draft/repair/review, Haiku for seed (LlmModels; DPS_NN_MODEL_SPEC/_SEED override).
// Interactive pickers (skipped entirely when --spec is explicit, or under --no-interactive):
//   - multiple project-spec*.json in cwd/fixtures/ -> numbered menu instead of the bare default
//   - multiple *.json under seeds/ -> numbered menu; the chosen file becomes prompts/in/seed-response.json
//     for that run (and any stale Data/seed-data.json in the target project is cleared so the choice sticks)
// Errors are logged, not just printed: a stage failure goes to logs/<Project>/actions.log.jsonl
// (ctx.Error, full exception detail); anything failing before a project context exists (spec
// resolution, GenesisRoot()) goes to logs/global-errors.jsonl instead.

var specOpt = new Option<FileInfo>("--spec", () => new FileInfo("project-spec.json"), "Path to project-spec.json");
var outOpt = new Option<DirectoryInfo>("--out", () => new DirectoryInfo("out"), "Output directory for generated projects");
var notesOpt = new Option<FileInfo>("--notes", "Raw notes file for spec build");
var fromOpt = new Option<string?>("--from", "Resume `run --all` from this stage");
var imagePackOpt = new Option<DirectoryInfo?>("--image-pack", "Local image directory override");
var imageProviderOpt = new Option<string>("--image-provider", () => "pexels", "pexels | pixabay | picsum");
var noInteractiveOpt = new Option<bool>("--no-interactive", "Never prompt; fail if a prompt would be required");
var resumeOpt = new Option<bool>("--resume", "Continue after manually filling prompts/in/<stage>-response.json");

// The installed self-contained exe has no `dotnet` parent to set DOTNET_ROOT (see DotnetHost).
DotnetHost.EnsureDotnetRoot(Environment.GetEnvironmentVariable, Environment.SetEnvironmentVariable, OperatingSystem.IsWindows());

var root = new RootCommand("dps-nn (ДПС-НН) - ASP.NET Core MVC project generator");
root.AddGlobalOption(specOpt);
root.AddGlobalOption(outOpt);
root.AddGlobalOption(noInteractiveOpt);
root.AddGlobalOption(resumeOpt);

string GenesisRoot()
{
    // Templates resolve relative to the tool's own install dir first, then cwd -
    // so `genesis` works both as a global tool and from the repo.
    var candidates = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
    foreach (var c in candidates)
        if (Directory.Exists(Path.Combine(c, "templates"))) return c;
    throw new InvalidOperationException("templates/ directory not found next to the tool or in cwd.");
}

StageContext BuildContext(FileInfo spec, DirectoryInfo outDir, bool useLock)
{
    var genesisRoot = GenesisRoot();
    var loader = new SpecLoader(Path.Combine(genesisRoot, "spec.schema.json"));
    var lockPath = Path.ChangeExtension(spec.FullName, null) + ".lock.json";
    var effective = useLock && File.Exists(lockPath) ? lockPath : spec.FullName;
    var loaded = loader.Load(effective);
    var variation = VariationResolver.Resolve(loaded);
    var state = new StateStore(Path.Combine(outDir.FullName, loaded.Meta.ProjectName, "plan", "state.json"));
    return new StageContext(loaded, variation, outDir.FullName, genesisRoot, state, Console.Out);
}

LlmBridge Llm(DirectoryInfo outDir, string? projectName = null, string? model = null) => new(
    promptsDir: Path.Combine(Directory.GetCurrentDirectory(), "prompts"),
    logPath: Path.Combine(Directory.GetCurrentDirectory(), "dps-nn.log.jsonl"),
    apiKey: Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
    inputDir: projectName is not null && Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "input", projectName))
        ? Path.Combine(Directory.GetCurrentDirectory(), "input", projectName) : null,
    model: model);

Func<string, string> Prompt(bool noInteractive) => noInteractive
    ? q => throw new InvalidOperationException($"--no-interactive but a prompt is required: {q}")
    : q => { Console.Write(q); return Console.ReadLine() ?? ""; };

// ---- logs/global-errors.jsonl: catch-all for exceptions with no project context yet -----
// (spec resolution itself failing, GenesisRoot() not finding templates/, etc.) System.CommandLine's
// own default pipeline already has exception-handling middleware (UseExceptionHandler) that
// intercepts and prints "Unhandled exception: ..." WITHOUT re-throwing past InvokeAsync - so this
// must be called from INSIDE each handler's own catch, not just wrapped around InvokeAsync itself,
// or it would never actually run. Best-effort: never let a logging failure mask the real exception.
void LogGlobalError(Exception ex)
{
    try
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "logs", "global-errors.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var entry = System.Text.Json.JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow,
            args = string.Join(' ', args),
            message = ex.Message,
            detail = ex.ToString(),
        });
        File.AppendAllText(path, entry + Environment.NewLine);
    }
    catch { /* best-effort */ }
}

// ---- Interactive spec/seed picking ----------------------------------------
// Stefan: "I may have multiple config json and multiple seeds, I want to choose
// which one every time in the CLI." Both pickers are pure add-ons: an explicit
// --spec always wins with zero prompting (scripting/automation stay untouched),
// and both only ever activate when there's genuinely more than one candidate -
// a single-spec, single-seed repo behaves exactly as before.

// Detected via the raw args array (not System.CommandLine's option-result API)
// so this doesn't depend on exactly which "was this explicit vs default" surface
// this System.CommandLine beta version exposes.
FileInfo ResolveSpecInteractively(FileInfo fallback, bool noInteractive)
{
    if (noInteractive || args.Contains("--spec")) return fallback;
    var candidates = InteractiveSelection.FindCandidates(
        new[]
        {
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "fixtures"),
            Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "schemas"),
        },
        "project-spec*.json")
        .Where(p => !p.EndsWith(".lock.json", StringComparison.OrdinalIgnoreCase)).ToList();
    // schemas/ archives use a "<project>-schema.json" name (see ArchiveCompletedProject), not
    // "project-spec*.json" - include those too so an archived spec stays pickable.
    candidates.AddRange(InteractiveSelection.FindCandidates(
        new[] { Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "schemas") }, "*-schema.json"));
    candidates = candidates.Distinct().OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToList();

    if (candidates.Count == 0) return fallback; // nothing found anywhere: keep the original default behavior
    if (candidates.Count == 1) return new FileInfo(candidates[0]); // exactly one: use it, no need to prompt

    Console.Write(InteractiveSelection.FormatMenu(candidates, "Multiple project specs found:"));
    var chosen = InteractiveSelection.ParseSelection(Console.ReadLine(), candidates);
    if (chosen is null) { Console.WriteLine("Invalid selection - using default."); return fallback; }
    return new FileInfo(chosen);
}

// Only meaningful for the seed stage. Lets Stefan keep several candidate seed-response
// files under seeds/ and pick one each run; the chosen file is copied to the exact path
// LlmBridge reads (prompts/in/seed-response.json). Deletes any stale Data/seed-data.json
// in the target project first - otherwise SeedStage's own "reuse existing valid seed
// data" check (see SeedStage.RunAsync) would win and the fresh choice would silently do
// nothing.
void ResolveSeedInteractively(StageContext builtCtx, bool noInteractive)
{
    if (noInteractive) return;
    var candidates = InteractiveSelection.FindCandidates(
        new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "seeds"),
            Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "seeds"),
        },
        "*.json");
    // Deliberately NOT auto-applying a sole candidate here (unlike the spec picker): a missing
    // spec is a hard crash, but a missing "choice" here just means SeedStage's own "reuse
    // existing valid seed data" logic handles it - auto-copying + deleting Data/seed-data.json
    // on every run with exactly one seeds/ file would force a needless reseed every time.
    if (candidates.Count < 2) return;

    Console.Write(InteractiveSelection.FormatMenu(candidates, "Multiple seed-response files found:"));
    var chosen = InteractiveSelection.ParseSelection(Console.ReadLine(), candidates);
    if (chosen is null) { Console.WriteLine("Invalid selection - leaving existing seed data/response untouched."); return; }

    var promptsIn = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "in");
    Directory.CreateDirectory(promptsIn);
    File.Copy(chosen, Path.Combine(promptsIn, "seed-response.json"), overwrite: true);

    var seedDataPath = Path.Combine(builtCtx.ProjectDir, "Data", "seed-data.json");
    if (File.Exists(seedDataPath)) File.Delete(seedDataPath);
    Console.WriteLine($"Using {InteractiveSelection.RelativeOrFull(chosen)} for this run's seed data.");
}

// ---- logs/ project registry: maps every project to the spec + seed it was run with -----
// logs/registry.jsonl = full append-only history (one line per invocation, never rewritten).
// logs/registry.json  = quick-lookup snapshot, keyed by project name, always the LATEST run.
// Best-effort: a registry-write failure must never break the pipeline itself.
void RecordProjectRegistry(FileInfo spec, DirectoryInfo outDir, StageContext builtCtx)
{
    try
    {
        var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        Directory.CreateDirectory(logsDir);
        var seedResponsePath = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "in", "seed-response.json");
        var projectName = builtCtx.Spec.Meta.ProjectName;
        var record = new
        {
            ts = DateTimeOffset.UtcNow,
            project = projectName,
            specPath = spec.FullName,
            seedResponsePath = File.Exists(seedResponsePath) ? seedResponsePath : null,
            outDir = outDir.FullName,
        };

        File.AppendAllText(Path.Combine(logsDir, "registry.jsonl"),
            System.Text.Json.JsonSerializer.Serialize(record) + Environment.NewLine);

        var snapshotPath = Path.Combine(logsDir, "registry.json");
        var snapshot = File.Exists(snapshotPath)
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(snapshotPath)) ?? new()
            : new Dictionary<string, object>();
        snapshot[projectName] = record;
        File.WriteAllText(snapshotPath,
            System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    catch { /* registry is best-effort, never blocks the pipeline */ }
}

// ---- prompts/out/schemas + prompts/out/seeds: permanent per-project history ---------
// Copies (not moves) the spec + lock + seed into the archive folders after a green run.
// The originals are left untouched so a successful run never deletes the canonical dev
// fixture. Both search paths above already include these folders, so the archived copy
// stays selectable in later runs. Overwrites any previous archive entry for the same
// project name (matches logs/registry.json's own "keep the latest" convention).
// Best-effort: never let archival failure mask a real successful run.
void ArchiveCompletedProject(FileInfo spec, StageContext lockCtx)
{
    try
    {
        var projectName = lockCtx.Spec.Meta.ProjectName.ToLowerInvariant();
        var schemasDir = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "schemas");
        var seedsDir = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "out", "seeds");
        Directory.CreateDirectory(schemasDir);
        Directory.CreateDirectory(seedsDir);

        if (spec.Exists)
        {
            var destSpec = Path.Combine(schemasDir, $"{projectName}-schema.json");
            File.Copy(spec.FullName, destSpec, overwrite: true);

            var lockPath = Path.ChangeExtension(spec.FullName, null) + ".lock.json";
            if (File.Exists(lockPath))
            {
                var destLock = Path.Combine(schemasDir, $"{projectName}-schema.lock.json");
                File.Copy(lockPath, destLock, overwrite: true);
            }
        }

        var seedResponsePath = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "in", "seed-response.json");
        if (File.Exists(seedResponsePath))
        {
            var destSeed = Path.Combine(seedsDir, $"{projectName}-seed.json");
            File.Copy(seedResponsePath, destSeed, overwrite: true);
        }

        Console.WriteLine($"Archived this run's spec/seed under prompts/out/schemas/ and prompts/out/seeds/ as \"{projectName}-*\".");
    }
    catch (Exception ex) { Console.WriteLine($"(archival skipped - {ex.Message})"); }
}

// ---- spec build / expand --------------------------------------------------
var specCmd = new Command("spec", "Spec authoring stages");
var buildCmd = new Command("build", "Draft spec from raw notes via LLM");
buildCmd.AddOption(notesOpt);
buildCmd.SetHandler(async ctx =>
{
    try
    {
        var spec = ctx.ParseResult.GetValueForOption(specOpt)!;
        var outDir = ctx.ParseResult.GetValueForOption(outOpt)!;
        var notes = ctx.ParseResult.GetValueForOption(notesOpt) ?? throw new InvalidOperationException("--notes is required");
        var noInt = ctx.ParseResult.GetValueForOption(noInteractiveOpt);
        var genesisRoot = GenesisRoot();
        var stage = new SpecBuildStage(Llm(outDir), notes.FullName, spec.FullName,
            Path.Combine(genesisRoot, "spec.schema.json"), Prompt(noInt));
        // spec-build runs before a project exists: minimal synthetic context.
        var state = new StateStore(Path.Combine(outDir.FullName, ".specbuild", "state.json"));
        var dummy = new ProjectSpec();
        await RunStage(stage, new StageContext(dummy, VariationResolver.Resolve(dummy), outDir.FullName, genesisRoot, state, Console.Out));
    }
    catch (Exception ex) { LogGlobalError(ex); throw; }
});
var expandCmd = new Command("expand", "Expand features + write project-spec.lock.json");
expandCmd.SetHandler(async ctx =>
{
    try
    {
        var spec = ctx.ParseResult.GetValueForOption(specOpt)!;
        var outDir = ctx.ParseResult.GetValueForOption(outOpt)!;
        var noInt = ctx.ParseResult.GetValueForOption(noInteractiveOpt);
        spec = ResolveSpecInteractively(spec, noInt);
        var genesisRoot = GenesisRoot();
        var loader = new SpecLoader(Path.Combine(genesisRoot, "spec.schema.json"));
        var lockPath = Path.ChangeExtension(spec.FullName, null) + ".lock.json";
        var stage = new SpecExpandStage(loader, spec.FullName, lockPath,
            Path.Combine(genesisRoot, "feature-templates"),
            Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is null && !File.Exists(Llm(outDir).ResponsePath("expand-review"))
                ? null : Llm(outDir), // review pass only when fillable; deterministic expansion always runs
            Prompt(noInt));
        await RunStage(stage, BuildContext(spec, outDir, useLock: false));
    }
    catch (Exception ex) { LogGlobalError(ex); throw; }
});
specCmd.AddCommand(buildCmd);
specCmd.AddCommand(expandCmd);
root.AddCommand(specCmd);

// ---- individual pipeline stages -------------------------------------------
void AddStageCommand(string name,
    Func<System.CommandLine.Invocation.InvocationContext, FileInfo, DirectoryInfo, string, IStage> factory)
{
    var cmd = new Command(name, $"Run the {name} stage");
    cmd.AddOption(imagePackOpt);
    cmd.AddOption(imageProviderOpt);
    cmd.SetHandler(async ctx =>
    {
        try
        {
            var spec = ctx.ParseResult.GetValueForOption(specOpt)!;
            var outDir = ctx.ParseResult.GetValueForOption(outOpt)!;
            var noInt = ctx.ParseResult.GetValueForOption(noInteractiveOpt);
            spec = ResolveSpecInteractively(spec, noInt);
            var builtCtx = BuildContext(spec, outDir, useLock: true);
            RecordProjectRegistry(spec, outDir, builtCtx);
            if (name == "seed") ResolveSeedInteractively(builtCtx, noInt);
            await RunStage(factory(ctx, spec, outDir, builtCtx.Spec.Meta.ProjectName), builtCtx);
        }
        catch (Exception ex) { LogGlobalError(ex); throw; }
    });
    root.AddCommand(cmd);
}

AddStageCommand("scaffold", (_, _, _, _) => new ScaffoldStage());
AddStageCommand("emit-models", (_, _, _, _) => new EmitModelsStage());
AddStageCommand("identity", (_, _, _, _) => new IdentityStage());
AddStageCommand("emit-controllers-views", (_, _, _, _) => new EmitControllersViewsStage());
AddStageCommand("seed", (ctx, _, outDir, projectName) => new SeedStage(
    Llm(outDir, projectName, LlmModels.Seed),
    ctx.ParseResult.GetValueForOption(imagePackOpt)?.FullName,
    ctx.ParseResult.GetValueForOption(imageProviderOpt)!));
AddStageCommand("verify", (_, _, _, _) => new VerifyStage());
AddStageCommand("report", (_, _, _, _) => new ReportStage());

// ---- run --all --------------------------------------------------------------
// Shared by `run --all` and the agent (`create`). agentMode: no interactive pickers (the seeds/
// picker would apply another project's seed), and any failure is re-thrown as an
// AgentStageException naming the stage, so the repair loop knows where it broke.
async Task RunPipelineAsync(FileInfo spec, DirectoryInfo outDir, string? from, string? imagePack,
    string imageProvider, bool noInt, bool agentMode)
{
    PipelineRunner? runner = null;
    try
    {
        var genesisRoot = GenesisRoot();
        var loader = new SpecLoader(Path.Combine(genesisRoot, "spec.schema.json"));
        var lockPath = Path.ChangeExtension(spec.FullName, null) + ".lock.json";

        // Expansion always precedes the rest (idempotent); spec-build only with --notes flow.
        var expand = new SpecExpandStage(loader, spec.FullName, lockPath,
            Path.Combine(genesisRoot, "feature-templates"), null, Prompt(noInt));
        var preCtx = BuildContext(spec, outDir, useLock: false);
        if (!preCtx.State.IsDone("spec-expand")) { await expand.RunAsync(preCtx); preCtx.State.MarkDone("spec-expand"); }

        var lockCtx = BuildContext(spec, outDir, useLock: true);
        RecordProjectRegistry(spec, outDir, lockCtx);
        if (!agentMode) ResolveSeedInteractively(lockCtx, noInt);

        // Reset actions.log.jsonl at the start of each run for fresh visibility
        var actionLogPath = Path.Combine(Directory.GetCurrentDirectory(), "logs", lockCtx.Spec.Meta.ProjectName, "actions.log.jsonl");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(actionLogPath)!);
            if (File.Exists(actionLogPath)) File.Delete(actionLogPath);
        }
        catch { /* best-effort */ }

        var stages = new IStage[]
        {
            new ScaffoldStage(), new EmitModelsStage(), new IdentityStage(), new EmitControllersViewsStage(),
            new SeedStage(Llm(outDir, lockCtx.Spec.Meta.ProjectName, LlmModels.Seed), imagePack, imageProvider),
            new VerifyStage(), new ReportStage(),
        };
        runner = new PipelineRunner(stages);
        await runner.RunAllAsync(lockCtx, from);
        PrintCompletionBanner(lockCtx);
        ArchiveCompletedProject(spec, lockCtx);
    }
    catch (LlmBridge.LlmPendingException) { throw; }
    catch (Exception ex) when (agentMode && ex is not OperationCanceledException)
    {
        // Anything before the stage loop (expansion, lock re-validation) counts as spec-expand.
        throw new AgentStageException(runner?.FailedStage ?? "spec-expand", ex);
    }
}

var runCmd = new Command("run", "Run the full pipeline");
var allOpt = new Option<bool>("--all", "Run all stages");
runCmd.AddOption(allOpt);
runCmd.AddOption(fromOpt);
runCmd.AddOption(imagePackOpt);
runCmd.AddOption(imageProviderOpt);
runCmd.SetHandler(async ctx =>
{
    try
    {
        var spec = ctx.ParseResult.GetValueForOption(specOpt)!;
        var outDir = ctx.ParseResult.GetValueForOption(outOpt)!;
        var noInt = ctx.ParseResult.GetValueForOption(noInteractiveOpt);
        spec = ResolveSpecInteractively(spec, noInt);
        try
        {
            await RunPipelineAsync(spec, outDir, ctx.ParseResult.GetValueForOption(fromOpt),
                ctx.ParseResult.GetValueForOption(imagePackOpt)?.FullName,
                ctx.ParseResult.GetValueForOption(imageProviderOpt)!, noInt, agentMode: false);
        }
        catch (LlmBridge.LlmPendingException e) { Console.WriteLine(e.Message); Environment.ExitCode = 3; }
    }
    catch (Exception ex) { LogGlobalError(ex); throw; }
});
root.AddCommand(runCmd);

// ---- create: autonomous agent mode (ADR-013) ---------------------------------
// description -> LLM drafts spec -> pipeline -> on failure the LLM repairs the SPEC (never the
// generated code) and the run starts over, up to --max-repairs times. Needs ANTHROPIC_API_KEY:
// a loop of manual pastes is the manual flow (`spec build` + `run --all`), not an agent.
var describeOpt = new Option<string?>("--describe", "Project description (free text, any language)");
var describeFileOpt = new Option<FileInfo?>("--describe-file", "File containing the project description");
var maxRepairsOpt = new Option<int>("--max-repairs", () => 3, "Repair rounds before giving up");
var createCmd = new Command("create", "Agent mode: description -> spec -> generated, verified app (needs ANTHROPIC_API_KEY)");
createCmd.AddOption(describeOpt);
createCmd.AddOption(describeFileOpt);
createCmd.AddOption(maxRepairsOpt);
createCmd.AddOption(imagePackOpt);
createCmd.AddOption(imageProviderOpt);
createCmd.SetHandler(async ctx =>
{
    try
    {
        var description = ctx.ParseResult.GetValueForOption(describeOpt);
        if (ctx.ParseResult.GetValueForOption(describeFileOpt) is { } df) description = await File.ReadAllTextAsync(df.FullName);
        if (string.IsNullOrWhiteSpace(description) && Console.IsInputRedirected) description = await Console.In.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(description))
        {
            Console.Error.WriteLine("create: give the project description via --describe \"...\", --describe-file <file>, or stdin.");
            Environment.ExitCode = 2; return;
        }
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            Console.Error.WriteLine("create: agent mode needs ANTHROPIC_API_KEY. Without a key use the manual flow:\n" +
                "  dps-nn spec build --notes <file> --spec <spec.json>   (paste the request into any Claude chat)\n" +
                "  dps-nn run --all --spec <spec.json>");
            Environment.ExitCode = 2; return;
        }

        // Every agent run gets its own folder unless --out is explicit (same idea as 2-generate.bat).
        var outDir = args.Contains("--out")
            ? ctx.ParseResult.GetValueForOption(outOpt)!
            : new DirectoryInfo(Path.Combine("out", $"agent_{DateTime.Now:yyyyMMdd_HHmmss}"));
        outDir.Create();
        var spec = args.Contains("--spec")
            ? ctx.ParseResult.GetValueForOption(specOpt)!
            : new FileInfo(Path.Combine(outDir.FullName, "project-spec.json"));

        var genesisRoot = GenesisRoot();
        var schemaPath = Path.Combine(genesisRoot, "spec.schema.json");
        var guidePath = Path.Combine(genesisRoot, "utils", "claude-skills", "dps-nn-spec-authoring", "SKILL.md");
        var examplePath = Path.Combine(genesisRoot, "fixtures", "project-spec.facenote.json");
        foreach (var required in new[] { guidePath, examplePath })
            if (!File.Exists(required)) throw new FileNotFoundException($"agent mode needs {required} (ships with dps-nn).");

        // Stale response files from an earlier run must never stand in for this project's:
        // LlmBridge falls back to them when an API call fails.
        var promptsIn = Path.Combine(Directory.GetCurrentDirectory(), "prompts", "in");
        foreach (var stale in new[] { LlmSpecAuthor.DraftStage, LlmSpecAuthor.RepairStage, "seed" })
            File.Delete(Path.Combine(promptsIn, $"{stale}-response.json"));

        var loader = new SpecLoader(schemaPath);
        var author = new LlmSpecAuthor(Llm(outDir, model: LlmModels.Spec), Json.Schema.JsonSchema.FromFile(schemaPath),
            await File.ReadAllTextAsync(guidePath), await File.ReadAllTextAsync(examplePath));
        var imagePack = ctx.ParseResult.GetValueForOption(imagePackOpt)?.FullName;
        var imageProvider = ctx.ParseResult.GetValueForOption(imageProviderOpt)!;

        // A repaired spec reruns from scratch: stale files from the failed attempt (a model that no
        // longer exists, an old controller) would otherwise break the next build. Only ever deletes
        // a project folder INSIDE this run's own --out directory.
        void ResetGeneratedOutput(string oldSpecJson)
        {
            var lockPath = Path.ChangeExtension(spec.FullName, null) + ".lock.json";
            if (File.Exists(lockPath)) File.Delete(lockPath);
            string? name = null;
            try { name = System.Text.Json.Nodes.JsonNode.Parse(oldSpecJson)?["meta"]?["projectName"]?.GetValue<string>(); }
            catch { /* unparsable old spec: nothing was generated from it */ }
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("..")) return;
            var projectDir = Path.GetFullPath(Path.Combine(outDir.FullName, name));
            if (projectDir.StartsWith(outDir.FullName + Path.DirectorySeparatorChar) && Directory.Exists(projectDir))
                Directory.Delete(projectDir, recursive: true);
        }

        Console.WriteLine($"[dps-nn] agent mode - spec/repair: {LlmModels.Spec}, seed: {LlmModels.Seed}, output: {outDir.FullName}");
        var agent = new AgentRunner(author,
            (specPath, ct) => RunPipelineAsync(new FileInfo(specPath), outDir, null, imagePack, imageProvider, noInt: true, agentMode: true),
            path => loader.Load(path),
            ResetGeneratedOutput,
            msg => Console.WriteLine($"[dps-nn] {msg}"),
            ctx.ParseResult.GetValueForOption(maxRepairsOpt));

        AgentResult result;
        try { result = await agent.RunAsync(description, spec.FullName); }
        catch (LlmBridge.LlmPendingException e) { Console.WriteLine(e.Message); Environment.ExitCode = 3; return; }

        var unsupported = new List<UnsupportedRequest>();
        try { unsupported = loader.Load(spec.FullName).Meta.UnsupportedRequests ?? []; } catch { /* spec may be invalid on failure */ }
        WriteAgentReport(outDir, description, spec.FullName, result, unsupported);

        if (unsupported.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Not generated - the description asked for things the generator can't express yet:");
            foreach (var u in unsupported) Console.WriteLine($"  - {u.Request}: {u.Reason}");
        }
        Console.WriteLine(result.Success
            ? $"[dps-nn] agent: done after {result.Attempts.Count} attempt(s). Report: {Path.Combine(outDir.FullName, "agent-report.json")}"
            : $"[dps-nn] agent: stopped - {result.StopReason}. Report: {Path.Combine(outDir.FullName, "agent-report.json")}");
        if (!result.Success) Environment.ExitCode = 1;
    }
    catch (Exception ex) { LogGlobalError(ex); throw; }
});
root.AddCommand(createCmd);

// ---- doctor: prerequisite check (also run by the GUI at startup) --------------
// Exit 0 = everything a pipeline run needs is present (warnings allowed), 1 = something is missing.
var doctorCmd = new Command("doctor", "Check the prerequisites (.NET SDK, dotnet-ef, scaffolder, LocalDB, API key) and print fixes");
doctorCmd.SetHandler(async () =>
{
    string? root;
    try { root = GenesisRoot(); } catch (InvalidOperationException) { root = null; }
    var doctor = new Doctor(Doctor.RunProcessAsync, Environment.GetEnvironmentVariable,
        OperatingSystem.IsWindows(), root, Directory.GetCurrentDirectory());
    var checks = await doctor.RunAsync();
    Console.WriteLine($"dps-nn {typeof(Doctor).Assembly.GetName().Version} - prerequisite check");
    foreach (var line in Doctor.Format(checks)) Console.WriteLine(line);
    var failed = checks.Count(c => c.Status == CheckStatus.Fail);
    Console.WriteLine(failed == 0 ? "All required prerequisites are present." : $"{failed} required prerequisite(s) missing - see the fixes above.");
    Environment.ExitCode = failed == 0 ? 0 : 1;
});
root.AddCommand(doctorCmd);

static void WriteAgentReport(DirectoryInfo outDir, string description, string specPath, AgentResult result,
    List<UnsupportedRequest> unsupported)
{
    var report = new
    {
        generatedAt = DateTimeOffset.UtcNow,
        description,
        spec = specPath,
        models = new { spec = LlmModels.Spec, seed = LlmModels.Seed },
        success = result.Success,
        stopReason = result.StopReason,
        attempts = result.Attempts,
        unsupportedRequests = unsupported,
    };
    File.WriteAllText(Path.Combine(outDir.FullName, "agent-report.json"),
        System.Text.Json.JsonSerializer.Serialize(report, ProjectSpec.JsonOpts));
}

// C2: after a full green run, print the ДПС-НН banner with seeded credentials + local URL.
static void PrintCompletionBanner(StageContext ctx)
{
    string? url = null;
    var lsPath = Path.Combine(ctx.ProjectDir, "Properties", "launchSettings.json");
    if (File.Exists(lsPath))
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(lsPath));
            var profiles = doc.RootElement.GetProperty("profiles");
            foreach (var profile in profiles.EnumerateObject())
                if (profile.Value.TryGetProperty("applicationUrl", out var au))
                {
                    url = au.GetString()?.Split(';').FirstOrDefault(u => u.StartsWith("http://")) ?? au.GetString();
                    break;
                }
        }
        catch { /* banner is best-effort */ }
    }

    string? dbName = null;
    var appsettingsPath = Path.Combine(ctx.ProjectDir, "appsettings.json");
    if (File.Exists(appsettingsPath))
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(appsettingsPath));
            var csKey = $"{ctx.Spec.Meta.ProjectName}ContextConnection";
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var csObj) &&
                csObj.TryGetProperty(csKey, out var csVal))
            {
                var m = System.Text.RegularExpressions.Regex.Match(csVal.GetString() ?? "", @"Database=([^;]+)");
                if (m.Success) dbName = m.Groups[1].Value;
            }
        }
        catch { /* banner is best-effort */ }
    }

    var users = ctx.Spec.Identity?.SeedUsers?.Users ?? [];
    var adminRoleNames = (ctx.Spec.Identity?.Roles ?? []).Where(r => r.IsAdmin).Select(r => r.Name).ToHashSet();

    // Every seed user's full credentials are recorded to a file (Stefan can always look them up);
    // the console banner and GUI only ever surface ONE admin + ONE non-admin, not all N seed users,
    // to keep the readout short and avoid splashing every password on screen.
    var credsPath = Path.Combine(Directory.GetCurrentDirectory(), "logs", ctx.Spec.Meta.ProjectName, "seed-credentials.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(credsPath)!);
    File.WriteAllLines(credsPath, users.Select(u => $"{u.Role,-16} {u.Email} / {u.Password}"));

    var admin = users.FirstOrDefault(u => adminRoleNames.Contains(u.Role));
    var nonAdmin = users.FirstOrDefault(u => !adminRoleNames.Contains(u.Role));
    var shown = new[] { admin, nonAdmin }.Where(u => u is not null).Cast<SeedUserSpec>().ToList();

    Console.WriteLine();
    Console.WriteLine("╔════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║  {ctx.Spec.Meta.ProjectName,-52} ║");
    if (url is not null)
        Console.WriteLine($"║  🔗 {url,-49} ║");
    else
        Console.WriteLine($"║  (Configure launchSettings.json for application URL)  ║");
    Console.WriteLine("╠════════════════════════════════════════════════════════╣");
    foreach (var u in shown)
    {
        var credential = $"{u.Email} / {u.Password}";
        Console.WriteLine($"║  {u.Role,-16} {credential,-35} ║");
    }
    Console.WriteLine("╚════════════════════════════════════════════════════════╝");
    if (users.Count > shown.Count)
        Console.WriteLine($"({users.Count - shown.Count} more seed user(s) — full list: {credsPath})");
    if (dbName is not null)
        Console.WriteLine($"Database: {dbName}");
    // `dotnet run --project "<dir>"` avoids `cd <dir> && dotnet run`: the `&&` chain operator
    // does NOT exist in Windows PowerShell 5.1 (added in PS 7) and hard-fails at parse time.
    // This single form works identically in PS 5.1, PS 7, and cmd.exe. See changelog 083.
    Console.WriteLine($"Start: dotnet run --project \"{ctx.ProjectDir}\"");
    Console.WriteLine();
}

static async Task RunStage(IStage stage, StageContext ctx)
{
    try
    {
        await stage.RunAsync(ctx);
        ctx.State.MarkDone(stage.Name);
    }
    catch (LlmBridge.LlmPendingException e)
    {
        Console.WriteLine(e.Message);
        Environment.ExitCode = 3; // distinct exit code: "waiting for manual LLM response"
    }
    catch (Exception ex)
    {
        ctx.Error($"{stage.Name} FAILED: {ex.Message}\n{ex}");
        throw;
    }
}

try
{
    // Handlers signal 1/2/3 via Environment.ExitCode, but System.CommandLine returns 0 for
    // void handlers and Main's return value wins over Environment.ExitCode - so without this
    // the exit-3 "LLM pending" signal and create's failure codes all surfaced as 0 (changelog 132).
    var invokeCode = await root.InvokeAsync(args);
    return invokeCode != 0 ? invokeCode : Environment.ExitCode;
}
catch (Exception ex)
{
    // Defense-in-depth: System.CommandLine's own UseExceptionHandler middleware (visible in
    // the stack trace as "Unhandled exception: ...") normally intercepts handler exceptions
    // before they'd ever reach here - the REAL logging happens inside each handler's own
    // catch below. This only fires for something that somehow escapes that middleware too.
    LogGlobalError(ex);
    throw;
}
