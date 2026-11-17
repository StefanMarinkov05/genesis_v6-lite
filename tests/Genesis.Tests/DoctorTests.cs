using GenesisV6.Core.Diagnostics;
using Util = GenesisV6.Core.Util;
using Xunit;

namespace Genesis.Tests;

/// <summary>`dps-nn doctor`: output parsing and verdicts, with a fake process runner (no real tools needed).</summary>
public sealed class DoctorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("dpsnn-doctor-").FullName;
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private const string Sdks = "8.0.404 [C:\\Program Files\\dotnet\\sdk]\n10.0.100 [C:\\Program Files\\dotnet\\sdk]\n10.0.402 [C:\\Program Files\\dotnet\\sdk]\n";
    private const string Tools = """
        Package Id                       Version      Commands
        -------------------------------------------------------------------------
        dotnet-aspnet-codegenerator      10.0.2       dotnet-aspnet-codegenerator
        dotnet-ef                        10.0.13      dotnet-ef
        """;

    private Doctor Make(Dictionary<string, (int, string)?> outputs, bool windows = true, string? key = "sk-test", bool templates = true)
    {
        if (templates) File.WriteAllText(Path.Combine(_root, "spec.schema.json"), "{}");
        return new Doctor(
            (file, args) => Task.FromResult(outputs.TryGetValue($"{file} {args}", out var r) ? r : null),
            name => name == "ANTHROPIC_API_KEY" ? key : null,
            windows, _root, _root);
    }

    private static Dictionary<string, (int, string)?> Healthy() => new()
    {
        ["dotnet --list-sdks"] = (0, Sdks),
        ["dotnet tool list -g"] = (0, Tools),
        ["sqllocaldb info"] = (0, "MSSQLLocalDB\r\n"),
    };

    [Fact]
    public async Task Healthy_machine_has_no_failures()
    {
        var checks = await Make(Healthy()).RunAsync();
        Assert.All(checks, c => Assert.Equal(CheckStatus.Ok, c.Status));
        Assert.Equal("10.0.402", checks.Single(c => c.Name == ".NET SDK").Detail);
        Assert.Equal("10.0.13", checks.Single(c => c.Name == "dotnet-ef").Detail);
    }

    [Fact]
    public async Task Missing_dotnet_fails_sdk_and_tools_without_calling_tool_list()
    {
        var calls = new List<string>();
        var doctor = new Doctor((f, a) => { calls.Add($"{f} {a}"); return Task.FromResult<(int, string)?>(null); },
            _ => null, true, _root, _root);
        var checks = await doctor.RunAsync();
        Assert.Equal(CheckStatus.Fail, checks.Single(c => c.Name == ".NET SDK").Status);
        Assert.Equal(CheckStatus.Fail, checks.Single(c => c.Name == "dotnet-ef").Status);
        Assert.DoesNotContain("dotnet tool list -g", calls);
    }

    [Fact]
    public async Task Only_an_older_sdk_is_a_failure()
    {
        var o = Healthy();
        o["dotnet --list-sdks"] = (0, "8.0.404 [/usr/share/dotnet/sdk]\n");
        var sdk = (await Make(o).RunAsync()).Single(c => c.Name == ".NET SDK");
        Assert.Equal(CheckStatus.Fail, sdk.Status);
        Assert.Contains("10.0", sdk.Fix);
    }

    [Fact]
    public async Task Outdated_dotnet_ef_fails_with_the_update_command()
    {
        var o = Healthy();
        o["dotnet tool list -g"] = (0, Tools.Replace("10.0.13", "8.0.11"));
        var ef = (await Make(o).RunAsync()).Single(c => c.Name == "dotnet-ef");
        Assert.Equal(CheckStatus.Fail, ef.Status);
        Assert.Contains("dotnet tool update -g dotnet-ef", ef.Fix);
    }

    [Fact]
    public async Task Missing_scaffolder_fails()
    {
        var o = Healthy();
        o["dotnet tool list -g"] = (0, string.Join('\n', Tools.Split('\n').Where(l => !l.Contains("codegenerator"))));
        Assert.Equal(CheckStatus.Fail, (await Make(o).RunAsync()).Single(c => c.Name == "dotnet-aspnet-codegenerator").Status);
    }

    [Theory]
    [InlineData(null, "sqllocaldb not found")]
    [InlineData("", "no MSSQLLocalDB instance")]
    public async Task LocalDb_problems_fail(string? infoOutput, string detail)
    {
        var o = Healthy();
        o["sqllocaldb info"] = infoOutput is null ? null : (0, infoOutput);
        var db = (await Make(o).RunAsync()).Single(c => c.Name == "SQL Server LocalDB");
        Assert.Equal(CheckStatus.Fail, db.Status);
        Assert.Equal(detail, db.Detail);
    }

    [Fact]
    public async Task Non_windows_fails_localdb_without_probing()
    {
        var db = (await Make(Healthy(), windows: false).RunAsync()).Single(c => c.Name == "SQL Server LocalDB");
        Assert.Equal(CheckStatus.Fail, db.Status);
        Assert.Contains("Windows-only", db.Detail);
    }

    [Fact]
    public async Task Missing_api_key_is_only_a_warning()
    {
        var checks = await Make(Healthy(), key: null).RunAsync();
        Assert.Equal(CheckStatus.Warn, checks.Single(c => c.Name == "Anthropic API key").Status);
        Assert.DoesNotContain(checks, c => c.Status == CheckStatus.Fail);
    }

    [Fact]
    public async Task Missing_templates_fail()
    {
        var checks = await Make(Healthy(), templates: false).RunAsync();
        Assert.Equal(CheckStatus.Fail, checks.Single(c => c.Name == "templates").Status);
    }

    [Fact]
    public void Format_prints_a_fix_line_only_for_problems()
    {
        var lines = Doctor.Format([
            new("a", CheckStatus.Ok, "fine", "never shown"),
            new("bbb", CheckStatus.Fail, "broken", "do this"),
        ]).ToList();
        Assert.Equal(3, lines.Count);
        Assert.StartsWith("[ok]   a  ", lines[0]);
        Assert.StartsWith("[FAIL] bbb", lines[1]);
        Assert.EndsWith("fix: do this", lines[2]);
    }

    [Fact]
    public void ToolVersion_ignores_header_and_matches_case_insensitively() =>
        Assert.Equal("10.0.2", Doctor.ToolVersion(Tools, "DOTNET-ASPNET-CODEGENERATOR"));
}

/// <summary>DOTNET_ROOT inference for the self-contained exe (what `dotnet run` does for its child).</summary>
public sealed class DotnetHostTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("dpsnn-host-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string FakeInstall(string name, bool withRuntime)
    {
        var root = Directory.CreateDirectory(Path.Combine(_dir, name)).FullName;
        File.WriteAllText(Path.Combine(root, "dotnet"), "");
        if (withRuntime) Directory.CreateDirectory(Path.Combine(root, "shared", "Microsoft.NETCore.App"));
        return root;
    }

    private static (string? root, Dictionary<string, string> set) Run(Dictionary<string, string?> env)
    {
        var set = new Dictionary<string, string>();
        var root = Util.DotnetHost.EnsureDotnetRoot(k => env.GetValueOrDefault(k), (k, v) => set[k] = v, isWindows: false);
        return (root, set);
    }

    [Fact]
    public void Sets_root_from_first_path_entry_that_holds_a_runtime()
    {
        var shim = FakeInstall("shim", withRuntime: false);
        var real = FakeInstall("real", withRuntime: true);
        var (root, set) = Run(new() { ["PATH"] = string.Join(Path.PathSeparator, Path.Combine(_dir, "nope"), shim, real) });
        Assert.Equal(real, root);
        Assert.Equal(real, set["DOTNET_ROOT"]);
    }

    [Fact]
    public void Leaves_an_existing_dotnet_root_alone()
    {
        var real = FakeInstall("real", withRuntime: true);
        var (root, set) = Run(new() { ["DOTNET_ROOT"] = "/custom", ["PATH"] = real });
        Assert.Null(root);
        Assert.Empty(set);
    }

    [Fact]
    public void No_dotnet_on_path_changes_nothing() => Assert.Null(Run(new() { ["PATH"] = _dir }).root);
}
