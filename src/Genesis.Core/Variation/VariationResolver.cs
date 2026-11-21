// FILE-MAP: ADR-004 deterministic per-project variation — resolves the 11 style axes from SHA256(seed+axis); also resolves $variation:axis placeholders and seed-user passwords.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GenesisV6.Core.Spec;

namespace GenesisV6.Core.Variation;

/// <summary>
/// Resolves the variation profile for a project (ADR-004).
///
/// DESIGN: cross-project dissimilarity, intra-project consistency.
/// Each axis gets ONE value per project, chosen either explicitly in the spec
/// or derived as SHA256(seed + ":" + axisName) % options.Length. SHA256, not
/// string.GetHashCode(), because GetHashCode is randomized per process since
/// .NET Core - it would break "re-run produces identical output".
/// Every template reads the resolved profile; no template ever rolls dice.
/// The resolved profile is serialized into the lock file and the output report.
/// </summary>
public sealed class VariationResolver
{
    // Axis registry: single source of truth for known axes and their option sets.
    public static readonly IReadOnlyDictionary<string, string[]> Registry = new Dictionary<string, string[]>
    {
        ["collectionInit"] = ["targetTyped", "explicitGeneric", "collectionExpression"],
        ["adminRoleName"] = ["Administrator", "Admin", "Owner"],
        ["userRoleName"] = ["Потребител", "User", "Client"],
        ["seedPasswordPattern"] = ["Word+Year+Symbol", "Symbol+Word+Digits", "CamelWords+Digit"],
        ["namespaceStyle"] = ["fileScoped", "blockScoped"],
        ["diRegistrationStyle"] = ["chained", "separateStatements"],
        ["stringInterpolationVsConcat"] = ["interpolation", "concat"],
        ["viewCommentDensity"] = ["sparse", "moderate"],
        // Visual identity: different generated projects must not look like reskins of one site.
        ["bootstrapTheme"] = ["flatly", "cosmo", "lumen", "sandstone", "united", "yeti", "journal", "spacelab"],
        ["navbarStyle"] = ["navbar-dark bg-dark", "navbar-dark bg-primary", "navbar-light bg-light border-bottom"],
        ["buttonShape"] = ["rounded-pill", "rounded-3"],
    };

    public static ResolvedVariation Resolve(ProjectSpec spec)
    {
        var section = spec.VariationProfile;
        var seed = section?.Seed is { Length: > 0 } s ? s : spec.Meta.ProjectName.ToLowerInvariant();
        var resolved = new Dictionary<string, string>();

        foreach (var (axis, options) in Registry)
        {
            // Explicit pin wins; "<axis>Options" keys in the spec are documentation only.
            if (section?.Axes.TryGetValue(axis, out var el) == true && el.ValueKind == JsonValueKind.String)
                resolved[axis] = el.GetString()!;
            else
                resolved[axis] = options[DeterministicIndex(seed, axis, options.Length)];
        }
        return new ResolvedVariation(seed, resolved);
    }

    internal static int DeterministicIndex(string seed, string axis, int optionCount)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}:{axis}"));
        // First 4 bytes as unsigned int - uniform enough for tiny option sets.
        var value = BitConverter.ToUInt32(bytes, 0);
        return (int)(value % (uint)optionCount);
    }
}

public sealed record ResolvedVariation(string Seed, IReadOnlyDictionary<string, string> Axes)
{
    public string this[string axis] => Axes[axis];

    /// <summary>Replaces "$variation:axisName" placeholders anywhere in a string.</summary>
    public string Substitute(string input)
    {
        var output = input;
        foreach (var (axis, value) in Axes)
            output = output.Replace($"$variation:{axis}", value);
        return output;
    }

    /// <summary>
    /// Deterministic seed-user password from the pattern axis. Deterministic so
    /// verify can re-derive it, but distinct per project+email so no two
    /// generated projects share credentials.
    /// </summary>
    public string SeedPassword(string email)
    {
        var h = SHA256.HashData(Encoding.UTF8.GetBytes($"{Seed}:{email}:pwd"));
        var word = Words[h[0] % Words.Length];
        var year = 2020 + (h[1] % 7);
        var digits = (100 + h[2] % 900).ToString();
        // All three patterns must satisfy ASP.NET Identity's DEFAULT password policy
        // (upper + lower + digit + non-alphanumeric) - a real bug had "CamelWords+Digit"
        // producing zero special characters, crashing IdentitySeeder at first boot.
        return Axes["seedPasswordPattern"] switch
        {
            "Word+Year+Symbol" => $"{word}{year}!",
            "Symbol+Word+Digits" => $"#{word}{digits}",
            _ => $"{word}{char.ToUpper(word[0])}{word[1..]}{h[3] % 10}!", // CamelWords+Digit
        };
    }

    private static readonly string[] Words = ["Sofia", "Plovdiv", "Varna", "Burgas", "Ruse", "Parola", "Password", "Genesis"];
}
