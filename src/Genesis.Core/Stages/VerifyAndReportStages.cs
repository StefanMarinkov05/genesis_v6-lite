// FILE-MAP: stage 8 — VerifyStage, the blocking gate (build, EF migrate, boot, HTTP checks, English-leak scan); stage 9 — ReportStage (project-summary.json).
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using GenesisV6.Core.Pipeline;
using GenesisV6.Core.Spec;

namespace GenesisV6.Core.Stages;

/// <summary>
/// Stage 8 `verify` - THE blocking gate. Checks, in order (cheapest first):
///  1. clean `dotnet build`
///  2. `ef database update` clean
///  3. app boots (kestrel on a random free port)
///  4. HTTP 200 on `/`; every discovered public route (cardGrids) anonymously; every protected
///     route (admin-area adminTables + [Authorize] feature pages) must REFUSE an anonymous
///     request; then a real form login as the seeded admin (antiforgery token + cookie jar)
///     and every discovered authed route (adminTables + each enabled Client-area feature page).
///     Every declared route must answer 200 - a non-200 is a hard failure (changelog 141;
///     before that it was logged and skipped, so a broken declared page could pass verify).
///  5. spec file manifest is a subset of actual files
///  6. zero English strings in rendered HTML of EVERY page fetched above (B4: whole route
///     surface, not one-per-kind) - visible-text heuristic + allowlist; report written either way
/// Any failure throws - the pipeline stops, state is NOT marked done.
/// </summary>
public sealed class VerifyStage : IStage
{
    public string Name => "verify";

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;

        // 1 + 2
        (await Proc.RunAsync("dotnet", "build --nologo", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("verify: dotnet build");
        (await Proc.RunAsync("dotnet", "ef database update", ctx.ProjectDir, ct, timeout: TimeSpan.FromMinutes(5)))
            .ThrowIfFailed("verify: ef database update");

        // 5 - manifest before boot: cheap, catches emission bugs without a webserver.
        var missing = ManifestCheck(ctx);
        if (missing.Count > 0)
            throw new InvalidOperationException("verify: spec manifest files missing on disk:\n - " + string.Join("\n - ", missing));

        // 3 + 4 + 6
        var port = FreePort();
        var url = $"http://127.0.0.1:{port}";
        using var app = Process.Start(new ProcessStartInfo("dotnet", $"run --no-build --urls {url}")
        { WorkingDirectory = ctx.ProjectDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })
            ?? throw new InvalidOperationException("verify: could not start app");
        try
        {
            var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = true };
            using var http = new HttpClient(handler) { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(30) };
            await WaitForBootAsync(http, ct);

            var pagesToCheck = new List<(string label, string html)>();

            // "/" stays hard-required (a broken landing is a hard failure, not a skip).
            pagesToCheck.Add(("/", await MustGet200Async(http, "/", ct)));

            // B4: scan the whole discovered route surface, not just one page per kind.
            var (publicRoutes, authedRoutes, refuseAnonymous) = DiscoverRoutes(spec);
            var failures = new List<string>();

            // Public routes (every cardGrid) - anonymous.
            foreach (var r in publicRoutes)
                if (await TryGetAsync(http, r, failures, ct) is { } html) pagesToCheck.Add((r, html));

            // Protected routes must refuse an anonymous request (401/403 or a redirect to the
            // login page). Own client: no cookies, no auto-redirect, so the refusal itself is seen.
            using (var anon = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
                   { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(30) })
                foreach (var r in refuseAnonymous)
                {
                    try
                    {
                        using var resp = await anon.GetAsync(r, ct);
                        if (!IsAnonymousRefusal(resp.StatusCode, resp.Headers.Location))
                            failures.Add($"{r}: an anonymous request was not refused (HTTP {(int)resp.StatusCode})");
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                    { failures.Add($"{r}: anonymous request failed ({ex.Message})"); }
                }

            // Authenticated routes (every adminTable + each enabled Client-area feature page):
            // real form login as the seeded admin, then every one must answer 200.
            var admin = spec.Identity?.SeedUsers?.Users.FirstOrDefault(u =>
                spec.Identity.Roles.Any(r => r.IsAdmin && r.Name == u.Role));
            if (authedRoutes.Count > 0)
            {
                if (admin is null)
                    throw new InvalidOperationException(
                        "verify: the spec declares admin/feature pages but no seed user has an admin role, so they cannot be checked");
                await LoginAsync(http, admin.Email,
                    admin.Password ?? ctx.Variation.SeedPassword(admin.Email), ct);
                foreach (var r in authedRoutes)
                    if (await TryGetAsync(http, r, failures, ct) is { } html) pagesToCheck.Add((r, html));
            }

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    $"verify: {failures.Count} declared route(s) failed:\n - " + string.Join("\n - ", failures));

            // 6 - English leak scan over everything just fetched. This gate exists specifically
            // to enforce the Bulgarian-only-UI requirement - it would be nonsensical (and would
            // hard-fail every single page) to run it against a project whose whole point is to
            // BE in English. Skipped entirely, not just non-fatal, for a non-Bulgarian project.
            var reportPath = Path.Combine(ctx.ProjectDir, "wwwroot", "output", "verify-language-report.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            if (spec.Meta.IsBulgarian)
            {
                var leaks = pagesToCheck
                    .SelectMany(p => EnglishLeaks(p.html).Select(l => $"{p.label}: \"{l}\""))
                    .ToList();
                await File.WriteAllTextAsync(reportPath,
                    leaks.Count == 0 ? "OK - no English visible-text leaks detected.\n" : string.Join("\n", leaks), ct);
                if (leaks.Count > 0)
                    throw new InvalidOperationException($"verify: {leaks.Count} English string leak(s) - see {reportPath}\n" +
                        string.Join("\n", leaks.Take(10)));
            }
            else
            {
                await File.WriteAllTextAsync(reportPath,
                    $"SKIPPED - project language is \"{spec.Meta.Language}\", not \"bg\"; the Bulgarian-only-UI language gate does not apply.\n", ct);
            }
        }
        finally
        {
            try { app.Kill(entireProcessTree: true); } catch { /* already exited */ }
        }
        ctx.Info(spec.Meta.IsBulgarian
            ? "verify PASSED: build, db, boot, http, manifest, language"
            : "verify PASSED: build, db, boot, http, manifest (language gate skipped - non-Bulgarian project)");
    }

    internal static List<string> ManifestCheck(StageContext ctx)
    {
        var spec = ctx.Spec;
        var expected = new List<string>();
        expected.AddRange(spec.Models.Select(m => m.File));
        expected.AddRange(spec.Dtos.Select(d => d.File));
        if (spec.CustomValidationAttributes is { } c) expected.Add(c.File);
        expected.AddRange(spec.Controllers.Select(c => c.File));
        if (spec.Identity is { } id)
        {
            expected.Add(id.ApplicationUser.File);
            // BgIdentityErrorDescriber is only emitted for a Bulgarian project (IdentityStage's
            // matching gate) - the manifest expectation MUST agree, or a green English run fails
            // verify over a file that was correctly never written.
            if (id.ErrorDescriber is { } ed && spec.Meta.IsBulgarian) expected.Add(ed.File);
            if (id.ApplicationUser.Controller is { } uc) expected.Add(uc.File);
        }
        if (spec.Views is { } v) { expected.Add(v.Layout.File); expected.AddRange(v.Pages.Select(p => p.File)); }
        if (spec.Seeding is { } s) expected.Add(s.OutputFile);
        return expected.Where(rel => !File.Exists(Path.Combine(ctx.ProjectDir, rel))).ToList();
    }

    /// <summary>
    /// Visible-text English heuristic: strip tags/scripts/styles, then flag runs of
    /// 2+ ASCII words where at least one is a common English word. Latin-script
    /// proper nouns alone (BMW, Facebook) don't trigger; "Add to cart" does.
    /// </summary>
    internal static IEnumerable<string> EnglishLeaks(string html)
    {
        var text = Regex.Replace(html, @"<script.*?</script>|<style.*?</style>", " ", RegexOptions.Singleline);
        text = Regex.Replace(text, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        foreach (Match m in Regex.Matches(text, @"\b[A-Za-z]{2,}(?:\s+[A-Za-z]{2,}){1,}\b"))
        {
            var words = m.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Any(w => CommonEnglish.Contains(w.ToLowerInvariant())))
                yield return m.Value.Trim();
        }
    }

    private static readonly HashSet<string> CommonEnglish =
    [
        "the","and","for","with","your","you","login","log","register","password","email","submit","search",
        "home","about","contact","edit","delete","create","details","add","cart","welcome","please","enter",
        "remember","forgot","account","sign","out","in","new","back","list","save","cancel","required","field",
    ];

    /// <summary>
    /// B4: the full route surface, split by auth requirement and deduped (order-stable).
    /// Public = every cardGrid page (anonymous-reachable). Authed = every adminTable route plus one
    /// landing route per enabled Client-area feature (fetched as the admin). RefuseAnonymous =
    /// the routes an anonymous request must be refused on: adminTable pages in an area mapped to
    /// an admin role, plus the feature pages whose controller is [Authorize] at class level (Cart,
    /// Catalogues, Follows - NOT Posts, whose index is public). Every route must answer 200; a
    /// non-200 fails verify (changelog 141), so these routes must match what the templates emit.
    /// </summary>
    internal static (List<string> Public, List<string> Authed, List<string> RefuseAnonymous) DiscoverRoutes(ProjectSpec spec)
    {
        var pub = new List<string>();
        var authed = new List<string>();
        var refuse = new List<string>();
        void Add(List<string> to, string r) { if (!to.Contains(r)) to.Add(r); }

        foreach (var p in spec.Views?.Pages ?? [])
        {
            if (p.Kind == "cardGrid") Add(pub, ViewFileToRoute(p.File));
            else if (p.Kind == "adminTable")
            {
                var route = ViewFileToRoute(p.File);
                Add(authed, route);
                var parts = p.File.Split('/');
                if (parts.Length > 1 && parts[0] == "Areas" && EmitControllersViewsStage.IsAdminArea(spec, parts[1]))
                    Add(refuse, route);
            }
        }

        var orders = EmitControllersViewsStage.FeatureEnabled(spec, "orders");
        if (orders || EmitControllersViewsStage.FeatureEnabled(spec, "cart"))
        { Add(authed, "/Client/Cart"); Add(refuse, "/Client/Cart"); }
        if (orders)
        { Add(authed, "/Client/Cart/Orders"); Add(refuse, "/Client/Cart/Orders"); }
        if (EmitControllersViewsStage.FeatureEnabled(spec, "posts")) Add(authed, "/Client/Posts");
        if (EmitControllersViewsStage.FeatureEnabled(spec, "follows"))
        { Add(authed, "/Client/Follows/List"); Add(refuse, "/Client/Follows/List"); }
        if (EmitControllersViewsStage.FeatureEnabled(spec, "catalogues"))
        { Add(pub, "/Client/Catalogues/Public"); Add(authed, "/Client/Catalogues"); Add(refuse, "/Client/Catalogues"); }

        return (pub, authed, refuse);
    }

    /// <summary>
    /// What counts as "refused" for an anonymous request: 401, 403, or a redirect to the Identity
    /// login / access-denied page (cookie auth's normal challenge). A 200 is a leak; a 404/500 or
    /// a redirect anywhere else is not a refusal either - the page is broken or misrouted.
    /// </summary>
    internal static bool IsAnonymousRefusal(HttpStatusCode status, Uri? location)
    {
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return true;
        if ((int)status is >= 300 and < 400 && location is not null)
        {
            var target = location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
            return target.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase)
                || target.Contains("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>GET that returns page HTML on 200; anything else is recorded in <paramref name="failures"/>.</summary>
    private static async Task<string?> TryGetAsync(HttpClient http, string path, List<string> failures, CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync(path, ct);
            if (resp.StatusCode == HttpStatusCode.OK)
                return await resp.Content.ReadAsStringAsync(ct);
            failures.Add($"{path}: HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { failures.Add($"{path}: could not fetch ({ex.Message})"); }
        return null;
    }

    /// <summary>Areas/Admin/Views/Cars/Index.cshtml -> /Admin/Cars ; Views/Home/Index.cshtml -> /</summary>
    internal static string ViewFileToRoute(string viewFile)
    {
        var parts = viewFile.Replace(".cshtml", "").Split('/');
        if (parts[0] == "Areas") return $"/{parts[1]}/{parts[3]}" + (parts[4] == "Index" ? "" : $"/{parts[4]}");
        return parts[1] == "Home" && parts[2] == "Index" ? "/" : $"/{parts[1]}" + (parts[2] == "Index" ? "" : $"/{parts[2]}");
    }

    // First boot runs in-context seeding: creating N seed users goes through Identity's
    // deliberately-slow password hashing (PBKDF2) plus inserting every feature model's rows.
    // With 5 seed users + the full feature seed this legitimately exceeds a 60s budget, so the
    // window is generous - a genuinely crashed app still fails fast (the process exits and the
    // GETs keep throwing until this window elapses; a healthy-but-slow boot just needs the room).
    private const int BootWaitSeconds = 180;

    private static async Task WaitForBootAsync(HttpClient http, CancellationToken ct)
    {
        for (var i = 0; i < BootWaitSeconds; i++)
        {
            try { if ((await http.GetAsync("/", ct)).StatusCode != 0) return; }
            catch (HttpRequestException) { await Task.Delay(1000, ct); }
        }
        throw new TimeoutException($"verify: app did not boot within {BootWaitSeconds}s");
    }

    private static async Task<string> MustGet200Async(HttpClient http, string path, CancellationToken ct)
    {
        var resp = await http.GetAsync(path, ct);
        if (resp.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"verify: GET {path} returned {(int)resp.StatusCode}");
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private static async Task LoginAsync(HttpClient http, string email, string password, CancellationToken ct)
    {
        var loginPage = await MustGet200Async(http, "/Identity/Account/Login", ct);
        var token = Regex.Match(loginPage, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""").Groups[1].Value;
        if (token.Length == 0) throw new InvalidOperationException("verify: antiforgery token not found on login page");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "false",
            ["__RequestVerificationToken"] = token,
        });
        var resp = await http.PostAsync("/Identity/Account/Login", form, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"verify: admin login failed ({(int)resp.StatusCode}) for {email}");
    }

    private static int FreePort()
    {
        using var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}

/// <summary>
/// Stage 9 `report`: wwwroot/output/project-summary.json - the machine-readable
/// contract the future documentation skills (diplomen-doc / diplomen-ppt) consume.
/// For each codePointer: resolved file paths + 1-based line ranges + extracted snippets.
/// </summary>
public sealed class ReportStage : IStage
{
    public string Name => "report";

    // codePointer -> (relative files to resolve, anchor regex to slice the relevant range)
    private static readonly Dictionary<string, (Func<StageContext, IEnumerable<string>> files, string anchor)> Pointers = new()
    {
        ["searchEngine"] = (ctx => ctx.Spec.Views!.Pages.Where(p => p.Kind == "cardGrid").Select(p => p.File),
            @"(?s)<script>.*?</script>"),
        ["sorting"] = (ctx => ctx.Spec.Controllers.Where(c => c.Sorting is { Enabled: true }).Select(c => c.File),
            @"(?s)public\s+async\s+Task<IActionResult>\s+Index.*?\n    \}"),
        ["dateTimeLogic"] = (ctx => ctx.Spec.CustomValidationAttributes is { } c ? [c.File] : [],
            @"(?s)public\s+(sealed\s+)?class\s+Date.*"),
        ["imageCrudLogic"] = (ctx => ctx.Spec.Models.Any(m => m.Images is not null) ? ["Services/ImageService.cs"] : [],
            @"(?s)public\s+(sealed\s+)?class\s+ImageService.*"),
        ["authorization"] = (ctx => new[] { ctx.Spec.Identity?.ApplicationUser.Controller?.File }
            .Concat(new[] { "Areas/Client/Controllers/PostsController.cs", "Areas/Client/Controllers/CartController.cs" }
                .Where(f => File.Exists(Path.Combine(ctx.ProjectDir, f))))
            .Where(f => f is not null).Select(f => f!),
            @"\[Authorize[^\]]*\]"),
        ["applicationUserControllerIndex"] = (ctx => ctx.Spec.Identity?.ApplicationUser.Controller is { } uc ? [uc.File] : [],
            @"(?s)public\s+async\s+Task<IActionResult>\s+Index.*?\n    \}"),
    };

    public async Task RunAsync(StageContext ctx, CancellationToken ct = default)
    {
        var spec = ctx.Spec;
        if (spec.OutputReport is not { } report) { ctx.Info("no outputReport section - skipping"); return; }

        var root = new JsonObject
        {
            ["generatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["settings"] = JsonNode.Parse(JsonSerializer.Serialize(spec, ProjectSpec.JsonOpts)),
            ["variationProfileResolved"] = JsonNode.Parse(JsonSerializer.Serialize(ctx.Variation.Axes)),
            ["codePointers"] = new JsonObject(),
        };

        foreach (var include in report.Include)
        {
            if (include.ValueKind != JsonValueKind.Object ||
                !include.TryGetProperty("codePointer", out var cpEl)) continue;
            var cp = cpEl.GetString()!;
            if (!Pointers.TryGetValue(cp, out var def))
            {
                ctx.Info($"WARNING: unknown codePointer '{cp}' - skipped");
                continue;
            }
            var entries = new JsonArray();
            foreach (var rel in def.files(ctx))
            {
                var full = Path.Combine(ctx.ProjectDir, rel);
                if (!File.Exists(full)) { ctx.Info($"WARNING: codePointer {cp}: {rel} missing"); continue; }
                var source = await File.ReadAllTextAsync(full, ct);
                var m = Regex.Match(source, def.anchor);
                var (fromLine, toLine, snippet) = m.Success
                    ? (LineOf(source, m.Index), LineOf(source, m.Index + m.Length), m.Value)
                    : (1, source.Count(c => c == '\n') + 1, source);
                entries.Add(new JsonObject
                {
                    ["file"] = rel,
                    ["fromLine"] = fromLine,
                    ["toLine"] = toLine,
                    ["snippet"] = snippet,
                });
            }
            root["codePointers"]!.AsObject()[cp] = entries;
        }

        var outPath = Path.Combine(ctx.ProjectDir, report.File);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        await File.WriteAllTextAsync(outPath, root.ToJsonString(ProjectSpec.JsonOpts), ct);
        ctx.Info($"report written: {report.File}");
    }

    private static int LineOf(string text, int index) => text[..Math.Min(index, text.Length)].Count(c => c == '\n') + 1;
}
