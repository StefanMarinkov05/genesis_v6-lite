// FILE-MAP: stages 3-5 — ScaffoldStage (dotnet new mvc + build), EmitModelsStage (models/DTOs/DbContext), IdentityStage (Identity scaffold, BG localization, roles + seed users).
using System.Text.Json;
using System.Text.RegularExpressions;
using GenesisV6.Core.Pipeline;
using GenesisV6.Core.Spec;
using GenesisV6.Core.Util;

namespace GenesisV6.Core.Stages;

/// <summary>
/// Stage 3 `scaffold`: dotnet new sln + mvc, sln add, NuGet package adds, first build.
/// Idempotency: presence of the .csproj short-circuits `dotnet new`; `dotnet add package`
/// is naturally idempotent (updates the reference).
/// </summary>
public sealed class ScaffoldStage : IStage
{
    public string Name => "scaffold";

    // All 10.0.* pinned - the generated app must restore identically months later.
    private static readonly string[] Packages =
    [
        "Microsoft.VisualStudio.Web.CodeGeneration.Design --version 10.0.*",
        "Microsoft.AspNetCore.Identity.UI --version 10.0.*",
        "Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 10.0.*",
        "Microsoft.EntityFrameworkCore.SqlServer --version 10.0.*",
        "Microsoft.EntityFrameworkCore.Tools --version 10.0.*",
        // Explicit direct references to force NuGet's resolution up from whatever vulnerable
        // 6.12.1 the codegen/EF tooling above transitively pulls in (GHSA-g4vj-cjjj-v7hg,
        // low severity, patched at 6.12.5 - NuGet always resolves to the HIGHEST version
        // requested anywhere in the graph, so this "upgrades" the transitive one too).
        "NuGet.Packaging --version 6.12.5",
        "NuGet.Protocol --version 6.12.5",
    ];

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        Directory.CreateDirectory(ctx.WorkDir);
        // .NET 10 `dotnet new sln` creates .slnx by default - accept whichever exists.
        var slnCandidates = new[] { $"{spec.Meta.SolutionName}.sln", $"{spec.Meta.SolutionName}.slnx" }
            .Select(f => Path.Combine(ctx.WorkDir, f)).ToArray();
        var csprojPath = Path.Combine(ctx.ProjectDir, $"{spec.Meta.ProjectName}.csproj");

        if (!slnCandidates.Any(File.Exists))
            (await Proc.RunAsync("dotnet", $"new sln -n {spec.Meta.SolutionName}", ctx.WorkDir, ct)).ThrowIfFailed("dotnet new sln");
        var slnPath = slnCandidates.First(File.Exists);

        if (!File.Exists(csprojPath))
            (await Proc.RunAsync("dotnet",
                $"new mvc -n {spec.Meta.ProjectName} --framework {spec.Meta.TargetFramework} --auth None",
                ctx.WorkDir, ct)).ThrowIfFailed("dotnet new mvc");

        (await Proc.RunAsync("dotnet", $"sln \"{slnPath}\" add \"{csprojPath}\"", ctx.WorkDir, ct))
            .ThrowIfFailed("dotnet sln add"); // idempotent: re-add is a no-op warning

        foreach (var pkg in Packages)
            (await Proc.RunAsync("dotnet", $"add \"{csprojPath}\" package {pkg}", ctx.WorkDir, ct))
                .ThrowIfFailed($"dotnet add package {pkg}");

        // Belt-and-braces alongside the explicit NuGet.Packaging/Protocol version bump above:
        // suppress NU1901 (low severity audit warning) directly too, in case the bump doesn't
        // fully silence it for some reason. Matches the same NoWarn convention this tool's own
        // csproj already uses for NU1902-1904 (see Genesis.Core.csproj/Genesis.Cli.csproj).
        var csprojDoc = System.Xml.Linq.XDocument.Load(csprojPath);
        var firstPropertyGroup = csprojDoc.Root!.Element("PropertyGroup");
        if (firstPropertyGroup?.Element("NoWarn") is { } existingNoWarn)
        {
            if (!existingNoWarn.Value.Contains("NU1901")) existingNoWarn.Value += ";NU1901";
        }
        else
        {
            firstPropertyGroup?.Add(new System.Xml.Linq.XElement("NoWarn", "$(NoWarn);NU1901"));
        }
        csprojDoc.Save(csprojPath);

        // Pin the SDK inside the GENERATED repo too - self-contained requirement.
        if (spec.Meta.SdkPin is { } pin)
            await File.WriteAllTextAsync(Path.Combine(ctx.WorkDir, "global.json"),
                JsonSerializer.Serialize(new { sdk = new { version = pin, rollForward = "latestFeature" } },
                    new JsonSerializerOptions { WriteIndented = true }), ct);

        // DB lifecycle: reuse the same LocalDB database when the spec hasn't changed;
        // only create a new timestamped one (and drop the old one) when the spec differs.
        // Prevents old databases piling up on every run while keeping the fast path
        // (same spec = same schema = reuse existing data, no re-seed, no re-migrate).
        var logsDir = Path.Combine(Directory.GetCurrentDirectory(), "logs", spec.Meta.ProjectName);
        Directory.CreateDirectory(logsDir);
        var dbJsonPath = Path.Combine(logsDir, "db.json");
        var specHash = ComputeSpecHash(spec);

        DbLifecycleRecord? storedDb = null;
        if (File.Exists(dbJsonPath))
            try { storedDb = JsonSerializer.Deserialize<DbLifecycleRecord>(File.ReadAllText(dbJsonPath)); } catch { /* corrupt db.json - ignore */ }

        var reuseDb = storedDb?.SpecHash == specHash && storedDb?.DbName is not null;
        if (!reuseDb && storedDb?.DbName is { } oldDbName)
            await TryDropLocalDbAsync(oldDbName, ctx, ct);

        var dbName = reuseDb ? storedDb!.DbName! : $"{spec.Meta.ProjectName}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var appsettingsPath = Path.Combine(ctx.ProjectDir, "appsettings.json");
        var appsettings = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(appsettingsPath, ct))!.AsObject();
        var cs = appsettings["ConnectionStrings"]?.AsObject() ?? new System.Text.Json.Nodes.JsonObject();
        cs[$"{spec.Meta.ProjectName}ContextConnection"] =
            $"Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true";
        appsettings["ConnectionStrings"] = cs;
        await File.WriteAllTextAsync(appsettingsPath, appsettings.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), ct);

        if (!reuseDb)
            await File.WriteAllTextAsync(dbJsonPath,
                JsonSerializer.Serialize(new DbLifecycleRecord { SpecHash = specHash, DbName = dbName }), ct);

        ctx.Info(reuseDb
            ? $"database name: {dbName} (reusing — spec unchanged)"
            : $"database name: {dbName} (new — spec changed or first run)");

        // launchBrowser:true on every profile - `dotnet run` auto-opens a tab on startup,
        // complementing the console URL print in program.sbn (which works even where a
        // browser can't auto-launch, e.g. over a bare remote shell).
        var launchSettingsPath = Path.Combine(ctx.ProjectDir, "Properties", "launchSettings.json");
        if (File.Exists(launchSettingsPath))
        {
            var launchSettings = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(launchSettingsPath, ct))!.AsObject();
            if (launchSettings["profiles"]?.AsObject() is { } profiles)
                foreach (var (_, profile) in profiles)
                    if (profile?.AsObject() is { } p && p.ContainsKey("applicationUrl"))
                        p["launchBrowser"] = true;
            await File.WriteAllTextAsync(launchSettingsPath,
                launchSettings.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), ct);
        }

        CopyStaticAssets(ctx);

        (await Proc.RunAsync("dotnet", "build", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("initial dotnet build");
        ctx.Info("scaffold complete, connection string set, initial build green");
    }

    // Ships the generic image fallbacks (ADR-011): avatar-default.svg / product-default.svg,
    // authored once in the repo's images/ drop folder, copied into every generated project's
    // wwwroot/images/ so the fallback <img> sources the views already reference actually
    // resolve instead of 404ing. Overwrite-safe (File.Copy overwrite:true) so a re-run after
    // an asset update always picks up the latest version.
    private static void CopyStaticAssets(StageContext ctx)
    {
        var srcDir = Path.Combine(ctx.GenesisRoot, "images");
        if (!Directory.Exists(srcDir)) return;
        var destDir = Path.Combine(ctx.ProjectDir, "wwwroot", "images");
        Directory.CreateDirectory(destDir);
        foreach (var svg in Directory.EnumerateFiles(srcDir, "*.svg"))
            File.Copy(svg, Path.Combine(destDir, Path.GetFileName(svg)), overwrite: true);
        ctx.Info("copied static image fallbacks to wwwroot/images/");
    }

    // Deterministic hash of the spec parts that drive the DB schema and seed shape.
    // OrderBy on all collections to avoid serialization-order variance.
    internal static string ComputeSpecHash(ProjectSpec spec)
    {
        var key = string.Join("|",
            spec.Meta.ProjectName,
            spec.Meta.TargetFramework,
            string.Join(",", spec.Models.OrderBy(m => m.Name).Select(m =>
                m.Name + ":" + string.Join(",", m.Properties.OrderBy(p => p.Name).Select(p => $"{p.Name}.{p.Type}.{p.Nullable}")))),
            spec.Seeding is { } s
                ? string.Join(",", s.RowsPerModel.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))
                : "");
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // Best-effort: drop a named LocalDB database via sqlcmd. If sqlcmd is absent or the
    // DB is already gone, log and continue — this is cleanup, not a hard requirement.
    private static async Task TryDropLocalDbAsync(string dbName, StageContext ctx, CancellationToken ct)
    {
        try
        {
            var safeId = dbName.Replace("]", "]]");
            var safeLit = dbName.Replace("'", "''");
            var sql = $"IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{safeLit}') DROP DATABASE [{safeId}]";
            var result = await Proc.RunAsync("sqlcmd",
                $"-S \"(localdb)\\mssqllocaldb\" -Q \"{sql}\"",
                Directory.GetCurrentDirectory(), ct, timeout: TimeSpan.FromSeconds(30));
            ctx.Info(result.ExitCode == 0
                ? $"dropped old LocalDB database: {dbName}"
                : $"(could not drop old database '{dbName}': {result.StdErr.Trim()})");
        }
        catch { ctx.Info($"(skipping old database '{dbName}' drop — sqlcmd unavailable)"); }
    }
}

/// <summary>
/// Stage 4 `emit models`: models, DTOs, {Project}Context, custom validation
/// attributes - all Scriban-rendered. Bulgarian error messages come straight from
/// the spec; templates never contain UI literals of their own.
/// </summary>
public sealed class EmitModelsStage : IStage
{
    public string Name => "emit-models";

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        var engine = new TemplateEngine(spec, ctx.Variation, ctx.GenesisRoot);

        // Every emission below renders from the immutable spec and writes to a DISTINCT file path
        // with no dependency on any sibling's output — the DbContext references ApplicationUser,
        // but only at BUILD time, and both are on disk before the build runs — so they fan out
        // across the named worker lanes safely (issue #5). The build gate stays serial, after
        // every file is written. A single shared TemplateEngine is reused: RenderFile builds all
        // of its state locally per call and touches no shared mutable field, so concurrent renders
        // are safe.
        var work = new List<(string Label, Func<Task> Work)>();

        if (spec.Conventions.Timestamps.CreatedAt || spec.Conventions.Timestamps.UpdatedAt)
            work.Add(("Models/AuditableEntity.cs", () => WriteAsync(ctx, "Models/AuditableEntity.cs",
                engine.RenderFile(ctx.Template("models/auditable_entity.sbn")), ct)));

        foreach (var model in spec.Models)
            work.Add((model.File, () => WriteAsync(ctx, model.File,
                engine.RenderFile(ctx.Template("models/model.sbn"), new { model }), ct)));

        foreach (var dto in spec.Dtos)
            work.Add((dto.File, () => WriteAsync(ctx, dto.File,
                engine.RenderFile(ctx.Template("models/dto.sbn"), new { dto }), ct)));

        if (spec.CustomValidationAttributes is { } cva)
            work.Add((cva.File, () => WriteAsync(ctx, cva.File,
                engine.RenderFile(ctx.Template("models/validation_attributes.sbn"), new { cva }), ct)));

        if (spec.Identity is { } idn)
            work.Add((idn.ApplicationUser.File, () => WriteAsync(ctx, idn.ApplicationUser.File,
                engine.RenderFile(ctx.Template("identity/application_user.sbn")), ct)));

        // TravelMap style: {ProjectName}Context at the project root, not Data/ApplicationDbContext.
        work.Add(($"{spec.Meta.ProjectName}Context.cs", () => WriteAsync(ctx, $"{spec.Meta.ProjectName}Context.cs",
            engine.RenderFile(ctx.Template("models/dbcontext.sbn")), ct)));

        await ParallelEmit.RunAsync(work);

        (await Proc.RunAsync("dotnet", "build", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("build after emit-models");
        ctx.Info("models emitted, build green");
    }

    internal static async Task WriteAsync(StageContext ctx, string relPath, string content, CancellationToken ct)
    {
        var full = Path.Combine(ctx.ProjectDir, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content, ct);
        ctx.Info($"wrote {relPath}");
    }
}

/// <summary>
/// Stage 5 `identity`:
///  1. aspnet-codegenerator identity with the spec's page modules
///  2. ApplicationUser (extends IdentityUser, spec extras) - template
///  3. BgIdentityErrorDescriber - template (runtime error strings)
///  4. Static EN->BG replacement map over scaffolded Identity Razor pages (ADR-002)
///     - the two Bulgarian surfaces are DIFFERENT mechanisms on purpose.
///  5. Roles + seed users via in-context seeding hook, areas + route registration
///     (Program.cs patching happens here since identity owns service registration).
/// </summary>
public sealed class IdentityStage : IStage
{
    public string Name => "identity";

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        if (spec.Identity is not { } id) { ctx.Info("no identity section - skipping"); return; }
        var engine = new TemplateEngine(spec, ctx.Variation, ctx.GenesisRoot);

        // ApplicationUser + DbContext already reference each other; emit user first.
        await EmitModelsStage.WriteAsync(ctx, id.ApplicationUser.File,
            engine.RenderFile(ctx.Template("identity/application_user.sbn")), ct);

        // Item 17: ProfileHash must exist before the scaffold+build below - _ManageNav.cshtml
        // (scaffolded further down in this method) references it, and this stage's own build
        // check runs before EmitControllersViewsStage would otherwise emit it.
        await EmitModelsStage.WriteAsync(ctx, "Infrastructure/ProfileHash.cs",
            engine.RenderFile(ctx.Template("models/profile_hash.sbn")), ct);

        // BgIdentityErrorDescriber hardcodes Bulgarian error text - only emit it (and only wire
        // it into DI, see program.sbn's matching meta.is_bulgarian gate) for a Bulgarian project.
        // The two gates MUST stay in sync: emitting the class without the DI line is harmless
        // dead code, but wiring the DI line without emitting the class is a CS0246 build break -
        // the same "two paths, one concept, drift" bug class as FileUploadService (changelog 033).
        if (id.ErrorDescriber is { } ed && spec.Meta.IsBulgarian)
            await EmitModelsStage.WriteAsync(ctx, ed.File,
                engine.RenderFile(ctx.Template("identity/bg_identity_error_describer.sbn")), ct);

        // Program.cs references ImageService and SeedData - emit both now; later stages
        // re-emit them (idempotent overwrite). Program.cs is the convergence point: every
        // symbol it mentions must exist by this stage's build gate.
        if (spec.Models.Any(m => m.Images is not null))
            await EmitModelsStage.WriteAsync(ctx, "Services/ImageService.cs",
                engine.RenderFile(ctx.Template("controllers/image_service.sbn")), ct);
        // Same reason: Program.cs registers FileUploadService when pfp/rating/posts uploads exist, so the
        // class must exist by this build gate too (mirrors EmitControllersViewsStage's emit condition).
        if ((spec.Identity?.ApplicationUser.ExtraProperties.Any(p => p.Name == "ProfilePicturePath") ?? false)
            || EmitControllersViewsStage.FeatureEnabled(spec, "rating")
            || EmitControllersViewsStage.FeatureEnabled(spec, "posts"))
            await EmitModelsStage.WriteAsync(ctx, "Services/FileUploadService.cs",
                engine.RenderFile(ctx.Template("controllers/file_upload_service.sbn")), ct);
        // Task 26/7: Program.cs registers UserProfileService (+ references UserProfileViewModel) when
        // posts/catalogues exist, so both must exist by this build gate too (mirrors the emit condition
        // in EmitControllersViewsStage + program.sbn's needs_profile_service).
        if (EmitControllersViewsStage.FeatureEnabled(spec, "posts")
            || EmitControllersViewsStage.FeatureEnabled(spec, "catalogues"))
        {
            await EmitModelsStage.WriteAsync(ctx, "Models/UserProfileViewModel.cs",
                engine.RenderFile(ctx.Template("models/user_profile_view_model.sbn")), ct);
            await EmitModelsStage.WriteAsync(ctx, "Services/UserProfileService.cs",
                engine.RenderFile(ctx.Template("controllers/user_profile_service.sbn")), ct);
        }
        if (spec.Seeding is not null && !File.Exists(Path.Combine(ctx.ProjectDir, spec.Seeding.OutputFile)))
            await EmitModelsStage.WriteAsync(ctx, spec.Seeding.OutputFile,
                engine.RenderFile(ctx.Template("models/seed_data.sbn")), ct);

        // Program.cs is REPLACED wholesale from template (not patched): the MVC
        // template's Program.cs is tiny and ours must own DI order, identity,
        // areas routing, seeding hook, and MigrateAsync-on-startup flag.
        await EmitModelsStage.WriteAsync(ctx, "Program.cs",
            engine.RenderFile(ctx.Template("project/program.sbn")), ct);
        await EmitModelsStage.WriteAsync(ctx, "Data/IdentitySeeder.cs",
            engine.RenderFile(ctx.Template("identity/identity_seeder.sbn")), ct);

        // Scaffold the Identity UI pages the spec asks for.
        var modules = string.Join(";", id.Modules); // e.g. Account.Register;Account.Login;Account.Logout
        var gen = await Proc.RunAsync("dotnet",
            $"aspnet-codegenerator identity -dc {spec.Meta.RootNamespace}.{spec.Meta.ProjectName}Context --files \"{modules}\"",
            ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5));
        if (gen.ExitCode != 0)
        {
            // codegenerator needs the tool installed; give the actionable command.
            throw new InvalidOperationException(
                "aspnet-codegenerator failed. Install once: dotnet tool install -g dotnet-aspnet-codegenerator\n" + gen.StdErr);
        }

        // Item 17: ProfileHash lives in {RootNamespace}.Infrastructure - Identity Pages have their
        // OWN scaffolder-generated _ViewImports.cshtml (Razor Pages, separate from the MVC areas'
        // _ViewImports.cshtml patched elsewhere), which doesn't include it by default. Patching the
        // top-level one is enough - Razor _ViewImports.cshtml cascades down the folder tree, so
        // Account/Manage/_ManageNav.cshtml inherits it without needing its own copy touched.
        var identityViewImports = Path.Combine(ctx.ProjectDir, "Areas", "Identity", "Pages", "_ViewImports.cshtml");
        if (File.Exists(identityViewImports))
        {
            var iviText = await File.ReadAllTextAsync(identityViewImports, ct);
            if (!iviText.Contains(".Infrastructure"))
                await File.WriteAllTextAsync(identityViewImports,
                    iviText.TrimEnd('\n') + $"\n@using {spec.Meta.RootNamespace}.Infrastructure\n", ct);
        }

        // Bulgarian page markup, two layers (ADR-002b) - BOTH are retrofit overrides of the
        // scaffolder's own (English) output, so both are skipped for a non-Bulgarian project:
        // the scaffolded pages are left exactly as aspnet-codegenerator produced them, which is
        // already correct English - there is nothing to translate away from.
        if (spec.Meta.IsBulgarian)
        {
            //  a) replacement map over ALL scaffolded pages (fallback for untemplated pages) -
            //     Manage/ is excluded inside ApplyReplacementMapAsync itself (the BG residue-map
            //     trap: it would corrupt ManageNavPages.Email/EmailModel-style identifiers).
            if (id.Localization is { Strategy: "staticReplacementMap" } loc)
                await ApplyReplacementMapAsync(ctx, Path.Combine(ctx.GenesisRoot, "templates/identity", loc.MapFile), ct);
            //  b) wholesale template emission of core page MARKUPS - the scaffolded .cshtml.cs
            //     code-behind (the logic) is kept untouched. Matches ADR-002b (Login/Register/
            //     Logout); Group A2 extends the same technique to Account/Manage/*, which the
            //     residue map above can never safely reach. The old RestructureManageNav /
            //     InjectPfpFormIntoManageIndex surgical transforms are folded directly into the
            //     new manage_nav_partial.sbn / manage_index_page.sbn templates below and removed.
            foreach (var (page, tpl) in new (string, string)[]
            {
                ("Login", "identity/login_page.sbn"),
                ("Register", "identity/register_page.sbn"),
                ("Logout", "identity/logout_page.sbn"),
            })
            {
                var target = Path.Combine("Areas", "Identity", "Pages", "Account", page + ".cshtml");
                if (File.Exists(Path.Combine(ctx.ProjectDir, target)))
                    await EmitModelsStage.WriteAsync(ctx, target,
                        engine.RenderFile(ctx.Template(tpl)), ct);
            }
            foreach (var (page, tpl) in new (string, string)[]
            {
                ("Index", "identity/manage_index_page.sbn"),
                ("Email", "identity/manage_email_page.sbn"),
                ("ChangePassword", "identity/manage_change_password_page.sbn"),
                ("_ManageNav", "identity/manage_nav_partial.sbn"),
                ("_Layout", "identity/manage_layout_partial.sbn"),
            })
            {
                var target = Path.Combine("Areas", "Identity", "Pages", "Account", "Manage", page + ".cshtml");
                if (File.Exists(Path.Combine(ctx.ProjectDir, target)))
                    await EmitModelsStage.WriteAsync(ctx, target,
                        engine.RenderFile(ctx.Template(tpl)), ct);
            }
        }

        // Root Views/_ViewImports.cshtml (from the initial `dotnet new mvc` scaffold, not
        // dps-nn's own output) needs the same Infrastructure using as the per-area ones below -
        // Views/Shared/*.cshtml partials (e.g. _UserProfileContent) live outside any area and use
        // this file instead. Item 17's ProfileHash helper lives in Infrastructure.
        var rootViewImports = Path.Combine(ctx.ProjectDir, "Views", "_ViewImports.cshtml");
        if (File.Exists(rootViewImports))
            await File.WriteAllTextAsync(rootViewImports,
                $"@using {spec.Meta.RootNamespace}\n@using {spec.Meta.RootNamespace}.Models\n@using {spec.Meta.RootNamespace}.Infrastructure\n@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers\n", ct);

        // Areas: physical folders + a placeholder _ViewStart per area.
        foreach (var area in spec.Areas)
        {
            var areaViews = Path.Combine(ctx.ProjectDir, "Areas", area.Name, "Views");
            Directory.CreateDirectory(Path.Combine(ctx.ProjectDir, "Areas", area.Name, "Controllers"));
            Directory.CreateDirectory(areaViews);
            await File.WriteAllTextAsync(Path.Combine(areaViews, "_ViewStart.cshtml"),
                "@{\n    Layout = \"/Views/Shared/_Layout.cshtml\";\n}\n", ct);
            await File.WriteAllTextAsync(Path.Combine(areaViews, "_ViewImports.cshtml"),
                $"@using {spec.Meta.RootNamespace}\n@using {spec.Meta.RootNamespace}.Models\n@using {spec.Meta.RootNamespace}.Infrastructure\n@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers\n", ct);
        }

        // Bulgarian DataAnnotations messages on the scaffolded code-behind InputModels
        // (Login/Register/Manage/* .cshtml.cs) - ADR-002b leaves this code untouched otherwise,
        // so [Required]/[EmailAddress]/[Compare] fall back to English defaults without this.
        // Skipped for a non-Bulgarian project: the English defaults ARE the correct messages.
        if (spec.Meta.IsBulgarian)
        {
            InjectBgValidationMessages(ctx);
            // Group A2: the scaffolded Manage/*.cshtml.cs TempData StatusMessage assignments
            // ("Your profile has been updated", etc.) are plain C# string-literal assignments,
            // not DataAnnotations - a different, narrower mechanism than the attribute injector
            // above, but the same discipline (exact known literal, never a fragment/blind map).
            InjectBgStatusMessages(ctx);
        }

        (await Proc.RunAsync("dotnet", "build", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("build after identity");
        ctx.Info("identity complete, build green");
    }

    /// <summary>
    /// Adds ErrorMessage="..." (Bulgarian) to bare DataAnnotations attributes inside every
    /// scaffolded Identity .cshtml.cs InputModel. Idempotent: skips attributes that already
    /// carry an ErrorMessage. Regex-based like the deviation transforms (ADR-006) - the
    /// scaffolder's InputModel shape is stable per SDK pin.
    /// </summary>
    internal static void InjectBgValidationMessages(StageContext ctx)
    {
        var pagesDir = Path.Combine(ctx.ProjectDir, "Areas", "Identity", "Pages", "Account");
        if (!Directory.Exists(pagesDir)) return;

        // attribute name -> Bulgarian message template. {0} is filled from the property name
        // for the couple of messages that read better parameterized.
        var messages = new Dictionary<string, string>
        {
            ["Required"] = "Полето {0} е задължително.",
            ["EmailAddress"] = "Невалиден имейл адрес.",
            ["Phone"] = "Невалиден телефонен номер.",
            ["Compare"] = "Паролите не съвпадат.",
        };
        // Bulgarian display names for the property names we know appear in scaffolded InputModels.
        var displayNames = new Dictionary<string, string>
        {
            ["Email"] = "Имейл", ["Password"] = "Парола", ["ConfirmPassword"] = "Потвърдете паролата",
            ["OldPassword"] = "Старата парола", ["NewPassword"] = "Новата парола", ["PhoneNumber"] = "Телефон",
            ["Code"] = "Код", ["TwoFactorCode"] = "Код за двуфакторна автентикация", ["RecoveryCode"] = "Код за възстановяване",
            ["NewEmail"] = "Нов имейл",
        };
        // Group A2: some scaffolded attributes already carry an explicit (English) ErrorMessage
        // rather than a bare attribute - the "fill in the blank" logic below only ADDS a missing
        // message, so these known-literal scaffolder defaults need a direct, exact replacement
        // instead. {0}/{1}/{2} are the DataAnnotations validator's own runtime-filled tokens
        // (field name / max / min) - preserved as tokens, not resolved here.
        var knownEnglishMessages = new Dictionary<string, string>
        {
            ["The {0} must be at least {2} and at max {1} characters long."] = "{0} трябва да е между {2} и {1} символа.",
            ["The new password and confirmation password do not match."] = "Новата парола и потвърждението не съвпадат.",
        };

        // AllDirectories: covers Account/*.cshtml.cs (Login/Register/...) AND Account/Manage/
        // (Group A2) in one pass - the scaffolder only ever creates subdirectories for modules
        // actually requested, so there's nothing else under here to accidentally sweep in.
        foreach (var file in Directory.EnumerateFiles(pagesDir, "*.cshtml.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            var changed = false;

            foreach (var (from, to) in knownEnglishMessages)
            {
                if (!source.Contains(from)) continue;
                source = source.Replace(from, to);
                changed = true;
            }

            // Attributes without any parentheses at all: [Required] -> [Required(ErrorMessage = "...")]
            // We look ahead for "public ... PropName { get; set; }" to know which property the
            // attribute decorates (for the display-name substitution) and whether it already
            // carries ErrorMessage (in which case we leave it alone - idempotent rerun). Window
            // widened from a hardcoded 3 lines to 10: Group A2 surfaced a real bug against
            // ChangePasswordModel.NewPassword, which stacks Required+StringLength+DataType+
            // Display before its own property line - 4 lines away, one past the old window -
            // producing "Полето  е задължително." (blank display name). propMatch only matches
            // an actual `public ... {` declaration, so a wider window can't grab the wrong
            // property; it still stops at the first one found.
            var lines = source.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var (attr, template) in messages)
                {
                    var bareMatch = Regex.Match(lines[i], $@"^(\s*)\[{attr}\]\s*$");
                    var withArgsNoMsg = Regex.Match(lines[i],
                        $@"^(\s*)\[{attr}\(([^)]*)\)\]\s*$");

                    string? propName = null;
                    for (var j = i + 1; j < Math.Min(i + 10, lines.Length); j++)
                    {
                        var propMatch = Regex.Match(lines[j], @"public\s+\S+\s+(\w+)\s*\{");
                        if (propMatch.Success) { propName = propMatch.Groups[1].Value; break; }
                    }
                    var display = propName is not null && displayNames.TryGetValue(propName, out var dn) ? dn : propName ?? "";
                    var message = string.Format(template, display);

                    if (bareMatch.Success)
                    {
                        lines[i] = $"{bareMatch.Groups[1].Value}[{attr}(ErrorMessage = \"{message}\")]";
                        changed = true;
                    }
                    else if (withArgsNoMsg.Success && !withArgsNoMsg.Groups[2].Value.Contains("ErrorMessage"))
                    {
                        lines[i] = $"{withArgsNoMsg.Groups[1].Value}[{attr}({withArgsNoMsg.Groups[2].Value}, ErrorMessage = \"{message}\")]";
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                File.WriteAllText(file, string.Join('\n', lines));
                ctx.Info($"injected BG validation messages into {Path.GetFileName(file)}");
            }
        }
    }

    /// <summary>
    /// Group A2: replaces the scaffolded Manage/*.cshtml.cs TempData StatusMessage string
    /// literals (success/error banners shown after a post) with Bulgarian equivalents. Exact
    /// known-literal replacement only - never a fragment/blind map - so it can never touch a
    /// C# identifier the way the residue-map trap does. _StatusMessage.cshtml's own
    /// `Model.StartsWith("Error")` styling check is unaffected: none of these messages start
    /// with the literal "Error", in either language.
    /// </summary>
    internal static void InjectBgStatusMessages(StageContext ctx)
    {
        var manageDir = Path.Combine(ctx.ProjectDir, "Areas", "Identity", "Pages", "Account", "Manage");
        if (!Directory.Exists(manageDir)) return;

        var known = new Dictionary<string, string>
        {
            ["Unexpected error when trying to set phone number."] = "Възникна грешка при задаване на телефонния номер.",
            ["Your profile has been updated"] = "Профилът е обновен.",
            ["Confirmation link to change email sent. Please check your email."] = "Изпратен е линк за потвърждение на новия имейл. Проверете пощата си.",
            ["Your email is unchanged."] = "Имейлът не е променен.",
            ["Verification email sent. Please check your email."] = "Изпратен е имейл за потвърждение. Проверете пощата си.",
            ["Your password has been changed."] = "Паролата е променена.",
        };

        foreach (var file in Directory.EnumerateFiles(manageDir, "*.cshtml.cs", SearchOption.TopDirectoryOnly))
        {
            var source = File.ReadAllText(file);
            var changed = false;
            foreach (var (from, to) in known)
            {
                if (!source.Contains($"\"{from}\"")) continue;
                source = source.Replace($"\"{from}\"", $"\"{to}\"");
                changed = true;
            }
            if (changed)
            {
                File.WriteAllText(file, source);
                ctx.Info($"injected BG status messages into {Path.GetFileName(file)}");
            }
        }
    }

    /// <summary>
    /// identity-bg-map.json format: { "sdk": "10.0.100", "replacements": { "Log in": "Вход", ... } }
    /// Longest-key-first application so "Log in with" never gets clobbered by "Log in".
    /// </summary>
    internal static async Task ApplyReplacementMapAsync(StageContext ctx, string mapPath, CancellationToken ct)
    {
        if (!File.Exists(mapPath))
        {
            ctx.Info($"WARNING: identity map {mapPath} missing - Identity pages remain English. Verify WILL fail on them.");
            return;
        }
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(mapPath, ct));
        var pairs = doc.RootElement.GetProperty("replacements").EnumerateObject()
            .Select(p => (From: p.Name, To: p.Value.GetString()!))
            .OrderByDescending(p => p.From.Length)
            .ToList();

        var identityPagesDir = Path.Combine(ctx.ProjectDir, "Areas", "Identity", "Pages");
        if (!Directory.Exists(identityPagesDir)) return;
        foreach (var file in Directory.EnumerateFiles(identityPagesDir, "*.cshtml", SearchOption.AllDirectories))
        {
            // Manage/ pages reference ManageNavPages.Email, ManageNavPages.ChangePassword, etc. -
            // real C# static-property access with no quotes/tags around it to distinguish from
            // prose, plus @model ...EmailModel / ...ChangePasswordModel class-name lines. A blind
            // whole-word map (e.g. "Email" -> "Имейл") corrupts BOTH into a compile error, and
            // there's no reliable text-only way to tell "translatable prose" from "bare identifier
            // reference" apart on the same C# code line. Skip the whole Manage/ subtree here -
            // it's new-to-this-session scaffolding, not previously working translated pages.
            if (file.Replace('\\', '/').Contains("/Pages/Account/Manage/", StringComparison.OrdinalIgnoreCase))
                continue;

            var text = await File.ReadAllTextAsync(file, ct);
            foreach (var (from, to) in pairs) text = text.Replace(from, to);
            await File.WriteAllTextAsync(file, text, ct);
        }
        ctx.Info($"identity BG map applied ({pairs.Count} replacements)");
    }

}
