// FILE-MAP: the single Scriban rendering surface every stage emits code through, plus its helper functions that carry variation into every emitted file.
using GenesisV6.Core.Spec;
using GenesisV6.Core.Variation;
using Scriban;
using Scriban.Runtime;

namespace GenesisV6.Core.Util;

/// <summary>
/// One Scriban wrapper for every emission in the pipeline, so ALL templates see the
/// same helper surface. This is where intra-project consistency is enforced: the
/// variation axes are resolved ONCE and injected as plain values + helper functions,
/// so a template literally cannot vary per-file.
///
/// Helpers exposed to templates:
///   collection_init "Car"      -> "new()" | "new List<Car>()" | "[]"  (per collectionInit axis)
///   ns_open / ns_close         -> file-scoped vs block-scoped namespace tokens
///   clr_type prop              -> C# type incl. nullability
///   variation.axes.xyz         -> raw resolved axis values
/// </summary>
public sealed class TemplateEngine(ProjectSpec spec, ResolvedVariation variation, string? genesisRoot = null)
{
    public string RenderFile(string templatePath, object? extra = null)
    {
        var tpl = Template.Parse(File.ReadAllText(templatePath), templatePath);
        if (tpl.HasErrors)
            throw new InvalidOperationException($"Template errors in {templatePath}:\n" +
                string.Join("\n", tpl.Messages.Select(m => m.ToString())));
        return Render(tpl, extra);
    }

    public string RenderString(string templateText, object? extra = null)
        => Render(Template.Parse(templateText), extra);

    private string Render(Template tpl, object? extra)
    {
        var so = new ScriptObject();
        so.Import(new { spec, meta = spec.Meta, conventions = spec.Conventions },
            renamer: m => ScribanNames(m.Name));
        so.SetValue("variation", ToScript(variation), true);

        so.Import("collection_init", new Func<string, string>(CollectionInit));
        so.Import("ns_open", new Func<string, string>(NsOpen));
        so.Import("ns_close", new Func<string>(NsClose));
        so.Import("clr_type", new Func<PropertySpec, string>(ClrType));
        so.Import("pascal", new Func<string, string>(s => char.ToUpperInvariant(s[0]) + s[1..]));
        so.Import("camel", new Func<string, string>(s => char.ToLowerInvariant(s[0]) + s[1..]));
        so.Import("subst", new Func<string, string>(variation.Substitute));
        // Effective theme: explicit spec value wins ("mode": "explicit"), otherwise the
        // per-project variation axis - so auto-generated specs get visual diversity for free.
        var theme = spec.Meta.Style.Mode == "explicit" && spec.Meta.Style.BootstrapTheme is { Length: > 0 } t
            ? t : variation["bootstrapTheme"];

        // A10: resolve a named design preset (accent/neutrals/fonts/depth over a bootswatch base).
        // meta.style.preset wins; else deterministically pick one from the seed so each project
        // looks intentional, not default. The chosen preset's base overrides the raw theme.
        var design = ResolveDesignPreset();
        if (design is not null)
        {
            theme = design["base"];
            var designObj = new ScriptObject();
            foreach (var (k, v) in design) designObj.SetValue(k, v, true);
            so.SetValue("design", designObj, true);
            so.SetValue("has_design", true, true);
        }
        else so.SetValue("has_design", false, true);

        so.SetValue("effective_theme", theme, true);

        // Feature flags as first-class template booleans: {{ features.posts }}, {{ features.orders }}...
        var features = new ScriptObject();
        foreach (var (fname, cfg) in spec.Features)
            features.SetValue(fname, cfg.TryGetProperty("enabled", out var en) && en.GetBoolean(), true);
        so.SetValue("features", features, true);

        // Deterministic dependency order for seed emission - any model whose manyToOne
        // targets are earlier in this list is guaranteed insertable in one linear pass
        // (fixes a real bug: the old 2-pass heuristic broke as soon as a model was both
        // a child (Product -> Brand/Category) AND a parent (Product <- CartItem/OrderItem)).
        so.SetValue("ordered_models", TopoSortModels(spec.Models), true);

        if (extra is not null) so.Import(extra, renamer: m => ScribanNames(m.Name));

        var ctx = new TemplateContext { MemberRenamer = m => ScribanNames(m.Name) };
        ctx.PushGlobal(so);
        return tpl.Render(ctx);
    }

    private static string ScribanNames(string name) =>
        string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    private static ScriptObject ToScript(ResolvedVariation v)
    {
        var axes = new ScriptObject();
        foreach (var (k, val) in v.Axes) axes.SetValue(k, val, true);
        var so = new ScriptObject();
        so.SetValue("seed", v.Seed, true);
        so.SetValue("axes", axes, true);
        return so;
    }

    public string CollectionInit(string elementType) => variation["collectionInit"] switch
    {
        "explicitGeneric" => $"new List<{elementType}>()",
        "collectionExpression" => "[]",
        _ => "new()",
    };

    public string NsOpen(string ns) => variation["namespaceStyle"] == "blockScoped"
        ? $"namespace {ns}\n{{" : $"namespace {ns};";

    public string NsClose() => variation["namespaceStyle"] == "blockScoped" ? "}" : "";

    internal static List<ModelSpec> TopoSortModels(List<ModelSpec> models)
    {
        var resolved = new HashSet<string>();
        var result = new List<ModelSpec>();
        var remaining = new List<ModelSpec>(models);
        var guard = 0;
        while (remaining.Count > 0 && guard++ < models.Count * models.Count + 10)
        {
            var progressed = false;
            for (var i = remaining.Count - 1; i >= 0; i--)
            {
                var m = remaining[i];
                var targets = m.Relations.Where(r => r.Kind == "manyToOne").Select(r => r.Target).Distinct();
                if (targets.All(t => resolved.Contains(t) || t == m.Name))
                {
                    result.Add(m);
                    resolved.Add(m.Name);
                    remaining.RemoveAt(i);
                    progressed = true;
                }
            }
            if (!progressed) { result.AddRange(remaining); break; }
        }
        return result;
    }

    private Dictionary<string, string>? ResolveDesignPreset()
    {
        var presetsPath = genesisRoot is not null
            ? Path.Combine(genesisRoot, "templates", "design-presets.json")
            : Path.Combine(Directory.GetCurrentDirectory(), "templates", "design-presets.json");
        if (!File.Exists(presetsPath)) return null;
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(presetsPath));
        var presets = doc.RootElement.GetProperty("presets");
        var names = presets.EnumerateObject().Select(p => p.Name).ToList();
        if (names.Count == 0) return null;

        // explicit choice, else deterministic pick from the variation seed
        var chosen = spec.Meta.Style.Preset is { Length: > 0 } pr && names.Contains(pr)
            ? pr
            : names[Math.Abs(variation.Seed.GetHashCode()) % names.Count];

        var node = presets.GetProperty(chosen);
        var map = node.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString() ?? "");
        map["name"] = chosen;
        return map;
    }

    public static string ClrType(PropertySpec p)
    {
        var t = p.Type switch
        {
            "string" => "string",
            "int" => "int",
            "decimal" => "decimal",
            "DateTime" => "DateTime",
            "bool" => "bool",
            "double" => "double",
            var other => other,
        };
        // Nullable ref/value types: nullable:true in the spec wins; strings default to
        // required non-null ("" init) to keep the NRT build clean.
        return p.Nullable ? t + "?" : t;
    }
}
