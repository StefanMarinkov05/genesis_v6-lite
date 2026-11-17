// FILE-MAP: sets DOTNET_ROOT from the `dotnet` on PATH so global tools launched by the self-contained dps-nn.exe find the runtime (what `dotnet run` does for its child).
namespace GenesisV6.Core.Util;

/// <summary>
/// `dotnet run` sets DOTNET_ROOT for the app it launches, so in the repo every tool the pipeline
/// starts (dotnet-aspnet-codegenerator, dotnet-ef - framework-dependent apphosts) finds the
/// runtime. The installed, self-contained dps-nn.exe has no `dotnet` parent: with .NET anywhere
/// but the default/registered location (a per-user install) those tools die with "You must
/// install .NET to run this application". Found running the staged build (changelog 134).
/// </summary>
public static class DotnetHost
{
    /// <summary>
    /// If DOTNET_ROOT is unset, set it to the directory of the first `dotnet` on PATH (symlinks
    /// resolved) that actually holds a runtime. Returns the root it set, or null if it changed nothing.
    /// </summary>
    public static string? EnsureDotnetRoot(Func<string, string?> getEnv, Action<string, string> setEnv, bool isWindows)
    {
        if (!string.IsNullOrEmpty(getEnv("DOTNET_ROOT"))) return null;
        var exe = isWindows ? "dotnet.exe" : "dotnet";
        foreach (var dir in (getEnv("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim('"'), exe);
            if (!File.Exists(candidate)) continue;
            var resolved = new FileInfo(candidate).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? candidate;
            var root = Path.GetDirectoryName(resolved);
            if (root is null || !Directory.Exists(Path.Combine(root, "shared", "Microsoft.NETCore.App"))) continue;
            setEnv("DOTNET_ROOT", root);
            return root;
        }
        return null;
    }
}
