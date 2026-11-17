// FILE-MAP: stage 7 — LLM seed-row generation via symbolic refs, image sourcing (Pexels/Pixabay/Picsum/local pack), SeedData.cs emission, EF migration add + apply.
using System.Text.Json;
using GenesisV6.Core.Llm;
using GenesisV6.Core.Pipeline;
using GenesisV6.Core.Spec;
using GenesisV6.Core.Util;
using Json.Schema;

namespace GenesisV6.Core.Stages;

/// <summary>
/// Stage 7 `seed` (ADR-003 symbolic refs):
///  1. LLM generates rows as strict JSON: parents carry "refKey", children carry
///     "parentRef" - NO numeric IDs are invented by the model, because SQL Server
///     identity columns assign them at insert time.
///  2. Rows with an image model carry "imageKeyword"; CLI downloads via
///     Pexels (PEXELS_API_KEY) / Pixabay (--image-provider pixabay, PIXABAY_API_KEY) /
///     Picsum fallback on zero hits / --image-pack local dir override.
///  3. templates/models/seed_data.sbn emits Data/SeedData.cs which inserts parents,
///     captures DB-assigned int IDs in a Dictionary&lt;string,int&gt;, resolves children.
///  4. THEN: ef migrations add Initial + ef database update (always after wiring).
/// </summary>
public sealed class SeedStage(LlmBridge llm, string? imagePackDir, string imageProvider) : IStage
{
    public string Name => "seed";

    // Default row counts for models a feature fragment injects - only applied when
    // the raw spec did not already configure that model explicitly.
    private static readonly Dictionary<string, int> FeatureModelDefaultRows = new()
    {
        ["Catalogue"] = 4, ["CatalogueItem"] = 6,
        ["CartItem"] = 3, ["Order"] = 2, ["OrderItem"] = 3,
        ["Follow"] = 2, ["Review"] = 2,
        ["PostComment"] = 8, ["PostVote"] = 12,
    };

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        if (spec.Seeding is not { } seeding) { ctx.Info("no seeding section - skipping"); return; }
        var engine = new TemplateEngine(spec, ctx.Variation, ctx.GenesisRoot);

        // Auto-fill row counts for feature-injected models (Catalogue, CartItem, Order,
        // Follow, Review, PostComment/Vote...) that the raw spec's seeding.rowsPerModel
        // never knew about at authoring time - so `run --all` always seeds everything
        // enabled, not just the models the author explicitly listed.
        var seedUserCount = spec.Identity?.SeedUsers?.Users.Count ?? 0;
        foreach (var model in spec.Models)
        {
            if (seeding.RowsPerModel.ContainsKey(model.Name)) continue;
            if (FeatureModelDefaultRows.TryGetValue(model.Name, out var defaultRows))
                seeding.RowsPerModel[model.Name] = model.Name switch
                {
                    "Catalogue" => Math.Max(defaultRows, seedUserCount * 2),
                    _ => defaultRows,
                };
        }

        // ---- 1. LLM seed data --------------------------------------------------
        var schema = BuildSeedSchema(spec, seeding);
        var seedJsonPath = Path.Combine(ctx.ProjectDir, "Data", "seed-data.json");
        JsonDocument? doc = null;

        // Re-run friendliness: a prior run (or a hand-provided file) may already have
        // valid seed data sitting there. Re-running `seed` should never force a fresh
        // manual paste-into-Claude-and-resume cycle when the data is already good -
        // so check first, and only fall through to the LLM exchange if it's missing
        // or no longer matches the current spec (re-validated against the SAME schema
        // an LLM response would have to satisfy, not just "file exists").
        if (File.Exists(seedJsonPath))
        {
            var existingText = await File.ReadAllTextAsync(seedJsonPath, ct);
            var (existingDoc, violations) = LlmBridge.Validate(existingText, schema);
            if (existingDoc is not null && violations.Count == 0)
            {
                doc = existingDoc;
                ctx.Info($"seed: reusing existing {seedJsonPath} (valid against current spec) - skipping the LLM exchange");
            }
            else
            {
                ctx.Info($"seed: existing {seedJsonPath} no longer matches the spec ({violations.Count} issue(s)) - regenerating via LLM");
            }
        }

        if (doc is null)
        {
            var input = new
            {
                domainHint = spec.Meta.DomainHint,
                language = spec.Meta.Language,
                rowsPerModel = seeding.RowsPerModel,
                seedUsers = spec.Identity?.SeedUsers?.Users.Select((u, i) => new { placeholder = $"$seedUser:{i}", u.Role }) ?? [],
                models = spec.Models.Select(m => new
                {
                    m.Name,
                    properties = m.Properties.Where(p => !p.Key && !p.ForeignKey)
                        .Select(p => new { p.Name, p.Type, hint = p.SeedHint, isUserRef = p.Name.EndsWith("UserId") }),
                    parents = m.Relations.Where(r => r.Kind == "manyToOne").Select(r => r.Target),
                    hasImages = m.Images is not null,
                }),
            };
            // Only the language/script requirement (rule 1) actually depends on spec.Meta.Language -
            // everything else below (realism, variety, refs, scenario coverage) is language-agnostic.
            var languageRule = spec.Meta.IsBulgarian
                ? "1. ALL human-readable string values in Bulgarian (Cyrillic): names, titles, descriptions, categories, " +
                  "cities, comments. Internationally-branded proper nouns (BMW, iPhone, Adidas) stay in their original " +
                  "script.\n"
                : "1. ALL human-readable string values in English: names, titles, descriptions, categories, cities, " +
                  "comments.\n";
            doc = await llm.ExchangeAsync("seed",
                $"Generate a COMPLETE, realistic seed dataset for {(spec.Meta.IsBulgarian ? "a BULGARIAN" : "an English-language")} " +
                "application (domainHint describes the domain) - every model key in rowsPerModel MUST have data, none " +
                "skipped, so the generated app is fully testable end-to-end with zero manual setup. Rules:\n" +
                languageRule +
                "2. Data must be plausible for the target market: real city names, realistic prices, real-world " +
                "category names, believable dates. NO placeholder text (e.g. 'Test 1'), NO lorem ipsum.\n" +
                "3. Rows must be varied - no two rows may share the same descriptive string.\n" +
                "4. Respect each property's 'hint' when present - it constrains realistic ranges/content.\n" +
                "5. Every parent row: unique refKey (kebab-case, Latin). Every child row: parentRef_{Target} " +
                "referencing an emitted refKey. Models with hasImages=true: include imageKeywords, an array of " +
                "1-2-word ENGLISH stock-photo search phrases (the only intentionally English field) - one entry per " +
                "photo. The schema fixes exactly how many entries each row needs; give each entry a DIFFERENT angle " +
                "on the same item (e.g. \"red sneakers\", \"sneakers side view\", \"sneakers on foot\") so the " +
                "photos in one row don't look identical.\n" +
                "6. Dates ISO-8601; decimals plain numbers.\n" +
                "7. Every property flagged isUserRef=true (owner/author fields) MUST use one of the exact strings " +
                "listed in 'seedUsers' (e.g. \"$seedUser:0\") - never invent a user id or GUID. Distribute ownership " +
                "realistically across the available seed users, not all rows to the same one.\n" +
                "8. Scenario coverage - make the generated app demoable with ZERO manual clicking: if Catalogue/" +
                "CatalogueItem are present, give EACH seed user 2-3 catalogues and put 2-3 items in at least one of " +
                "them; if CartItem is present, give at least one user a non-empty cart; if Order/OrderItem are " +
                "present, give at least one user 1-2 past orders with items; if Follow is present, create at least " +
                "one mutual follow relationship between the seed users; if Review is present, add a few reviews " +
                "targeting different users; if PostComment/PostVote are present, spread several comments and " +
                "upvotes/downvotes (\"likes\") across the seeded posts so engagement looks organic, not uniform.",
                schema, input, ct);
            Directory.CreateDirectory(Path.GetDirectoryName(seedJsonPath)!);
            await File.WriteAllTextAsync(seedJsonPath, doc.RootElement.GetRawText(), ct);
        }

        // ---- 2. images ---------------------------------------------------------
        // One or more images per row (mode "single" = exactly 1, "multiple" = 2-4, enforced by
        // BuildSeedSchema's imageKeywords array). Filenames are "{model}-{row}-{img}.jpg" - the
        // per-image suffix is new; seed_data.sbn's runtime loop computes the identical pattern
        // from the same seed-data.json array, so the two stay in sync without passing state.
        foreach (var model in spec.Models.Where(m => m.Images is not null))
        {
            if (!doc.RootElement.TryGetProperty(model.Name, out var rows)) continue;
            var targetDir = Path.Combine(ctx.ProjectDir, model.Images!.StoredIn);
            Directory.CreateDirectory(targetDir);
            var rowIndex = 0;
            foreach (var row in rows.EnumerateArray())
            {
                ++rowIndex;
                var keywords = row.TryGetProperty("imageKeywords", out var kws) && kws.ValueKind == JsonValueKind.Array
                    ? kws.EnumerateArray().Select(k => k.GetString()!).ToList()
                    : [model.Name.ToLower()];
                var imgIndex = 0;
                foreach (var keyword in keywords)
                {
                    ++imgIndex;
                    var fileName = $"{model.Name.ToLower()}-{rowIndex}-{imgIndex}.jpg";
                    await DownloadImageAsync(keyword, Path.Combine(targetDir, fileName), ctx, ct);
                }
            }
        }

        // ---- 3. SeedData.cs ----------------------------------------------------
        await EmitModelsStage.WriteAsync(ctx, seeding.OutputFile,
            engine.RenderFile(ctx.Template("models/seed_data.sbn")), ct);

        // ---- 4. migrations + database update ----------------------------------------
        // Smart resume: if EF migrations were already successfully applied to the current
        // database (recorded in logs/<Project>/db.json), skip the entire migration block.
        // This prevents re-running the slow build+ef cycle when seed stage was interrupted
        // AFTER migrations completed (e.g. by a crash in the verify stage).
        var dbJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "logs", spec.Meta.ProjectName, "db.json");
        DbLifecycleRecord? dbRecord = null;
        if (File.Exists(dbJsonPath))
            try { dbRecord = JsonSerializer.Deserialize<DbLifecycleRecord>(File.ReadAllText(dbJsonPath)); } catch { }

        var currentDbName = GetCurrentDbName(ctx);
        var migrationsAlreadyApplied = dbRecord?.MigrationsApplied == true
                                       && dbRecord?.DbName == currentDbName
                                       && currentDbName is not null;

        if (migrationsAlreadyApplied)
        {
            ctx.Info($"migrations: already applied to {currentDbName} — skipping (smart resume)");
        }
        else
        {
            // REVISED THEORY (the prior "just build first" fix did not help - Stefan hit the exact
            // same failure again, which rules out a pure file-flush/antivirus race and points at
            // something a single extra build can't fix: a STALE BACKGROUND PROCESS). `dotnet build`
            // and `dotnet ef`'s own internal build both use MSBuild's "node reuse" feature, which
            // keeps a persistent MSBuild/Roslyn build-server process running in the background across
            // invocations for speed. Rapid back-to-back builds (our pre-build, then dotnet-ef's own
            // internal one, moments later) can hit that SAME reused, possibly-still-busy node, which
            // matches the exact symptom Stefan saw: the tool's own attempt fails, but running the
            // identical command manually moments later - from a fresh shell, not hitting the same
            // just-used node in the same way - succeeds outright. Disabling node reuse for these
            // specific calls forces a clean process every time, closing that window directly instead
            // of hoping a settle delay is long enough.
            var noNodeReuse = new Dictionary<string, string?> { ["DOTNET_CLI_DISABLE_BUILD_SERVERS"] = "1" };
            (await Proc.RunAsync("dotnet", "build", ctx.ProjectDir, ct, env: noNodeReuse, timeout: TimeSpan.FromMinutes(5)))
                .ThrowIfFailed("dotnet build (pre-migrations)");

            // First migration named Initial; later model changes (feature additions, spec edits)
            // get timestamped auto-migrations - only when EF reports pending model changes.
            var hasInitial = Directory.Exists(Path.Combine(ctx.ProjectDir, "Migrations")) &&
                             Directory.EnumerateFiles(Path.Combine(ctx.ProjectDir, "Migrations"), "*_Initial.cs").Any();
            if (!hasInitial)
            {
                var mig = await RunEfWithRetryAsync("ef migrations add Initial --verbose", ctx.ProjectDir, ct);
                if (mig.ExitCode != 0
                    && !mig.StdOut.Contains("already exists") && !mig.StdErr.Contains("already exists")
                    && !mig.StdOut.Contains("is used by an existing migration") && !mig.StdErr.Contains("is used by an existing migration"))
                    ThrowEfError("ef migrations add Initial", mig);
            }
            else
            {
                var pending = await RunEfWithRetryAsync("ef migrations has-pending-model-changes --verbose", ctx.ProjectDir, ct, timeoutMinutes: 3);
                if (pending.ExitCode != 0) // nonzero = there ARE pending changes (or old EF w/o the command - then attempt is harmless)
                {
                    var name = $"Auto{DateTime.UtcNow:yyyyMMddHHmmss}";
                    var addMig = await RunEfWithRetryAsync($"ef migrations add {name} --verbose", ctx.ProjectDir, ct);
                    if (addMig.ExitCode != 0) ThrowEfError($"ef migrations add {name}", addMig);
                }
            }
            var update = await RunEfWithRetryAsync("ef database update --verbose", ctx.ProjectDir, ct);
            if (update.ExitCode != 0) ThrowEfError("ef database update", update);

            // Record that migrations are applied to this specific DB so future runs can skip.
            if (dbRecord is not null)
            {
                dbRecord.MigrationsApplied = true;
                try { await File.WriteAllTextAsync(dbJsonPath, JsonSerializer.Serialize(dbRecord), ct); } catch { }
            }
        }
        ctx.Info("seed complete: data json, images, SeedData.cs, migration applied");
    }

    private static string? GetCurrentDbName(StageContext ctx)
    {
        var path = Path.Combine(ctx.ProjectDir, "appsettings.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var csKey = $"{ctx.Spec.Meta.ProjectName}ContextConnection";
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                cs.TryGetProperty(csKey, out var val))
            {
                var m = System.Text.RegularExpressions.Regex.Match(val.GetString() ?? "", @"Database=([^;]+)");
                if (m.Success) return m.Groups[1].Value;
            }
        }
        catch { }
        return null;
    }

    /// <summary>Response schema: one array property per model, row shape derived from the spec.</summary>
    internal static JsonSchema BuildSeedSchema(ProjectSpec spec, SeedingSection seeding)
    {
        var props = new List<(string, JsonSchema)>();
        foreach (var model in spec.Models.Where(m => seeding.RowsPerModel.ContainsKey(m.Name)))
        {
            var rowProps = new List<(string, JsonSchema)>();
            var required = new List<string>();
            var isParent = spec.Models.Any(o => o.Relations.Any(r => r.Target == model.Name));
            if (isParent) { rowProps.Add(("refKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Build())); required.Add("refKey"); }
            foreach (var rel in model.Relations.Where(r => r.Kind == "manyToOne"))
            { rowProps.Add(($"parentRef_{rel.Target}", new JsonSchemaBuilder().Type(SchemaValueType.String).Build())); required.Add($"parentRef_{rel.Target}"); }
            foreach (var p in model.Properties.Where(p => !p.Key && !p.ForeignKey))
            {
                var t = p.Type switch
                {
                    "int" => SchemaValueType.Integer,
                    "decimal" or "double" => SchemaValueType.Number,
                    "bool" => SchemaValueType.Boolean,
                    _ => SchemaValueType.String,
                };
                rowProps.Add((p.Name, new JsonSchemaBuilder().Type(t).Build()));
                if (!p.Nullable) required.Add(p.Name);
            }
            if (model.Images is not null)
            {
                // "multiple" mode gets a real gallery (2-4 stock photos/product); "single" stays
                // exactly one, same as before this changed. Both are the SAME field
                // (imageKeywords: string[]) so seed_data.sbn's runtime loop doesn't need to
                // branch on mode - it just inserts one {Model}Image row per array element.
                var (min, max) = model.Images.Mode == "multiple" ? (2u, 4u) : (1u, 1u);
                var keywordsSchema = new JsonSchemaBuilder().Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String).Build())
                    .MinItems(min).MaxItems(max).Build();
                rowProps.Add(("imageKeywords", keywordsSchema));
                required.Add("imageKeywords");
            }

            var rowSchema = new JsonSchemaBuilder().Type(SchemaValueType.Object)
                .Properties(rowProps.ToArray()).Required(required).AdditionalProperties(false);
            props.Add((model.Name, new JsonSchemaBuilder().Type(SchemaValueType.Array)
                .Items(rowSchema).MinItems((uint)seeding.RowsPerModel[model.Name]).Build()));
        }
        return new JsonSchemaBuilder().Type(SchemaValueType.Object)
            .Properties(props.ToArray()).Required(props.Select(p => p.Item1).ToArray())
            .AdditionalProperties(false).Build();
    }

    // Persistent cross-run image cache (ADR-011): lives OUTSIDE out/, which since changelog 019
    // is a fresh out/run_<timestamp>/ folder per run - a cache keyed under ctx.ProjectDir would
    // never survive a regenerate. Keyed by provider+keyword since Picsum/Pexels/Pixabay results
    // for the same keyword can differ; local-pack hits are cached too (cheap, and consistent
    // with "download once" even though a local copy is already fast).
    private async Task DownloadImageAsync(string keyword, string targetPath, StageContext ctx, CancellationToken ct)
    {
        if (File.Exists(targetPath)) return; // idempotent within a single run

        // Workspace (cwd), not GenesisRoot: an installed GenesisRoot is read-only (ADR-014). In the repo
        // both are the repo root, so dev behaviour is unchanged.
        var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), ".image-cache");
        Directory.CreateDirectory(cacheDir);
        var cacheKey = $"{(imagePackDir is not null ? "pack" : imageProvider)}-{SlugifyKeyword(keyword)}.jpg";
        var cachePath = Path.Combine(cacheDir, cacheKey);

        if (File.Exists(cachePath))
        {
            File.Copy(cachePath, targetPath);
            return;
        }

        // Local pack override wins over any network provider.
        if (imagePackDir is not null)
        {
            var candidate = Directory.EnumerateFiles(imagePackDir, $"{keyword}*.*").FirstOrDefault()
                            ?? Directory.EnumerateFiles(imagePackDir).FirstOrDefault();
            if (candidate is not null)
            {
                File.Copy(candidate, cachePath);
                File.Copy(cachePath, targetPath);
                return;
            }
        }

        using var http = new HttpClient();
        try
        {
            string? imageUrl = null;
            if (imageProvider == "pexels" && Environment.GetEnvironmentVariable("PEXELS_API_KEY") is { } pexelsKey)
            {
                http.DefaultRequestHeaders.Add("Authorization", pexelsKey);
                using var resp = JsonDocument.Parse(await http.GetStringAsync(
                    $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(keyword)}&per_page=1", ct));
                var photos = resp.RootElement.GetProperty("photos");
                if (photos.GetArrayLength() > 0)
                    imageUrl = photos[0].GetProperty("src").GetProperty("large").GetString();
            }
            else if (imageProvider == "pixabay" && Environment.GetEnvironmentVariable("PIXABAY_API_KEY") is { } pixKey)
            {
                using var resp = JsonDocument.Parse(await http.GetStringAsync(
                    $"https://pixabay.com/api/?key={pixKey}&q={Uri.EscapeDataString(keyword)}&per_page=3", ct));
                var hits = resp.RootElement.GetProperty("hits");
                if (hits.GetArrayLength() > 0)
                    imageUrl = hits[0].GetProperty("largeImageURL").GetString();
            }
            // Zero hits or no provider/key: Picsum fallback - keyword-seeded so re-runs are stable.
            imageUrl ??= $"https://picsum.photos/seed/{Uri.EscapeDataString(keyword)}/800/600";

            var bytes = await http.GetByteArrayAsync(imageUrl, ct);
            await File.WriteAllBytesAsync(cachePath, bytes, ct);
            File.Copy(cachePath, targetPath);
        }
        catch (HttpRequestException ex)
        {
            // Leave targetPath absent on failure - the render layer falls back to
            // product-default.svg (ADR-011) rather than a hardcoded 1x1 placeholder.
            ctx.Info($"WARNING: image download for '{keyword}' failed ({ex.Message}) - no image written, falling back to the shipped default at render time");
        }
    }

    private static string SlugifyKeyword(string keyword) =>
        new string(keyword.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    /// <summary>
    /// Runs a `dotnet ef ...` command; if it fails with the exact "deps.json does not exist"
    /// signature (a known dotnet-ef race - see the node-reuse comment above), retries up to
    /// twice more with increasing delays rather than failing outright. Any other failure, or a
    /// final failure after all retries, is returned as-is for the caller to handle/throw.
    /// </summary>
    private static async Task<Proc.Result> RunEfWithRetryAsync(string args, string workDir, CancellationToken ct, int timeoutMinutes = 5)
    {
        var noNodeReuse = new Dictionary<string, string?> { ["DOTNET_CLI_DISABLE_BUILD_SERVERS"] = "1" };
        var delays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) };
        Proc.Result result;
        for (var attempt = 0; ; attempt++)
        {
            result = await Proc.RunAsync("dotnet", args, workDir, ct, env: noNodeReuse, timeout: TimeSpan.FromMinutes(timeoutMinutes));
            var looksLikeRace = result.ExitCode != 0 &&
                (result.StdErr.Contains("deps.json does not exist", StringComparison.OrdinalIgnoreCase) ||
                 result.StdOut.Contains("deps.json does not exist", StringComparison.OrdinalIgnoreCase));
            if (!looksLikeRace || attempt >= delays.Length) return result;
            await Task.Delay(delays[attempt], ct);
        }
    }

    /// <summary>
    /// `dotnet ef ...` needs a `dotnet-ef` tool version compatible with this project's EF Core
    /// packages (pinned to 10.0.* - see BuildStages' package-add list). "Build succeeded" followed
    /// by "the specified deps.json does not exist" is the textbook symptom of an installed
    /// `dotnet-ef` that predates .NET 10 - it can trigger a build (version-agnostic) but then
    /// fails to locate/load the resulting net10.0 assembly. Give the exact fix instead of a bare
    /// stack trace, matching the existing aspnet-codegenerator pattern in BuildStages. (If this
    /// still fires after RunEfWithRetryAsync's retry, the tool version really is the problem -
    /// the race-condition case above already got its one free retry before reaching here.)
    /// </summary>
    private static void ThrowEfError(string what, Proc.Result result)
    {
        var looksLikeToolMismatch =
            result.StdErr.Contains("deps.json does not exist", StringComparison.OrdinalIgnoreCase) ||
            result.StdOut.Contains("deps.json does not exist", StringComparison.OrdinalIgnoreCase);
        var hint = looksLikeToolMismatch
            ? "\n\nThree different fixes (dotnet-ef version, pre-build + retry, disabling MSBuild node\n" +
              "reuse) have not resolved this, so guessing a fourth blind fix isn't the right move. The\n" +
              "STDOUT above now includes dotnet-ef's own --verbose output for this exact failing attempt -\n" +
              "compare it line-by-line against a manual 'dotnet ef migrations add Initial --verbose' run in\n" +
              "the same project folder (which has succeeded every time it's been tried by hand). Look\n" +
              "specifically at the 'Using application base' / 'Using working directory' lines in both - a\n" +
              "difference there would point at the real cause directly."
            : "";
        throw new InvalidOperationException($"{what} failed (exit {result.ExitCode}).{hint}\nSTDOUT:\n{result.StdOut}\nSTDERR:\n{result.StdErr}");
    }
}
