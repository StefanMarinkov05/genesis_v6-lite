// FILE-MAP: `dps-nn doctor` — checks every prerequisite the pipeline needs (SDK, dotnet-ef, scaffolder, LocalDB, templates, writable workspace, API key) and prints the fix for each one missing.
using System.ComponentModel;
using System.Text.RegularExpressions;
using GenesisV6.Core.Pipeline;

namespace GenesisV6.Core.Diagnostics;

public enum CheckStatus { Ok, Warn, Fail }

/// <summary>One line of the report. <see cref="Fix"/> is what to run/install when not Ok.</summary>
public sealed record DoctorCheck(string Name, CheckStatus Status, string Detail, string? Fix = null);

/// <summary>
/// Prerequisite check run by `dps-nn doctor` and by the GUI at startup (into its terminal).
/// Fail = a pipeline run cannot succeed; Warn = an optional capability is unavailable. Every
/// external call goes through <paramref name="run"/> (null result = the program could not be
/// started at all), so the parsing and the verdicts are unit-tested without the real tools.
/// The FAIL fixes repeat the exact hints AgentRunner's environment triage gives mid-run.
/// </summary>
public sealed class Doctor(
    Func<string, string, Task<(int ExitCode, string Output)?>> run,
    Func<string, string?> env,
    bool isWindows,
    string? genesisRoot,
    string workspace)
{
    public const int RequiredMajor = 10;

    public async Task<IReadOnlyList<DoctorCheck>> RunAsync()
    {
        var checks = new List<DoctorCheck>();

        var sdks = await run("dotnet", "--list-sdks");
        var sdk = sdks is { ExitCode: 0 } s ? BestSdk(s.Output) : null;
        checks.Add(sdk is not null
            ? new(".NET SDK", CheckStatus.Ok, sdk)
            : new(".NET SDK", CheckStatus.Fail, sdks is null ? "dotnet not found" : $"no {RequiredMajor}.x SDK installed",
                $"install the .NET {RequiredMajor} SDK from https://dotnet.microsoft.com/download/dotnet/{RequiredMajor}.0"));

        // Global tools need a working dotnet; without one, report them as unknown rather than missing.
        var tools = sdk is null ? null : await run("dotnet", "tool list -g");
        var toolList = tools is { ExitCode: 0 } t ? t.Output : null;
        checks.Add(ToolCheck("dotnet-ef", toolList, sdk is null,
            "dotnet tool install -g dotnet-ef --version 10.0.*   (or: dotnet tool update -g dotnet-ef --version 10.0.*)"));
        checks.Add(ToolCheck("dotnet-aspnet-codegenerator", toolList, sdk is null,
            "dotnet tool install -g dotnet-aspnet-codegenerator"));

        checks.Add(await LocalDbCheck());

        checks.Add(genesisRoot is not null && File.Exists(Path.Combine(genesisRoot, "spec.schema.json"))
            ? new("templates", CheckStatus.Ok, genesisRoot)
            : new("templates", CheckStatus.Fail, "templates/ + spec.schema.json not found next to dps-nn or in the current folder",
                "run dps-nn from the repo root, or reinstall"));

        checks.Add(WorkspaceCheck());

        checks.Add(string.IsNullOrWhiteSpace(env("ANTHROPIC_API_KEY"))
            ? new("Anthropic API key", CheckStatus.Warn, "not set - agent mode (create) and Claude seed generation need it; the bundled FaceNote sample works without it",
                "set ANTHROPIC_API_KEY (GUI: Settings)")
            : new("Anthropic API key", CheckStatus.Ok, "set"));

        return checks;
    }

    /// <summary>Highest installed SDK with the required major version, e.g. "10.0.402".</summary>
    internal static string? BestSdk(string listSdksOutput) =>
        listSdksOutput.Split('\n')
            .Select(l => l.Trim().Split(' ')[0])
            .Where(v => Version.TryParse(v.Split('-')[0], out var p) && p.Major == RequiredMajor)
            .OrderByDescending(v => Version.Parse(v.Split('-')[0]))
            .FirstOrDefault();

    /// <summary>The version column for a package id in `dotnet tool list -g` output.</summary>
    internal static string? ToolVersion(string toolListOutput, string packageId) =>
        toolListOutput.Split('\n')
            .Select(l => Regex.Split(l.Trim(), @"\s+"))
            .FirstOrDefault(cols => cols.Length >= 2 && cols[0].Equals(packageId, StringComparison.OrdinalIgnoreCase))?[1];

    private static DoctorCheck ToolCheck(string id, string? toolList, bool sdkMissing, string fix)
    {
        if (sdkMissing) return new(id, CheckStatus.Fail, "cannot check without the .NET SDK", fix);
        var version = toolList is null ? null : ToolVersion(toolList, id);
        if (version is null) return new(id, CheckStatus.Fail, "not installed", fix);
        var major = Version.TryParse(version.Split('-')[0], out var v) ? v.Major : 0;
        return major >= RequiredMajor
            ? new(id, CheckStatus.Ok, version)
            : new(id, CheckStatus.Fail, $"{version} is too old for net{RequiredMajor}.0 projects", fix);
    }

    private async Task<DoctorCheck> LocalDbCheck()
    {
        const string name = "SQL Server LocalDB";
        const string fix = "install SQL Server Express LocalDB (Visual Studio installer or SQL Server Express), then: sqllocaldb start mssqllocaldb";
        if (!isWindows)
            return new(name, CheckStatus.Fail, "LocalDB is Windows-only; generated apps connect to (localdb)\\mssqllocaldb", "run dps-nn on Windows");
        var info = await run("sqllocaldb", "info");
        if (info is null) return new(name, CheckStatus.Fail, "sqllocaldb not found", fix);
        return info.Value.Output.Contains("MSSQLLocalDB", StringComparison.OrdinalIgnoreCase)
            ? new(name, CheckStatus.Ok, "MSSQLLocalDB instance present")
            : new(name, CheckStatus.Fail, "no MSSQLLocalDB instance", "sqllocaldb create MSSQLLocalDB -s");
    }

    private DoctorCheck WorkspaceCheck()
    {
        try
        {
            var probe = Path.Combine(workspace, $".dps-nn-write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return new("workspace", CheckStatus.Ok, $"{workspace} (writable)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new("workspace", CheckStatus.Fail, $"{workspace} is not writable", "run dps-nn from a folder you can write to");
        }
    }

    /// <summary>Report lines, e.g. "[ok]   .NET SDK  10.0.402" / "[FAIL] dotnet-ef  not installed" + "       fix: ...".</summary>
    public static IEnumerable<string> Format(IReadOnlyList<DoctorCheck> checks)
    {
        var width = checks.Max(c => c.Name.Length);
        foreach (var c in checks)
        {
            var tag = c.Status switch { CheckStatus.Ok => "[ok]  ", CheckStatus.Warn => "[warn]", _ => "[FAIL]" };
            yield return $"{tag} {c.Name.PadRight(width)}  {c.Detail}";
            if (c.Status != CheckStatus.Ok && c.Fix is not null) yield return $"       {"".PadRight(width)}  fix: {c.Fix}";
        }
    }

    /// <summary>The real process runner: null when the program is not on PATH at all.</summary>
    public static async Task<(int, string)?> RunProcessAsync(string file, string args)
    {
        try
        {
            var r = await Proc.RunAsync(file, args, Directory.GetCurrentDirectory(), timeout: TimeSpan.FromSeconds(30));
            return (r.ExitCode, r.StdOut + r.StdErr);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or TimeoutException)
        {
            return null;
        }
    }
}
